using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Models.ViewModels;
using FEENALOoFINALE.Services;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FEENALOoFINALE.Controllers
{
    [Authorize]
    public class EquipmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MaintenanceSchedulingService _schedulingService;
        private readonly ICacheService _cacheService;
        private readonly IPerformanceMonitoringService? _performanceMonitor;
        private readonly ILogger<EquipmentController> _logger;
        private readonly IDocumentProcessingService _documentProcessingService;
        private readonly IWebHostEnvironment _environment;

        public EquipmentController(ApplicationDbContext context, 
                                  MaintenanceSchedulingService schedulingService, 
                                  ICacheService cacheService,
                                  ILogger<EquipmentController> logger,
                                  IDocumentProcessingService documentProcessingService,
                                  IWebHostEnvironment environment,
                                  IPerformanceMonitoringService? performanceMonitor = null)
        {
            _context = context;
            _schedulingService = schedulingService;
            _cacheService = cacheService;
            _logger = logger;
            _documentProcessingService = documentProcessingService;
            _environment = environment;
            _performanceMonitor = performanceMonitor;
        }

        // GET: Equipment
        public async Task<IActionResult> Index(string searchTerm, int? buildingId, int? roomId, string status, string filter)
        {
            // Start with all equipment
            var query = _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .Include(e => e.MaintenanceLogs)
                .Include(e => e.FailurePredictions)
                .Include(e => e.Alerts)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(e => (e.EquipmentModel != null && e.EquipmentModel.ModelName.Contains(searchTerm)) ||
                                       (e.EquipmentType != null && e.EquipmentType.EquipmentTypeName.Contains(searchTerm)) ||
                                       (e.Notes != null && e.Notes.Contains(searchTerm)));
                ViewBag.SearchTerm = searchTerm;
            }

            if (buildingId.HasValue)
            {
                query = query.Where(e => e.BuildingId == buildingId.Value);
                ViewBag.SelectedBuildingId = buildingId.Value;
            }

            if (roomId.HasValue)
            {
                query = query.Where(e => e.RoomId == roomId.Value);
                ViewBag.SelectedRoomId = roomId.Value;
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<EquipmentStatus>(status, out var statusEnum))
                {
                    query = query.Where(e => e.Status == statusEnum);
                }
                ViewBag.SelectedStatus = status;
            }

            // Handle special filters from dashboard
            if (!string.IsNullOrEmpty(filter))
            {
                switch (filter.ToLower())
                {
                    case "needsattention":
                        // Equipment that needs attention: has open alerts, overdue maintenance, or inactive status
                        query = query.Where(e => 
                            e.Status == EquipmentStatus.Inactive || 
                            e.Status == EquipmentStatus.Retired ||
                            (e.Alerts != null && e.Alerts.Any(a => a.Status == AlertStatus.Open || a.Status == AlertStatus.InProgress)) ||
                            (e.MaintenanceLogs != null && e.MaintenanceLogs.Any(ml => ml.LogDate.AddDays(30) < DateTime.Now && ml.Status == MaintenanceStatus.Pending)));
                        ViewBag.FilterMessage = "Showing equipment that needs immediate attention";
                        break;
                }
                ViewBag.SelectedFilter = filter;
            }

            // Load filter data
            ViewBag.Buildings = await _context.Buildings.OrderBy(b => b.BuildingName).ToListAsync();
            ViewBag.Rooms = await _context.Rooms.Include(r => r.Building).OrderBy(r => r.Building != null ? r.Building.BuildingName : "").ThenBy(r => r.RoomName).ToListAsync();

            var equipment = await query.OrderBy(e => e.Building != null ? e.Building.BuildingName : "")
                                     .ThenBy(e => e.Room != null ? e.Room.RoomName : "")
                                     .ThenBy(e => e.EquipmentType != null ? e.EquipmentType.EquipmentTypeName : "")
                                     .ToListAsync();

            return View(equipment);
        }

        // GET: Equipment/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var equipment = await _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .Include(e => e.MaintenanceLogs)
                .Include(e => e.FailurePredictions)
                .Include(e => e.Alerts)
                .FirstOrDefaultAsync(m => m.EquipmentId == id);

            if (equipment == null)
            {
                return NotFound();
            }

            return View(equipment);
        }

        // GET: Equipment/Create
        public async Task<IActionResult> Create()
        {
            ViewData["EquipmentTypeId"] = new SelectList(await _context.EquipmentTypes.ToListAsync(), "EquipmentTypeId", "EquipmentTypeName");
            ViewData["EquipmentModelId"] = new SelectList(await _context.EquipmentModels.ToListAsync(), "EquipmentModelId", "ModelName");
            ViewData["BuildingId"] = new SelectList(await _context.Buildings.ToListAsync(), "BuildingId", "BuildingName");
            ViewData["RoomId"] = new SelectList(await _context.Rooms.ToListAsync(), "RoomId", "RoomName");
            return View();
        }

        // POST: Equipment/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EquipmentId,EquipmentTypeId,EquipmentModelName,BuildingId,RoomId,InstallationDate,ExpectedLifespanMonths,Status,Notes")] Equipment equipment)
        {
            // Track performance if service is available
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Enhanced validation with user-friendly messages
            if (string.IsNullOrWhiteSpace(equipment.EquipmentModelName))
            {
                ModelState.AddModelError("EquipmentModelName", "Equipment model name is required. Please enter a descriptive name for this equipment.");
            }

            if (equipment.EquipmentTypeId <= 0)
            {
                ModelState.AddModelError("EquipmentTypeId", "Please select a valid equipment type from the dropdown menu.");
            }

            if (equipment.BuildingId <= 0)
            {
                ModelState.AddModelError("BuildingId", "Please select the building where this equipment is located.");
            }

            if (equipment.RoomId <= 0)
            {
                ModelState.AddModelError("RoomId", "Please select the specific room where this equipment is installed.");
            }

            if (equipment.InstallationDate.HasValue && equipment.InstallationDate.Value > DateTime.Now)
            {
                ModelState.AddModelError("InstallationDate", "Installation date cannot be in the future. Please enter a valid past date.");
            }

            if (equipment.ExpectedLifespanMonths <= 0)
            {
                ModelState.AddModelError("ExpectedLifespanMonths", "Expected lifespan must be greater than 0 months. Please enter a realistic lifespan estimate.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Verify references exist
                    var equipmentTypeExists = await _context.EquipmentTypes.AnyAsync(et => et.EquipmentTypeId == equipment.EquipmentTypeId);
                    var buildingExists = await _context.Buildings.AnyAsync(b => b.BuildingId == equipment.BuildingId);
                    var roomExists = await _context.Rooms.AnyAsync(r => r.RoomId == equipment.RoomId && r.BuildingId == equipment.BuildingId);

                    if (!equipmentTypeExists)
                    {
                        ModelState.AddModelError("EquipmentTypeId", "The selected equipment type is no longer available. Please refresh the page and try again.");
                        await PopulateDropdowns();
                        return View(equipment);
                    }

                    if (!buildingExists)
                    {
                        ModelState.AddModelError("BuildingId", "The selected building is no longer available. Please refresh the page and try again.");
                        await PopulateDropdowns();
                        return View(equipment);
                    }

                    if (!roomExists)
                    {
                        ModelState.AddModelError("RoomId", "The selected room is not available in the chosen building. Please select a different room or building.");
                        await PopulateDropdowns();
                        return View(equipment);
                    }            // Set default values
            equipment.Status = equipment.Status == 0 ? EquipmentStatus.Active : equipment.Status;

            _context.Add(equipment);
            await _context.SaveChangesAsync();

                    // Clear cache
                    await _cacheService.RemoveAsync("equipment_list");
                    await _cacheService.RemoveAsync("dashboard_equipment_metrics");            TempData["SuccessMessage"] = $"Equipment '{equipment.EquipmentModelName}' has been successfully added to {await GetBuildingName(equipment.BuildingId)}.";
            
            // Track performance
            stopwatch.Stop();
            _performanceMonitor?.TrackOperation("Equipment.Create", stopwatch.Elapsed, true);
            
            return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    _logger?.LogError(ex, "Database error while creating equipment");
                    ModelState.AddModelError("", "A database error occurred while saving the equipment. Please try again or contact support if the problem persists.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Unexpected error while creating equipment");
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again or contact support if the problem persists.");
                }
            }

            await PopulateDropdowns();
            return View(equipment);
        }

        private async Task PopulateDropdowns()
        {
            ViewData["EquipmentTypeId"] = new SelectList(await _cacheService.GetOrSetAsync("equipment_types", 
                async () => await _context.EquipmentTypes.ToListAsync(), TimeSpan.FromMinutes(30)), 
                "EquipmentTypeId", "EquipmentTypeName");
            
            ViewData["BuildingId"] = new SelectList(await _cacheService.GetOrSetAsync("buildings", 
                async () => await _context.Buildings.ToListAsync(), TimeSpan.FromMinutes(30)), 
                "BuildingId", "BuildingName");
            
            ViewData["RoomId"] = new SelectList(new List<Room>(), "RoomId", "RoomName");
        }

        private async Task<string> GetBuildingName(int buildingId)
        {
            var building = await _context.Buildings.FindAsync(buildingId);
            return building?.BuildingName ?? "Unknown Building";
        }

        // GET: Equipment/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var equipment = await _context.Equipment
                .Include(e => e.EquipmentModel)
                .Include(e => e.EquipmentType)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .FirstOrDefaultAsync(e => e.EquipmentId == id);
            
            if (equipment == null)
            {
                return NotFound();
            }

            // Create edit view model
            var viewModel = new EquipmentEditViewModel
            {
                EquipmentId = equipment.EquipmentId,
                EquipmentTypeId = equipment.EquipmentTypeId,
                EquipmentModelName = equipment.EquipmentModel?.ModelName ?? "",
                BuildingId = equipment.BuildingId,
                RoomId = equipment.RoomId,
                InstallationDate = equipment.InstallationDate,
                ExpectedLifespanMonths = equipment.ExpectedLifespanMonths,
                Status = equipment.Status,
                Notes = equipment.Notes,
                EquipmentTypeName = equipment.EquipmentType?.EquipmentTypeName,
                BuildingName = equipment.Building?.BuildingName,
                RoomName = equipment.Room?.RoomName,
                CurrentLocation = $"{equipment.Building?.BuildingName} - {equipment.Room?.RoomName}"
            };

            // Get existing documents for this equipment model (shared across all equipment of same model)
            var existingDocs = await _context.ManufacturerDocuments
                .Where(d => d.EquipmentModelId == equipment.EquipmentModelId)
                .Select(d => new DocumentInfo
                {
                    DocumentId = d.DocumentId,
                    FileName = d.FileName,
                    DocumentType = d.DocumentType ?? "Unknown",
                    FileSizeBytes = d.FileSize,
                    UploadDate = d.UploadDate,
                    FilePath = d.FilePath
                })
                .ToListAsync();

            viewModel.CurrentDocuments = existingDocs;

            // Populate dropdown data
            ViewBag.EquipmentTypeId = new SelectList(
                await _context.EquipmentTypes.OrderBy(t => t.EquipmentTypeName).ToListAsync(), 
                "EquipmentTypeId", "EquipmentTypeName", equipment.EquipmentTypeId);
            
            ViewBag.BuildingId = new SelectList(
                await _context.Buildings.OrderBy(b => b.BuildingName).ToListAsync(), 
                "BuildingId", "BuildingName", equipment.BuildingId);
            
            ViewBag.RoomId = new SelectList(
                await _context.Rooms.Where(r => r.BuildingId == equipment.BuildingId)
                    .OrderBy(r => r.RoomName).ToListAsync(), 
                "RoomId", "RoomName", equipment.RoomId);

            return View(viewModel);
        }

        // POST: Equipment/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EquipmentEditViewModel viewModel)
        {
            if (id != viewModel.EquipmentId)
            {
                return NotFound();
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(viewModel.EquipmentModelName))
            {
                ModelState.AddModelError("EquipmentModelName", "Equipment Model is required.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the original equipment for comparison and update
                    var equipment = await _context.Equipment
                        .FirstOrDefaultAsync(e => e.EquipmentId == id);

                    if (equipment == null)
                    {
                        return NotFound();
                    }

                    var originalStatus = equipment.Status;

                    // Find or create the equipment model
                    var existingModel = await _context.EquipmentModels
                        .FirstOrDefaultAsync(m => m.ModelName.ToLower() == viewModel.EquipmentModelName!.ToLower() 
                                                && m.EquipmentTypeId == viewModel.EquipmentTypeId);

                    if (existingModel != null)
                    {
                        // Use existing model
                        equipment.EquipmentModelId = existingModel.EquipmentModelId;
                    }
                    else
                    {
                        // Create new model
                        var newModel = new EquipmentModel
                        {
                            ModelName = viewModel.EquipmentModelName!.Trim(),
                            EquipmentTypeId = viewModel.EquipmentTypeId
                        };
                        
                        _context.EquipmentModels.Add(newModel);
                        await _context.SaveChangesAsync(); // Save to get the ID
                        equipment.EquipmentModelId = newModel.EquipmentModelId;
                    }

                    // Update equipment properties
                    equipment.EquipmentTypeId = viewModel.EquipmentTypeId;
                    equipment.BuildingId = viewModel.BuildingId;
                    equipment.RoomId = viewModel.RoomId;
                    equipment.InstallationDate = viewModel.InstallationDate;
                    equipment.ExpectedLifespanMonths = viewModel.ExpectedLifespanMonths;
                    equipment.Status = viewModel.Status;
                    equipment.Notes = viewModel.Notes;
                    
                    // Handle average weekly usage hours if provided
                    if (viewModel.AverageWeeklyUsageHours.HasValue)
                    {
                        equipment.AverageWeeklyUsageHours = viewModel.AverageWeeklyUsageHours.Value;
                    }

                    _context.Update(equipment);

                    // Handle document uploads
                    if (viewModel.ManufacturerDocuments != null && viewModel.ManufacturerDocuments.Any())
                    {
                        var documentProcessingService = HttpContext.RequestServices.GetService<DocumentProcessingService>();
                        
                        if (documentProcessingService != null)
                        {
                            foreach (var file in viewModel.ManufacturerDocuments)
                            {
                                if (file.Length > 0)
                                {
                                    try
                                    {
                                        var saveResult = await documentProcessingService.SaveDocumentAsync(
                                            file, 
                                            equipment.EquipmentId, 
                                            "Manual Upload" // Default document type
                                        );
                                        
                                        if (!saveResult)
                                        {
                                            _logger.LogWarning("Failed to save document {FileName} for equipment {EquipmentId}", 
                                                file.FileName, equipment.EquipmentId);
                                        }
                                        else
                                        {
                                            _logger.LogInformation("Successfully saved document {FileName} for equipment {EquipmentId}", 
                                                file.FileName, equipment.EquipmentId);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error saving document {FileName} for equipment {EquipmentId}", 
                                            file.FileName, equipment.EquipmentId);
                                    }
                                }
                            }
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Generate condition-based alerts only when status changes to problematic states
                    if (originalStatus != equipment.Status)
                    {
                        await GenerateConditionBasedAlert(equipment, originalStatus);
                    }

                    TempData["SuccessMessage"] = "Equipment updated successfully!";
                    return RedirectToAction(nameof(Details), new { id = equipment.EquipmentId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EquipmentExists(viewModel.EquipmentId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating equipment: {ex.Message}";
                }
            }

            // If we got this far, something failed, redisplay form
            // Repopulate dropdown data
            ViewData["EquipmentTypeId"] = new SelectList(
                await _context.EquipmentTypes.OrderBy(t => t.EquipmentTypeName).ToListAsync(), 
                "EquipmentTypeId", "EquipmentTypeName", viewModel.EquipmentTypeId);
            
            ViewData["BuildingId"] = new SelectList(
                await _context.Buildings.OrderBy(b => b.BuildingName).ToListAsync(), 
                "BuildingId", "BuildingName", viewModel.BuildingId);
            
            ViewData["RoomId"] = new SelectList(
                await _context.Rooms.Where(r => r.BuildingId == viewModel.BuildingId)
                    .OrderBy(r => r.RoomName).ToListAsync(), 
                "RoomId", "RoomName", viewModel.RoomId);

            // Reload existing documents for display (by equipment model)
            var equipmentForDocs = await _context.Equipment.FindAsync(viewModel.EquipmentId);
            if (equipmentForDocs?.EquipmentModelId != null)
            {
                var existingDocs = await _context.ManufacturerDocuments
                    .Where(d => d.EquipmentModelId == equipmentForDocs.EquipmentModelId)
                    .Select(d => new DocumentInfo
                    {
                        DocumentId = d.DocumentId,
                        FileName = d.FileName,
                        DocumentType = d.DocumentType ?? "Unknown",
                        FileSizeBytes = d.FileSize,
                        UploadDate = d.UploadDate,
                        FilePath = d.FilePath
                    })
                    .ToListAsync();

                viewModel.CurrentDocuments = existingDocs;
            }

            return View(viewModel);
        }

        // GET: Equipment/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var equipment = await _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .FirstOrDefaultAsync(m => m.EquipmentId == id);
            
            if (equipment == null)
            {
                return NotFound();
            }

            // Count related data that will be deleted
            var relatedAlertsCount = await _context.Alerts.CountAsync(a => a.EquipmentId == id);
            var relatedMaintenanceLogsCount = await _context.MaintenanceLogs.CountAsync(ml => ml.EquipmentId == id);
            var relatedFailurePredictionsCount = await _context.FailurePredictions.CountAsync(fp => fp.EquipmentId == id);

            ViewBag.RelatedAlertsCount = relatedAlertsCount;
            ViewBag.RelatedMaintenanceLogsCount = relatedMaintenanceLogsCount;
            ViewBag.RelatedFailurePredictionsCount = relatedFailurePredictionsCount;
            ViewBag.HasRelatedData = relatedAlertsCount > 0 || relatedMaintenanceLogsCount > 0 || relatedFailurePredictionsCount > 0;

            return View(equipment);
        }

        // POST: Equipment/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var equipment = await _context.Equipment.FindAsync(id);
                if (equipment == null)
                {
                    TempData["ErrorMessage"] = "Equipment not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Delete related alerts first
                var relatedAlerts = await _context.Alerts
                    .Where(a => a.EquipmentId == id)
                    .ToListAsync();
                
                if (relatedAlerts.Any())
                {
                    _context.Alerts.RemoveRange(relatedAlerts);
                    await _context.SaveChangesAsync();
                }

                // Delete related maintenance logs
                var relatedMaintenanceLogs = await _context.MaintenanceLogs
                    .Where(ml => ml.EquipmentId == id)
                    .ToListAsync();
                
                if (relatedMaintenanceLogs.Any())
                {
                    _context.MaintenanceLogs.RemoveRange(relatedMaintenanceLogs);
                    await _context.SaveChangesAsync();
                }

                // Delete related failure predictions
                var relatedFailurePredictions = await _context.FailurePredictions
                    .Where(fp => fp.EquipmentId == id)
                    .ToListAsync();
                
                if (relatedFailurePredictions.Any())
                {
                    _context.FailurePredictions.RemoveRange(relatedFailurePredictions);
                    await _context.SaveChangesAsync();
                }

                // Finally, delete the equipment
                _context.Equipment.Remove(equipment);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                TempData["SuccessMessage"] = $"Equipment '{equipment.EquipmentModel?.ModelName ?? "Unknown"}' and all related data have been successfully deleted.";
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"Database error while deleting equipment: {dbEx.InnerException?.Message ?? dbEx.Message}";
                return RedirectToAction(nameof(Delete), new { id = id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"Unexpected error deleting equipment: {ex.Message}";
                return RedirectToAction(nameof(Delete), new { id = id });
            }
            
            return RedirectToAction(nameof(Index));
        }

        // GET: Equipment/MaintenanceHistory/5
        public async Task<IActionResult> MaintenanceHistory(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var equipment = await _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .Include(e => e.MaintenanceLogs)
                .FirstOrDefaultAsync(m => m.EquipmentId == id);

            if (equipment != null && equipment.MaintenanceLogs != null)
            {
                equipment.MaintenanceLogs = equipment.MaintenanceLogs
                    .OrderByDescending(ml => ml.LogDate)
                    .ToList();
            }

            if (equipment == null)
            {
                return NotFound();
            }

            return View(equipment);
        }

        // GET: Equipment/PredictionHistory/5
        public async Task<IActionResult> PredictionHistory(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var equipment = await _context.Equipment
                .Include(e => e.FailurePredictions)
                .FirstOrDefaultAsync(m => m.EquipmentId == id);

            if (equipment == null)
            {
                return NotFound();
            }

            return View(equipment);
        }

        private bool EquipmentExists(int id)
        {
            return _context.Equipment.Any(e => e.EquipmentId == id);
        }

        [HttpGet]
        public async Task<JsonResult> GetEquipmentModels(int equipmentTypeId)
        {
            var models = await _context.EquipmentModels
                .Where(em => em.EquipmentTypeId == equipmentTypeId)
                .OrderBy(em => em.ModelName)
                .Select(em => new 
                { 
                    value = em.EquipmentModelId, 
                    text = em.ModelName 
                })
                .ToListAsync();

            return Json(models);
        }

        [HttpGet]
        public async Task<JsonResult> GetRooms(int buildingId)
        {
            var rooms = await _context.Rooms
                .Where(r => r.BuildingId == buildingId)
                .OrderBy(r => r.RoomName)
                .Select(r => new { value = r.RoomId, text = r.RoomName })
                .ToListAsync();
            return Json(rooms);
        }

        [HttpGet]
        public async Task<JsonResult> GetRoomsByBuilding(int buildingId)
        {
            var rooms = await _context.Rooms
                .Where(r => r.BuildingId == buildingId)
                .OrderBy(r => r.RoomName)
                .Select(r => new { value = r.RoomId, text = r.RoomName })
                .ToListAsync();
            return Json(rooms);
        }

        [HttpPost]
        public async Task<IActionResult> AddEquipmentType(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Json(new { success = false });
            }

            var newEquipmentType = new EquipmentType { EquipmentTypeName = name };
            _context.EquipmentTypes.Add(newEquipmentType);
            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync("equipment_types");

            return Json(new { success = true, newId = newEquipmentType.EquipmentTypeId, text = newEquipmentType.EquipmentTypeName });
        }

        [HttpPost]
        public async Task<IActionResult> AddEquipmentModel(int equipmentTypeId, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Json(new { success = false });
            }

            // Look up the equipment type by id
            var equipmentType = await _context.EquipmentTypes
                .FirstOrDefaultAsync(et => et.EquipmentTypeId == equipmentTypeId);

            if (equipmentType == null)
            {
                return Json(new { success = false, error = "Invalid equipment type." });
            }

            var newEquipmentModel = new EquipmentModel 
            { 
                ModelName = name, 
                EquipmentTypeId = equipmentTypeId,
                EquipmentType = equipmentType // Set the required EquipmentType property
            };
            
            _context.EquipmentModels.Add(newEquipmentModel);
            await _context.SaveChangesAsync();

            return Json(new { success = true, newId = newEquipmentModel.EquipmentModelId, text = newEquipmentModel.ModelName });
        }

        [HttpPost]
        public async Task<IActionResult> AddBuilding(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Json(new { success = false });
            }

            var newBuilding = new Building { BuildingName = name };
            _context.Buildings.Add(newBuilding);
            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync("buildings");

            return Json(new { success = true, newId = newBuilding.BuildingId, text = newBuilding.BuildingName });
        }

        [HttpPost]
        public async Task<IActionResult> AddRoom(string name, int buildingId)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Json(new { success = false, message = "Room name is required." });
            }

            var building = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == buildingId);
            if (building == null)
            {
                return Json(new { success = false, message = "Selected building not found." });
            }

            var newRoom = new Room { RoomName = name, BuildingId = buildingId };
            _context.Rooms.Add(newRoom);
            await _context.SaveChangesAsync();

            return Json(new { success = true, newId = newRoom.RoomId, text = newRoom.RoomName });
        }
        [HttpGet]
        public async Task<JsonResult> GetEquipmentTypes()
        {
            var cacheKey = "equipment_types";
            var equipmentTypes = await _cacheService.GetAsync<List<EquipmentType>>(cacheKey);
            if (equipmentTypes == null)
            {
                equipmentTypes = await _context.EquipmentTypes
                    .Select(et => new EquipmentType { EquipmentTypeId = et.EquipmentTypeId, EquipmentTypeName = et.EquipmentTypeName })
                    .ToListAsync();
                await _cacheService.SetAsync(cacheKey, equipmentTypes, TimeSpan.FromMinutes(30));
            }
            return Json(equipmentTypes.Select(et => new { value = et.EquipmentTypeId, text = et.EquipmentTypeName }));
        }

        [HttpGet]
        public async Task<JsonResult> GetBuildings()
        {
            var cacheKey = "buildings";
            var buildings = await _cacheService.GetAsync<List<Building>>(cacheKey);
            if (buildings == null)
            {
                buildings = await _context.Buildings
                    .Select(b => new Building { BuildingId = b.BuildingId, BuildingName = b.BuildingName })
                    .ToListAsync();
                await _cacheService.SetAsync(cacheKey, buildings, TimeSpan.FromMinutes(30));
            }
            return Json(buildings.Select(b => new { value = b.BuildingId, text = b.BuildingName }));
        }    

        // Generate condition-based alerts when equipment status changes
        private async Task GenerateConditionBasedAlert(Equipment equipment, EquipmentStatus previousStatus)
        {
            string? alertTitle = null;
            string? alertDescription = null;
            AlertPriority priority = AlertPriority.Low;

            // Only generate alerts for problematic status changes
            switch (equipment.Status)
            {
                case EquipmentStatus.Inactive:
                    if (previousStatus == EquipmentStatus.Active)
                    {
                        alertTitle = "Equipment Became Inactive";
                        alertDescription = $"Equipment has changed from Active to Inactive status. Immediate attention may be required.";
                        priority = AlertPriority.High;
                    }
                    break;

                case EquipmentStatus.Retired:
                    if (previousStatus != EquipmentStatus.Retired)
                    {
                        alertTitle = "Equipment Retired";
                        alertDescription = $"Equipment has been retired from service. Consider replacement planning.";
                        priority = AlertPriority.Low;
                    }
                    break;

                case EquipmentStatus.Active:
                    // Good news - equipment is back to active, no alert needed
                    // But we could optionally create a "resolved" type notification
                    if (previousStatus == EquipmentStatus.Inactive)
                    {
                        alertTitle = "Equipment Restored to Active";
                        alertDescription = $"Equipment has been successfully restored to active status.";
                        priority = AlertPriority.Low;
                    }
                    break;
            }

            // Only create alert if we determined one is needed
            if (!string.IsNullOrEmpty(alertTitle) && !string.IsNullOrEmpty(alertDescription))
            {
                // Check if a similar alert already exists for this equipment
                var existingAlert = await _context.Alerts
                    .Where(a => a.EquipmentId == equipment.EquipmentId && 
                               a.Status == AlertStatus.Open && 
                               a.Title == alertTitle)
                    .FirstOrDefaultAsync();

                if (existingAlert == null)
                {
                    var alert = new Alert
                    {
                        Title = alertTitle,
                        Description = alertDescription,
                        Priority = priority,
                        Status = AlertStatus.Open,
                        CreatedDate = DateTime.Now,
                        EquipmentId = equipment.EquipmentId,
                        AssignedToUserId = null // Can be assigned later
                    };

                    _context.Alerts.Add(alert);
                    await _context.SaveChangesAsync();
                }
            }
        }

        private async Task GenerateNewEquipmentAlert(Equipment equipment)
        {
            string? alertTitle = null;
            string? alertDescription = null;
            AlertPriority priority = AlertPriority.Low;

            // Generate alerts for newly added equipment with problematic status
            switch (equipment.Status)
            {
                case EquipmentStatus.Inactive:
                    alertTitle = "New Equipment Added with Inactive Status";
                    alertDescription = $"New equipment has been added with Inactive status. Please verify if this is correct and take appropriate action.";
                    priority = AlertPriority.High;
                    break;

                case EquipmentStatus.Retired:
                    alertTitle = "New Equipment Added with Retired Status";
                    alertDescription = $"New equipment has been added with Retired status. Please verify if this is correct.";
                    priority = AlertPriority.Medium;
                    break;

                case EquipmentStatus.Active:
                    // Optional: Create a welcome/tracking alert for new active equipment
                    alertTitle = "New Active Equipment Added";
                    alertDescription = $"New equipment has been successfully added to the system with Active status.";
                    priority = AlertPriority.Low;
                    break;
            }

            // Create alert if one is needed
            if (!string.IsNullOrEmpty(alertTitle) && !string.IsNullOrEmpty(alertDescription))
            {
                var alert = new Alert
                {
                    Title = alertTitle,
                    Description = alertDescription,
                    Priority = priority,
                    Status = AlertStatus.Open,
                    CreatedDate = DateTime.Now,
                    EquipmentId = equipment.EquipmentId,
                    AssignedToUserId = null // Can be assigned later
                };

                _context.Alerts.Add(alert);
                await _context.SaveChangesAsync();
            }
        }

        // Check for overdue maintenance and generate condition-based alerts
        public async Task<IActionResult> CheckMaintenanceOverdue()
        {
            var alertsGenerated = 0;
            
            // Get all active equipment with their maintenance logs
            var equipmentList = await _context.Equipment
                .Where(e => e.Status == EquipmentStatus.Active)
                .Include(e => e.MaintenanceLogs)
                .Include(e => e.EquipmentType)
                .ToListAsync();

            foreach (var equipment in equipmentList)
            {
                var lastMaintenance = equipment.MaintenanceLogs?
                    .OrderByDescending(ml => ml.LogDate)
                    .FirstOrDefault();

                // Consider equipment overdue if no maintenance in last 90 days for active equipment
                var daysSinceLastMaintenance = lastMaintenance != null ? 
                    (DateTime.Now - lastMaintenance.LogDate).Days : 
                    equipment.InstallationDate.HasValue ? (DateTime.Now - equipment.InstallationDate.Value).Days : 365;

                if (daysSinceLastMaintenance > 90)
                {
                    // Check if alert already exists
                    var existingAlert = await _context.Alerts
                        .Where(a => a.EquipmentId == equipment.EquipmentId && 
                                   a.Status == AlertStatus.Open && 
                                   a.Title!.Contains("Maintenance Overdue"))
                        .FirstOrDefaultAsync();

                    if (existingAlert == null)
                    {
                        var alert = new Alert
                        {
                            Title = "Maintenance Overdue",
                            Description = $"Equipment {equipment.EquipmentType?.EquipmentTypeName} has not received maintenance for {daysSinceLastMaintenance} days. Recommended maintenance interval exceeded.",
                            Priority = daysSinceLastMaintenance > 180 ? AlertPriority.High : AlertPriority.Medium,
                            Status = AlertStatus.Open,
                            CreatedDate = DateTime.Now,
                            EquipmentId = equipment.EquipmentId,
                            AssignedToUserId = null
                        };

                        _context.Alerts.Add(alert);
                        alertsGenerated++;
                    }
                }
            }

            if (alertsGenerated > 0)
            {
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Maintenance check completed. {alertsGenerated} alerts generated for overdue maintenance.";
            return RedirectToAction("Index", "Alert");
        }

        // Enhanced Equipment Management
        public async Task<IActionResult> Enhanced(
            string searchTerm = "",
            string sortBy = "Name",
            string sortDirection = "asc",
            int currentPage = 1,
            int pageSize = 20,
            string[]? statusFilter = null,
            string maintenanceStatus = "",
            string alertLevel = "",
            int? buildingId = null,
            int? equipmentTypeId = null)
        {
            var viewModel = new EquipmentManagementViewModel
            {
                PageTitle = "Equipment Management",
                PageDescription = "Manage and monitor all equipment in your facility",
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDirection = sortDirection,
                CurrentPage = currentPage,
                PageSize = pageSize,
                CanExport = true,
                CanBulkEdit = true,
                Breadcrumbs = new List<BreadcrumbItem>
                {
                    new BreadcrumbItem { Text = "Dashboard", Controller = "Dashboard", Action = "Index" },
                    new BreadcrumbItem { Text = "Equipment Management", IsActive = true }
                }
            };

            // Build query
            var query = _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .Include(e => e.MaintenanceLogs)
                .Include(e => e.Alerts)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(e => 
                    (e.EquipmentType != null && e.EquipmentType.EquipmentTypeName.Contains(searchTerm)) ||
                    (e.EquipmentModel != null && e.EquipmentModel.ModelName.Contains(searchTerm)) ||
                    (e.Building != null && e.Building.BuildingName.Contains(searchTerm)) ||
                    (e.Room != null && e.Room.RoomName.Contains(searchTerm)));
            }

            // Apply status filter
            if (statusFilter != null && statusFilter.Any())
            {
                var statuses = statusFilter.Select(s => Enum.Parse<EquipmentStatus>(s)).ToList();
                query = query.Where(e => statuses.Contains(e.Status));
            }

            // Apply building filter
            if (buildingId.HasValue)
            {
                query = query.Where(e => e.BuildingId == buildingId.Value);
            }

            // Apply equipment type filter
            if (equipmentTypeId.HasValue)
            {
                query = query.Where(e => e.EquipmentTypeId == equipmentTypeId.Value);
            }

            // Apply maintenance status filter
            if (!string.IsNullOrEmpty(maintenanceStatus))
            {
                switch (maintenanceStatus.ToLower())
                {
                    case "overdue":
                        // Equipment with overdue maintenance
                        query = query.Where(e => e.MaintenanceLogs != null && e.MaintenanceLogs.Any() && 
                            e.MaintenanceLogs.OrderByDescending(ml => ml.LogDate).First().LogDate < DateTime.Now.AddMonths(-6));
                        break;
                    case "due-soon":
                        // Equipment due for maintenance soon
                        query = query.Where(e => e.MaintenanceLogs != null && e.MaintenanceLogs.Any() && 
                            e.MaintenanceLogs.OrderByDescending(ml => ml.LogDate).First().LogDate < DateTime.Now.AddMonths(-3) &&
                            e.MaintenanceLogs.OrderByDescending(ml => ml.LogDate).First().LogDate >= DateTime.Now.AddMonths(-6));
                        break;
                    case "current":
                        // Equipment with recent maintenance
                        query = query.Where(e => e.MaintenanceLogs != null && e.MaintenanceLogs.Any() && 
                            e.MaintenanceLogs.OrderByDescending(ml => ml.LogDate).First().LogDate >= DateTime.Now.AddMonths(-3));
                        break;
                }
            }

            // Apply alert level filter
            if (!string.IsNullOrEmpty(alertLevel))
            {
                switch (alertLevel.ToLower())
                {
                    case "none":
                        query = query.Where(e => e.Alerts == null || !e.Alerts.Any(a => a.Status == AlertStatus.Open));
                        break;
                    case "low":
                        query = query.Where(e => e.Alerts != null && e.Alerts.Any(a => a.Status == AlertStatus.Open && a.Priority == AlertPriority.Low));
                        break;
                    case "medium":
                        query = query.Where(e => e.Alerts != null && e.Alerts.Any(a => a.Status == AlertStatus.Open && a.Priority == AlertPriority.Medium));
                        break;
                    case "high":
                        query = query.Where(e => e.Alerts != null && e.Alerts.Any(a => a.Status == AlertStatus.Open && a.Priority == AlertPriority.High));
                        break;
                }
            }

            // Get total count before pagination
            viewModel.TotalRecords = await query.CountAsync();

            // Apply sorting
            query = sortBy.ToLower() switch
            {
                "name" => sortDirection == "desc" 
                    ? query.OrderByDescending(e => e.EquipmentType != null ? e.EquipmentType.EquipmentTypeName : "")
                    : query.OrderBy(e => e.EquipmentType != null ? e.EquipmentType.EquipmentTypeName : ""),
                "type" => sortDirection == "desc" 
                    ? query.OrderByDescending(e => e.EquipmentType != null ? e.EquipmentType.EquipmentTypeName : "")
                    : query.OrderBy(e => e.EquipmentType != null ? e.EquipmentType.EquipmentTypeName : ""),
                "location" => sortDirection == "desc" 
                    ? query.OrderByDescending(e => e.Building != null ? e.Building.BuildingName : "")
                    : query.OrderBy(e => e.Building != null ? e.Building.BuildingName : ""),
                "status" => sortDirection == "desc" 
                    ? query.OrderByDescending(e => e.Status)
                    : query.OrderBy(e => e.Status),
                _ => query.OrderBy(e => e.EquipmentType != null ? e.EquipmentType.EquipmentTypeName : "")
            };

            // Apply pagination
            var equipment = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Convert to view models
            viewModel.Equipment = equipment.Select(e => new EquipmentItemViewModel
            {
                EquipmentId = e.EquipmentId,
                Name = e.EquipmentType?.EquipmentTypeName ?? "Unknown",
                Model = e.EquipmentModel?.ModelName ?? "Unknown",
                TypeName = e.EquipmentType?.EquipmentTypeName ?? "Unknown",
                Location = $"{e.Building?.BuildingName ?? "Unknown"} - {e.Room?.RoomName ?? "Unknown"}",
                Building = e.Building?.BuildingName ?? "Unknown",
                Room = e.Room?.RoomName ?? "Unknown",
                Status = e.Status,
                LastMaintenanceDate = e.MaintenanceLogs?.OrderByDescending(ml => ml.LogDate).FirstOrDefault()?.LogDate,
                NextMaintenanceDate = e.MaintenanceLogs?.OrderByDescending(ml => ml.LogDate).FirstOrDefault()?.LogDate.AddMonths(6),
                MaintenanceOverdueDays = CalculateOverdueDays(e.MaintenanceLogs?.OrderByDescending(ml => ml.LogDate).FirstOrDefault()?.LogDate),
                AlertCount = e.Alerts?.Count(a => a.Status == AlertStatus.Open) ?? 0,
                Notes = e.Notes ?? "",
                AvailableActions = BuildEquipmentActions(e.EquipmentId)
            }).ToList();

            // Build statistics
            await BuildEquipmentStatistics(viewModel);

            // Build filter options
            await BuildFilterOptions(viewModel);

            // Build bulk actions
            viewModel.BulkActions = BuildBulkActions();

            return View(viewModel);
        }

        private int CalculateOverdueDays(DateTime? lastMaintenanceDate)
        {
            if (!lastMaintenanceDate.HasValue) return 0;
            
            var expectedNextMaintenance = lastMaintenanceDate.Value.AddMonths(6);
            if (expectedNextMaintenance < DateTime.Now)
            {
                return (int)(DateTime.Now - expectedNextMaintenance).TotalDays;
            }
            return 0;
        }

        private List<QuickActionItem> BuildEquipmentActions(int equipmentId)
        {
            return new List<QuickActionItem>
            {
                new QuickActionItem
                {
                    Id = "view-history",
                    Title = "View History",
                    Description = "View maintenance history",
                    Icon = "bi-clock-history",
                    Color = "info",
                    Controller = "MaintenanceLog",
                    Action = "Index",
                    RouteValues = new Dictionary<string, string> { { "equipmentId", equipmentId.ToString() } }
                },
                new QuickActionItem
                {
                    Id = "schedule-maintenance",
                    Title = "Schedule Maintenance",
                    Description = "Schedule new maintenance",
                    Icon = "bi-calendar-plus",
                    Color = "warning",
                    Controller = "MaintenanceLog",
                    Action = "Create",
                    RouteValues = new Dictionary<string, string> { { "equipmentId", equipmentId.ToString() } }
                },
                new QuickActionItem
                {
                    Id = "view-alerts",
                    Title = "View Alerts",
                    Description = "View active alerts",
                    Icon = "bi-bell",
                    Color = "danger",
                    Controller = "Alert",
                    Action = "Index",
                    RouteValues = new Dictionary<string, string> { { "equipmentId", equipmentId.ToString() } }
                }
            };
        }

        private async Task BuildEquipmentStatistics(EquipmentManagementViewModel viewModel)
        {
            var allEquipment = await _context.Equipment
                .Include(e => e.MaintenanceLogs)
                .Include(e => e.Alerts)
                .ToListAsync();

            viewModel.Statistics = new EquipmentStatistics
            {
                TotalEquipment = allEquipment.Count,
                OperationalCount = allEquipment.Count(e => e.Status == EquipmentStatus.Active),
                MaintenanceCount = allEquipment.Count(e => e.Status == EquipmentStatus.Inactive),
                OutOfServiceCount = allEquipment.Count(e => e.Status == EquipmentStatus.Retired),
                OverdueMaintenanceCount = allEquipment.Count(e => 
                    e.MaintenanceLogs != null && e.MaintenanceLogs.Any() && 
                    e.MaintenanceLogs.OrderByDescending(ml => ml.LogDate).First().LogDate < DateTime.Now.AddMonths(-6)),
                AverageEfficiency = 92.5, // This would be calculated from actual performance data
                TotalMaintenanceCost = allEquipment
                    .SelectMany(e => e.MaintenanceLogs ?? new List<MaintenanceLog>())
                    .Sum(ml => ml.Cost),
                CriticalAlertsCount = allEquipment
                    .SelectMany(e => e.Alerts ?? new List<Alert>())
                    .Count(a => a.Status == AlertStatus.Open && a.Priority == AlertPriority.High)
            };
        }

        private async Task BuildFilterOptions(EquipmentManagementViewModel viewModel)
        {
            viewModel.FilterOptions = new EquipmentFilterOptions
            {
                Buildings = await _context.Buildings
                    .Select(b => new BuildingOption 
                    { 
                        Id = b.BuildingId, 
                        Name = b.BuildingName,
                        EquipmentCount = b.Equipments != null ? b.Equipments.Count : 0
                    })
                    .ToListAsync(),
                
                EquipmentTypes = await _context.EquipmentTypes
                    .Select(et => new EquipmentTypeOption 
                    { 
                        Id = et.EquipmentTypeId, 
                        Name = et.EquipmentTypeName,
                        EquipmentCount = et.Equipments != null ? et.Equipments.Count : 0
                    })
                    .ToListAsync(),
                
                Statuses = Enum.GetValues<EquipmentStatus>()
                    .Select(status => new StatusOption 
                    { 
                        Status = status, 
                        Name = status.ToString(),
                        Count = _context.Equipment.Count(e => e.Status == status)
                    })
                    .ToList(),
                
                MaintenanceStatuses = new List<MaintenanceStatusOption>
                {
                    new MaintenanceStatusOption { Status = "current", Display = "Up to Date", Count = 0 },
                    new MaintenanceStatusOption { Status = "due-soon", Display = "Due Soon", Count = 0 },
                    new MaintenanceStatusOption { Status = "overdue", Display = "Overdue", Count = 0 }
                }
            };
        }

        private List<EquipmentBulkAction> BuildBulkActions()
        {
            return new List<EquipmentBulkAction>
            {
                new EquipmentBulkAction
                {
                    Id = "activate",
                    Name = "Activate",
                    Icon = "bi-play-circle",
                    Color = "success",
                    RequiresConfirmation = false
                },
                new EquipmentBulkAction
                {
                    Id = "deactivate",
                    Name = "Deactivate",
                    Icon = "bi-pause-circle",
                    Color = "warning",
                    RequiresConfirmation = true,
                    ConfirmationMessage = "Are you sure you want to deactivate the selected equipment?"
                },
                new EquipmentBulkAction
                {
                    Id = "schedule-maintenance",
                    Name = "Schedule Maintenance",
                    Icon = "bi-calendar-plus",
                    Color = "info",
                    RequiresConfirmation = false
                },
                new EquipmentBulkAction
                {
                    Id = "delete",
                    Name = "Delete",
                    Icon = "bi-trash",
                    Color = "danger",
                    RequiresConfirmation = true,
                    ConfirmationMessage = "Are you sure you want to delete the selected equipment? This action cannot be undone."
                }
            };
        }

        // Bulk Actions API
        [HttpPost]
        public async Task<IActionResult> BulkAction(string actionId, [FromBody] BulkActionRequest request)
        {
            try
            {
                var equipmentIds = request.SelectedIds ?? new List<int>();
                var equipment = await _context.Equipment
                    .Where(e => equipmentIds.Contains(e.EquipmentId))
                    .ToListAsync();

                if (!equipment.Any())
                {
                    return Json(new { success = false, message = "No equipment found." });
                }

                switch (actionId.ToLower())
                {
                    case "activate":
                        foreach (var item in equipment)
                            item.Status = EquipmentStatus.Active;
                        break;
                    
                    case "deactivate":
                        foreach (var item in equipment)
                            item.Status = EquipmentStatus.Inactive;
                        break;
                    
                    case "delete":
                        _context.Equipment.RemoveRange(equipment);
                        break;
                    
                    case "schedule-maintenance":
                        // Create maintenance tasks for selected equipment
                        foreach (var item in equipment)
                        {
                            var maintenanceTask = await _schedulingService.CreateMaintenanceTaskAsync(
                                item.EquipmentId,
                                "Scheduled maintenance from bulk action",
                                TaskPriority.Medium
                            );
                            _context.MaintenanceTasks.Add(maintenanceTask);
                        }
                        break;
                    
                    default:
                        return Json(new { success = false, message = "Unknown action." });
                }

                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = $"Successfully processed {equipment.Count} equipment items." 
                });
            }
            catch (Exception)
            {
                return Json(new { 
                    success = false, 
                    message = "An error occurred while processing the request." 
                });
            }
        }

        // Export functionality
        public async Task<IActionResult> Export(string format = "csv", string searchTerm = "", string[]? statusFilter = null)
        {
            var query = _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .AsQueryable();

            // Apply filters (same as in Enhanced action)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(e => 
                    (e.EquipmentType != null && e.EquipmentType.EquipmentTypeName.Contains(searchTerm)) ||
                    (e.EquipmentModel != null && e.EquipmentModel.ModelName.Contains(searchTerm)));
            }

            if (statusFilter != null && statusFilter.Any())
            {
                var statuses = statusFilter.Select(s => Enum.Parse<EquipmentStatus>(s)).ToList();
                query = query.Where(e => statuses.Contains(e.Status));
            }

            var equipment = await query.ToListAsync();

            switch (format.ToLower())
            {
                case "csv":
                    return ExportToCsv(equipment);
                case "excel":
                    return ExportToExcel(equipment);
                case "pdf":
                    return ExportToPdf(equipment);
                default:
                    return ExportToCsv(equipment);
            }
        }

        private IActionResult ExportToCsv(List<Equipment> equipment)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Equipment Type,Model,Building,Room,Status,Installation Date");

            foreach (var item in equipment)
            {
                csv.AppendLine($"\"{item.EquipmentType?.EquipmentTypeName ?? "N/A"}\",\"{item.EquipmentModel?.ModelName ?? "N/A"}\",\"{item.Building?.BuildingName ?? "N/A"}\",\"{item.Room?.RoomName ?? "N/A"}\",\"{item.Status}\",\"{item.InstallationDate?.ToString("yyyy-MM-dd") ?? "N/A"}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"equipment_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        private IActionResult ExportToExcel(List<Equipment> equipment)
        {
            // Implementation for Excel export would go here
            // For now, return CSV
            return ExportToCsv(equipment);
        }

        private IActionResult ExportToPdf(List<Equipment> equipment)
        {
            // Implementation for PDF export would go here
            // For now, return CSV
            return ExportToCsv(equipment);
        }

        // TEST ACTION: Create test equipment for delete testing
        // TODO: Remove this action in production
        [HttpGet]
        public async Task<IActionResult> CreateTestEquipment()
        {
            try
            {
                // Get first available equipment type, building, and room
                var equipmentType = await _context.EquipmentTypes.FirstAsync();
                var building = await _context.Buildings.FirstAsync();
                var room = await _context.Rooms.FirstAsync();

                // Create or get test equipment model
                var testModel = await _context.EquipmentModels
                    .FirstOrDefaultAsync(m => m.ModelName == "TEST-DELETE-MODEL");
                
                if (testModel == null)
                {
                    testModel = new EquipmentModel
                    {
                        ModelName = "TEST-DELETE-MODEL",
                        EquipmentTypeId = equipmentType.EquipmentTypeId
                    };
                    _context.EquipmentModels.Add(testModel);
                    await _context.SaveChangesAsync();
                }

                // Create test equipment
                var testEquipment = new Equipment
                {
                    EquipmentTypeId = equipmentType.EquipmentTypeId,
                    EquipmentModelId = testModel.EquipmentModelId,
                    BuildingId = building.BuildingId,
                    RoomId = room.RoomId,
                    Status = EquipmentStatus.Active,
                    InstallationDate = DateTime.Now.AddDays(-30),
                    ExpectedLifespanMonths = 60,
                    Notes = "TEST EQUIPMENT - Safe to delete"
                };

                _context.Equipment.Add(testEquipment);
                await _context.SaveChangesAsync();

                // Create test alerts
                var alert1 = new Alert
                {
                    EquipmentId = testEquipment.EquipmentId,
                    Title = "Test Alert 1",
                    Description = "Test alert for delete functionality - will be deleted with equipment",
                    Priority = AlertPriority.Medium,
                    Status = AlertStatus.Open,
                    CreatedDate = DateTime.Now.AddDays(-5)
                };

                var alert2 = new Alert
                {
                    EquipmentId = testEquipment.EquipmentId,
                    Title = "Test Alert 2",
                    Description = "Another test alert - will be deleted with equipment",
                    Priority = AlertPriority.High,
                    Status = AlertStatus.InProgress,
                    CreatedDate = DateTime.Now.AddDays(-3)
                };

                _context.Alerts.AddRange(alert1, alert2);

                // Create test maintenance log
                var maintenanceLog = new MaintenanceLog
                {
                    EquipmentId = testEquipment.EquipmentId,
                    LogDate = DateTime.Now.AddDays(-10),
                    MaintenanceType = MaintenanceType.Preventive,
                    Description = "Test maintenance log - will be deleted with equipment",
                    Cost = 150.00m,
                    Technician = "Test Technician",
                    Status = MaintenanceStatus.Completed
                };

                _context.MaintenanceLogs.Add(maintenanceLog);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Test equipment created successfully! Equipment ID: {testEquipment.EquipmentId} with 2 alerts and 1 maintenance log. You can now test the delete functionality.";
                
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error creating test equipment: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Equipment/Documents/5
        public async Task<IActionResult> Documents(int id)
        {
            var equipment = await _context.Equipment
                .Include(e => e.EquipmentModel)
                .Include(e => e.EquipmentType)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .FirstOrDefaultAsync(e => e.EquipmentId == id);

            if (equipment == null)
            {
                return NotFound();
            }

            var documents = await _context.ManufacturerDocuments
                .Include(d => d.MaintenanceRecommendations)
                .Where(d => d.EquipmentModelId == equipment.EquipmentModelId)
                .OrderByDescending(d => d.UploadDate)
                .ToListAsync();

            ViewBag.EquipmentId = id;
            ViewBag.EquipmentName = $"{equipment.EquipmentType?.EquipmentTypeName} - {equipment.EquipmentModel?.ModelName}";

            return View(documents);
        }

        // POST: Equipment/UploadDocument
        [HttpPost]
        public async Task<IActionResult> UploadDocument(int equipmentId, IFormFile documentFile, string documentType, bool processAutomatically = true)
        {
            try
            {
                if (documentFile == null || documentFile.Length == 0)
                {
                    return Json(new { success = false, message = "No file selected" });
                }

                // Validate file
                var allowedTypes = new[] { "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "text/plain" };
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt" };
                var extension = Path.GetExtension(documentFile.FileName).ToLower();

                if (!allowedTypes.Contains(documentFile.ContentType) || !allowedExtensions.Contains(extension))
                {
                    return Json(new { success = false, message = "Invalid file type. Only PDF, Word, and text files are allowed." });
                }

                if (documentFile.Length > 10 * 1024 * 1024) // 10MB
                {
                    return Json(new { success = false, message = "File size exceeds 10MB limit." });
                }

                // Check if equipment exists
                var equipment = await _context.Equipment.FindAsync(equipmentId);
                if (equipment == null)
                {
                    return Json(new { success = false, message = "Equipment not found." });
                }

                // Save document
                var success = await _documentProcessingService.SaveDocumentAsync(documentFile, equipmentId, documentType);
                
                if (success)
                {
                    if (processAutomatically)
                    {
                        // Get the equipment and its model to find the document we just saved
                        var uploadedEquipment = await _context.Equipment.FindAsync(equipmentId);
                        if (uploadedEquipment?.EquipmentModelId != null)
                        {
                            var document = await _context.ManufacturerDocuments
                                .Where(d => d.EquipmentModelId == uploadedEquipment.EquipmentModelId && d.FileName == documentFile.FileName)
                                .OrderByDescending(d => d.UploadDate)
                                .FirstOrDefaultAsync();

                            if (document != null)
                            {
                                // Process document in background
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await _documentProcessingService.ProcessDocumentAsync(document.DocumentId);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, $"Error processing document {document.DocumentId} in background");
                                    }
                                });
                            }
                        }
                    }

                    return Json(new { success = true, message = "Document uploaded successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to save document" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return Json(new { success = false, message = "An error occurred while uploading the document" });
            }
        }

        // POST: Equipment/ProcessDocument
        [HttpPost]
        public async Task<IActionResult> ProcessDocument(int documentId)
        {
            try
            {
                var document = await _context.ManufacturerDocuments.FindAsync(documentId);
                if (document == null)
                {
                    return Json(new { success = false, message = "Document not found" });
                }

                await _documentProcessingService.ProcessDocumentAsync(documentId);
                return Json(new { success = true, message = "Document processed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document");
                return Json(new { success = false, message = "An error occurred while processing the document" });
            }
        }

        // GET: Equipment/GetDocumentRecommendations
        public async Task<IActionResult> GetDocumentRecommendations(int documentId)
        {
            try
            {
                var document = await _context.ManufacturerDocuments
                    .Include(d => d.MaintenanceRecommendations)
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                {
                    return PartialView("_DocumentRecommendations", new List<MaintenanceRecommendation>());
                }

                return PartialView("_DocumentRecommendations", document.MaintenanceRecommendations ?? new List<MaintenanceRecommendation>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document recommendations");
                return PartialView("_DocumentRecommendations", new List<MaintenanceRecommendation>());
            }
        }

        // POST: Equipment/DeleteDocument
        [HttpPost]
        public async Task<IActionResult> DeleteDocument(int documentId)
        {
            try
            {
                var document = await _context.ManufacturerDocuments
                    .Include(d => d.MaintenanceRecommendations)
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                {
                    return Json(new { success = false, message = "Document not found" });
                }

                // Delete associated recommendations first
                if (document.MaintenanceRecommendations?.Any() == true)
                {
                    _context.MaintenanceRecommendations.RemoveRange(document.MaintenanceRecommendations);
                }

                // Delete the physical file
                if (System.IO.File.Exists(document.FilePath))
                {
                    System.IO.File.Delete(document.FilePath);
                }

                // Delete the database record
                _context.ManufacturerDocuments.Remove(document);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Document deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document");
                return Json(new { success = false, message = "An error occurred while deleting the document" });
            }
        }

        // POST: Equipment/RemoveDocument (alias for DeleteDocument)
        [HttpPost]
        public async Task<IActionResult> RemoveDocument(int documentId)
        {
            return await DeleteDocument(documentId);
        }

        // GET: Equipment/MaintenanceRecommendations/5
        public async Task<IActionResult> MaintenanceRecommendations(int id)
        {
            var equipment = await _context.Equipment
                .Include(e => e.EquipmentModel)
                .Include(e => e.EquipmentType)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .FirstOrDefaultAsync(e => e.EquipmentId == id);

            if (equipment == null)
            {
                return NotFound();
            }

            var recommendations = await _context.MaintenanceRecommendations
                .Include(r => r.Document)
                .Where(r => r.EquipmentId == id)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            ViewBag.Equipment = equipment;
            ViewBag.EquipmentName = $"{equipment.EquipmentType?.EquipmentTypeName} - {equipment.EquipmentModel?.ModelName}";

            return View(recommendations);
        }

        // GET: Equipment/CreateWithDocs
        public async Task<IActionResult> CreateWithDocs()
        {
            ViewData["EquipmentTypeId"] = new SelectList(await _context.EquipmentTypes.ToListAsync(), "EquipmentTypeId", "EquipmentTypeName");
            ViewData["EquipmentModelId"] = new SelectList(await _context.EquipmentModels.ToListAsync(), "EquipmentModelId", "ModelName");
            ViewData["BuildingId"] = new SelectList(await _context.Buildings.ToListAsync(), "BuildingId", "BuildingName");
            ViewData["RoomId"] = new SelectList(await _context.Rooms.ToListAsync(), "RoomId", "RoomName");
            
            // Add document types for the view
            ViewBag.DocumentTypes = new string[] { "Manual", "Specification", "Warranty", "Installation Guide", "Maintenance Guide", "Safety Instructions" };
            
            return View();
        }

        // POST: Equipment/CreateWithDocs
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWithDocs([Bind("EquipmentId,EquipmentTypeId,EquipmentModelName,BuildingId,RoomId,InstallationDate,ExpectedLifespanMonths,Status,Notes")] Equipment equipment, IFormFileCollection ManufacturerDocuments)
        {
            // Enhanced validation with user-friendly messages
            if (string.IsNullOrWhiteSpace(equipment.EquipmentModelName))
            {
                ModelState.AddModelError("EquipmentModelName", "Equipment model name is required. Please enter a descriptive name for this equipment.");
            }

            if (equipment.EquipmentTypeId <= 0)
            {
                ModelState.AddModelError("EquipmentTypeId", "Please select a valid equipment type from the dropdown menu.");
            }

            if (equipment.BuildingId <= 0)
            {
                ModelState.AddModelError("BuildingId", "Please select the building where this equipment is located.");
            }

            if (equipment.RoomId <= 0)
            {
                ModelState.AddModelError("RoomId", "Please select the room where this equipment is located.");
            }

            if (equipment.ExpectedLifespanMonths <= 0)
            {
                ModelState.AddModelError("ExpectedLifespanMonths", "Expected lifespan must be greater than 0 months.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // First, find or create the equipment model
                    var existingModel = await _context.EquipmentModels
                        .FirstOrDefaultAsync(em => em.ModelName.ToLower() == equipment.EquipmentModelName!.ToLower());

                    if (existingModel == null)
                    {
                        // Create new equipment model
                        existingModel = new EquipmentModel
                        {
                            ModelName = equipment.EquipmentModelName!,
                            EquipmentTypeId = equipment.EquipmentTypeId
                        };
                        _context.EquipmentModels.Add(existingModel);
                        await _context.SaveChangesAsync();
                    }

                    // Set the equipment model ID
                    equipment.EquipmentModelId = existingModel.EquipmentModelId;

                    // Add equipment to context
                    _context.Add(equipment);
                    await _context.SaveChangesAsync();

                    // Handle document uploads if any
                    if (ManufacturerDocuments != null && ManufacturerDocuments.Count > 0)
                    {
                        await ProcessDocumentUploads(ManufacturerDocuments, existingModel.EquipmentModelId, equipment.EquipmentId);
                    }

                    TempData["SuccessMessage"] = $"Equipment '{equipment.EquipmentModelName}' has been created successfully with {ManufacturerDocuments?.Count ?? 0} documents.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred while creating the equipment: {ex.Message}");
                }
            }

            // If we got here, something failed, redisplay form
            ViewData["EquipmentTypeId"] = new SelectList(await _context.EquipmentTypes.ToListAsync(), "EquipmentTypeId", "EquipmentTypeName", equipment.EquipmentTypeId);
            ViewData["EquipmentModelId"] = new SelectList(await _context.EquipmentModels.ToListAsync(), "EquipmentModelId", "ModelName");
            ViewData["BuildingId"] = new SelectList(await _context.Buildings.ToListAsync(), "BuildingId", "BuildingName", equipment.BuildingId);
            ViewData["RoomId"] = new SelectList(await _context.Rooms.Where(r => r.BuildingId == equipment.BuildingId).ToListAsync(), "RoomId", "RoomName", equipment.RoomId);
            ViewBag.DocumentTypes = new string[] { "Manual", "Specification", "Warranty", "Installation Guide", "Maintenance Guide", "Safety Instructions" };
            
            return View(equipment);
        }

        private async Task ProcessDocumentUploads(IFormFileCollection files, int equipmentModelId, int uploadedByEquipmentId)
        {
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    try
                    {
                        // Create uploads directory if it doesn't exist
                        var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "documents");
                        Directory.CreateDirectory(uploadsDir);

                        // Generate unique filename
                        var fileName = Path.GetFileNameWithoutExtension(file.FileName);
                        var extension = Path.GetExtension(file.FileName);
                        var uniqueFileName = $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                        var filePath = Path.Combine(uploadsDir, uniqueFileName);

                        // Save file
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Create document record
                        var document = new ManufacturerDocument
                        {
                            FileName = file.FileName,
                            FilePath = $"/uploads/documents/{uniqueFileName}",
                            FileSize = file.Length,
                            DocumentType = "Manual Upload",
                            UploadDate = DateTime.Now,
                            EquipmentModelId = equipmentModelId,
                            UploadedByEquipmentId = uploadedByEquipmentId
                        };

                        _context.ManufacturerDocuments.Add(document);
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't fail the entire operation
                        Console.WriteLine($"Error uploading document {file.FileName}: {ex.Message}");
                    }
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
