using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;

namespace FEENALOoFINALE.Controllers
{
    [Authorize]
    public class AlertController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly MaintenanceSchedulingService _schedulingService;

        public AlertController(ApplicationDbContext context, UserManager<User> userManager, MaintenanceSchedulingService schedulingService)
        {
            _context = context;
            _userManager = userManager;
            _schedulingService = schedulingService;
        }

        // GET: Alert
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            
            // Auto-assign unassigned alerts to current user
            var unassignedAlerts = await _context.Alerts
                .Where(a => a.AssignedToUserId == null && a.Status == AlertStatus.Open)
                .ToListAsync();
                
            if (currentUser != null && unassignedAlerts.Any())
            {
                foreach (var alert in unassignedAlerts)
                {
                    alert.AssignedToUserId = currentUser.Id;
                }
                await _context.SaveChangesAsync();
            }

            // Get alerts excluding resolved ones (unless we want to show them)
            var alerts = await _context.Alerts
                .Include(a => a.Equipment!)
                    .ThenInclude(e => e.EquipmentType)
                .Include(a => a.Equipment!)
                    .ThenInclude(e => e.EquipmentModel)
                .Include(a => a.AssignedTo)
                .Where(a => a.Status != AlertStatus.Resolved) // Hide resolved alerts
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            // Get alert IDs that have completed maintenance logs
            var completedMaintenanceAlertIds = await _context.MaintenanceLogs
                .Where(ml => ml.AlertId != null && ml.Status == MaintenanceStatus.Completed)
                .Select(ml => ml.AlertId!.Value)
                .ToListAsync();

            ViewBag.CompletedMaintenanceTasks = completedMaintenanceAlertIds;

            return View(alerts);
        }

        // GET: Alert/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var alert = await _context.Alerts
                .Include(a => a.Equipment) // Changed from a.EquipmentName
                .Include(a => a.AssignedTo)
                .FirstOrDefaultAsync(m => m.AlertId == id);

            if (alert == null)
            {
                return NotFound();
            }

            return View(alert);
        }

        // GET: Alert/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var alert = await _context.Alerts
                .Include(a => a.Equipment)
                    .ThenInclude(e => e!.EquipmentType)
                .Include(a => a.Equipment)
                    .ThenInclude(e => e!.EquipmentModel)
                .Include(a => a.AssignedTo)
                .FirstOrDefaultAsync(m => m.AlertId == id);

            if (alert == null)
            {
                return NotFound();
            }

            // Prepare dropdown lists
            ViewBag.EquipmentList = new SelectList(
                await _context.Equipment
                    .Include(e => e.EquipmentType)
                    .Include(e => e.EquipmentModel)
                    .Select(e => new { 
                        Value = e.EquipmentId, 
                        Text = $"{e.EquipmentModel!.ModelName} ({e.EquipmentType!.EquipmentTypeName})" 
                    })
                    .ToListAsync(), 
                "Value", "Text", alert.EquipmentId);

            ViewBag.UserList = new SelectList(
                await _context.Users
                    .Select(u => new { 
                        Value = u.Id, 
                        Text = $"{u.FirstName} {u.LastName}" 
                    })
                    .ToListAsync(), 
                "Value", "Text", alert.AssignedToUserId);

            return View(alert);
        }

        // POST: Alert/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AlertId,EquipmentId,InventoryItemId,Title,Description,Priority,Status,CreatedDate,AssignedToUserId")] Alert alert)
        {
            if (id != alert.AlertId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(alert);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Alert updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AlertExists(alert.AlertId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Repopulate dropdown lists if validation fails
            ViewBag.EquipmentList = new SelectList(
                await _context.Equipment
                    .Include(e => e.EquipmentType)
                    .Include(e => e.EquipmentModel)
                    .Select(e => new { 
                        Value = e.EquipmentId, 
                        Text = $"{e.EquipmentModel!.ModelName} ({e.EquipmentType!.EquipmentTypeName})" 
                    })
                    .ToListAsync(), 
                "Value", "Text", alert.EquipmentId);

            ViewBag.UserList = new SelectList(
                await _context.Users
                    .Select(u => new { 
                        Value = u.Id, 
                        Text = $"{u.FirstName} {u.LastName}" 
                    })
                    .ToListAsync(), 
                "Value", "Text", alert.AssignedToUserId);

            return View(alert);
        }

        // GET: Alert/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var alert = await _context.Alerts
                .Include(a => a.Equipment) // Changed from a.EquipmentName
                .Include(a => a.AssignedTo)
                .FirstOrDefaultAsync(m => m.AlertId == id);
            if (alert == null)
            {
                return NotFound();
            }

            return View(alert);
        }

        // POST: Alert/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var alert = await _context.Alerts.FindAsync(id);
            if (alert != null)
            {
                _context.Alerts.Remove(alert);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Resolve(int id)
        {
            var alert = await _context.Alerts.FindAsync(id);
            if (alert == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            
            // Auto-assign alert to current user if not already assigned or reassign if assigned to someone else
            if (alert.AssignedToUserId == null && currentUser != null)
            {
                alert.AssignedToUserId = currentUser.Id;
                alert.Status = AlertStatus.InProgress;
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Alert has been assigned to you and scheduled for maintenance.";
            }
            else if (alert.AssignedToUserId != null && currentUser != null && alert.AssignedToUserId != currentUser.Id)
            {
                // If alert is assigned to someone else, reassign to current user
                alert.AssignedToUserId = currentUser.Id;
                alert.Status = AlertStatus.InProgress;
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Alert has been reassigned to you and scheduled for maintenance.";
            }
            else if (currentUser != null)
            {
                TempData["SuccessMessage"] = "Maintenance has been scheduled for this alert.";
            }
            
            // Create a properly scheduled and assigned MaintenanceTask from the alert
            var priority = alert.Priority == AlertPriority.High ? TaskPriority.Critical :
                          alert.Priority == AlertPriority.Medium ? TaskPriority.High : TaskPriority.Medium;

            var maintenanceTask = await _schedulingService.CreateMaintenanceTaskAsync(
                alert.EquipmentId ?? 0,
                $"Maintenance required: {alert.Title}",
                priority,
                alert.AlertId
            );

            _context.MaintenanceTasks.Add(maintenanceTask);
            await _context.SaveChangesAsync();

            // Redirect to MaintenanceLog/Create, passing the EquipmentId and AlertId
            return RedirectToAction("Create", "MaintenanceLog", new { 
                equipmentId = alert.EquipmentId, 
                alertId = id,
                taskId = maintenanceTask.TaskId
            });
        }

        [HttpPost]
        public async Task<IActionResult> MarkResolved(int id)
        {
            var alert = await _context.Alerts.FindAsync(id);
            if (alert == null)
            {
                return NotFound();
            }

            // Check if there's a completed maintenance log for this alert
            var hasCompletedMaintenance = await _context.MaintenanceLogs
                .AnyAsync(ml => ml.AlertId == id && ml.Status == MaintenanceStatus.Completed);

            if (hasCompletedMaintenance)
            {
                // Mark the alert as resolved
                alert.Status = AlertStatus.Resolved;
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Alert has been resolved successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Cannot resolve alert. No completed maintenance found for this alert.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> MarkInProgress(int id)
        {
            var alert = await _context.Alerts
                .Include(a => a.Equipment)
                .FirstOrDefaultAsync(a => a.AlertId == id);
            if (alert == null)
                return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            
            // Auto-assign alert to current user if not already assigned
            if (alert.AssignedToUserId == null && currentUser != null)
            {
                alert.AssignedToUserId = currentUser.Id;
                TempData["SuccessMessage"] = "Alert has been assigned to you and marked as in progress.";
            }
            else if (alert.AssignedToUserId != null && currentUser != null && alert.AssignedToUserId != currentUser.Id)
            {
                // If alert is assigned to someone else, reassign to current user
                alert.AssignedToUserId = currentUser.Id;
                TempData["SuccessMessage"] = "Alert has been reassigned to you and marked as in progress.";
            }
            else
            {
                TempData["SuccessMessage"] = "Alert has been marked as in progress.";
            }

            alert.Status = AlertStatus.InProgress;
            
            // Check if there's already a maintenance log for this alert to avoid duplicates
            var existingLog = await _context.MaintenanceLogs
                .FirstOrDefaultAsync(ml => ml.AlertId == alert.AlertId && ml.Status == MaintenanceStatus.Pending);
            
            if (existingLog == null && alert.Equipment != null)
            {
                // Create a maintenance log entry when starting work on an alert
                var maintenanceLog = new MaintenanceLog
                {
                    EquipmentId = alert.Equipment.EquipmentId,
                    AlertId = alert.AlertId,
                    LogDate = DateTime.Now,
                    MaintenanceType = MaintenanceType.Corrective, // Since this is from an alert
                    Description = $"Work started on alert: {alert.Title}",
                    Technician = currentUser?.UserName ?? "Unknown",
                    Status = MaintenanceStatus.Pending, // Starts as pending until completed
                    Cost = 0, // Will be updated when work is completed
                    DowntimeHours = 0 // Will be updated when work is completed
                };
                
                _context.MaintenanceLogs.Add(maintenanceLog);
                TempData["InfoMessage"] = "A maintenance log has been created for you to complete when work is finished.";
            }
            
            _context.Update(alert);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Temporary method to clear auto-generated alerts
        [HttpPost]
        public async Task<IActionResult> ClearAutoGeneratedAlerts()
        {
            try
            {
                // Remove all auto-generated alerts (those with generic titles/descriptions indicating auto-generation)
                var autoGeneratedAlerts = await _context.Alerts
                    .Where(a => (a.Title != null && (a.Title.Contains("overdue maintenance") || 
                               a.Title.Contains("Equipment Status Alert"))) ||
                               (a.Description != null && (a.Description.Contains("automatically generated") ||
                               a.Description.Contains("has been inactive") ||
                               a.Description.Contains("overdue for maintenance"))))
                    .ToListAsync();

                // Remove auto-generated maintenance tasks linked to these alerts
                var linkedTaskIds = autoGeneratedAlerts.Select(a => a.AlertId).ToList();
                var autoGeneratedTasks = await _context.MaintenanceTasks
                    .Where(mt => mt.CreatedFromAlertId != null && 
                                linkedTaskIds.Contains(mt.CreatedFromAlertId.Value))
                    .ToListAsync();

                // Also remove unlinked auto-generated tasks (those with generic descriptions)
                var otherAutoTasks = await _context.MaintenanceTasks
                    .Where(mt => mt.Description != null && (mt.Description.Contains("Address Alert:") ||
                               mt.Description.Contains("Perform overdue maintenance") ||
                               mt.Description.Contains("Investigate equipment status")))
                    .ToListAsync();

                _context.MaintenanceTasks.RemoveRange(autoGeneratedTasks);
                _context.MaintenanceTasks.RemoveRange(otherAutoTasks);
                _context.Alerts.RemoveRange(autoGeneratedAlerts);
                await _context.SaveChangesAsync();

                var totalTasks = autoGeneratedTasks.Count + otherAutoTasks.Count;
                TempData["SuccessMessage"] = $"Cleared {autoGeneratedAlerts.Count} auto-generated alerts and {totalTasks} auto-generated tasks.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error clearing alerts: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Debug method to see all alerts and their content
        [HttpGet]
        public async Task<IActionResult> DebugAlerts()
        {
            var alerts = await _context.Alerts.ToListAsync();
            var alertInfo = alerts.Select(a => new 
            {
                Id = a.AlertId,
                Title = a.Title ?? "NULL",
                Description = a.Description ?? "NULL",
                CreatedDate = a.CreatedDate
            }).ToList();

            ViewBag.AlertInfo = alertInfo;
            return View();
        }

        // Enhanced clear method with broader criteria (fixed for EF Core LINQ)
        [HttpPost]
        public async Task<IActionResult> ClearAllAutoGeneratedAlerts()
        {
            try
            {
                // Remove all auto-generated alerts (case-insensitive, using ToLower for EF Core compatibility)
                var autoGeneratedAlerts = await _context.Alerts
                    .Where(a =>
                        (!string.IsNullOrEmpty(a.Title) && (
                            a.Title.ToLower().Contains("overdue maintenance") ||
                            a.Title.ToLower().Contains("equipment status alert") ||
                            a.Title.ToLower().Contains("equipment became inactive") ||
                            a.Title.ToLower().Contains("equipment retired") ||
                            a.Title.ToLower().Contains("equipment restored") ||
                            a.Title.ToLower().Contains("maintenance overdue")
                        )) ||
                        (!string.IsNullOrEmpty(a.Description) && (
                            a.Description.ToLower().Contains("automatically generated") ||
                            a.Description.ToLower().Contains("has been inactive") ||
                            a.Description.ToLower().Contains("overdue for maintenance") ||
                            a.Description.ToLower().Contains("changed from active to inactive") ||
                            a.Description.ToLower().Contains("has not received maintenance") ||
                            a.Description.ToLower().Contains("recommended maintenance interval")
                        )) ||
                        (a.CreatedDate.Date == DateTime.Today && string.IsNullOrEmpty(a.AssignedToUserId))
                    )
                    .ToListAsync();

                // Remove auto-generated maintenance tasks linked to these alerts
                var linkedTaskIds = autoGeneratedAlerts.Select(a => a.AlertId).ToList();
                var autoGeneratedTasks = await _context.MaintenanceTasks
                    .Where(mt => mt.CreatedFromAlertId != null &&
                                linkedTaskIds.Contains(mt.CreatedFromAlertId.Value))
                    .ToListAsync();

                // Also remove unlinked auto-generated tasks (those with generic descriptions)
                var otherAutoTasks = await _context.MaintenanceTasks
                    .Where(mt => !string.IsNullOrEmpty(mt.Description) && (
                        mt.Description.ToLower().Contains("address alert:") ||
                        mt.Description.ToLower().Contains("perform overdue maintenance") ||
                        mt.Description.ToLower().Contains("investigate equipment status")
                    ))
                    .ToListAsync();

                if (autoGeneratedTasks.Any())
                    _context.MaintenanceTasks.RemoveRange(autoGeneratedTasks);
                if (otherAutoTasks.Any())
                    _context.MaintenanceTasks.RemoveRange(otherAutoTasks);
                if (autoGeneratedAlerts.Any())
                    _context.Alerts.RemoveRange(autoGeneratedAlerts);

                await _context.SaveChangesAsync();

                var totalTasks = autoGeneratedTasks.Count + otherAutoTasks.Count;
                TempData["SuccessMessage"] = $"Cleared {autoGeneratedAlerts.Count} auto-generated alerts and {totalTasks} auto-generated tasks.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error clearing alerts: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        private bool AlertExists(int id)
        {
            return _context.Alerts.Any(e => e.AlertId == id);
        }

        // Nuclear option - clear ALL alerts and maintenance tasks (for testing)
        [HttpPost]
        public async Task<IActionResult> ClearAllAlerts()
        {
            try
            {
                // Remove ALL maintenance tasks first (to avoid foreign key issues)
                var allTasks = await _context.MaintenanceTasks.ToListAsync();
                if (allTasks.Any())
                {
                    _context.MaintenanceTasks.RemoveRange(allTasks);
                }

                // Remove ALL alerts
                var allAlerts = await _context.Alerts.ToListAsync();
                if (allAlerts.Any())
                {
                    _context.Alerts.RemoveRange(allAlerts);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cleared ALL {allAlerts.Count} alerts and {allTasks.Count} maintenance tasks from the database.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error clearing all alerts: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Test method to show what would be cleared without actually clearing
        [HttpGet]
        public async Task<IActionResult> TestClearCriteria()
        {
            try
            {
                // Get ALL alerts to examine them
                var allAlerts = await _context.Alerts.ToListAsync();
                
                // Identify auto-generated alerts by multiple criteria
                var autoGeneratedAlerts = allAlerts.Where(a => 
                    // Title-based detection
                    (!string.IsNullOrEmpty(a.Title) && (
                        a.Title.Contains("overdue maintenance", StringComparison.OrdinalIgnoreCase) ||
                        a.Title.Contains("Equipment Status Alert", StringComparison.OrdinalIgnoreCase) ||
                        a.Title.Contains("Equipment Became Inactive", StringComparison.OrdinalIgnoreCase) ||
                        a.Title.Contains("Equipment Retired", StringComparison.OrdinalIgnoreCase) ||
                        a.Title.Contains("Equipment Restored", StringComparison.OrdinalIgnoreCase) ||
                        a.Title.Contains("Maintenance Overdue", StringComparison.OrdinalIgnoreCase)
                    )) ||
                    // Description-based detection
                    (!string.IsNullOrEmpty(a.Description) && (
                        a.Description.Contains("automatically generated", StringComparison.OrdinalIgnoreCase) ||
                        a.Description.Contains("has been inactive", StringComparison.OrdinalIgnoreCase) ||
                        a.Description.Contains("overdue for maintenance", StringComparison.OrdinalIgnoreCase) ||
                        a.Description.Contains("changed from Active to Inactive", StringComparison.OrdinalIgnoreCase) ||
                        a.Description.Contains("has not received maintenance", StringComparison.OrdinalIgnoreCase) ||
                        a.Description.Contains("Recommended maintenance interval", StringComparison.OrdinalIgnoreCase)
                    )) ||
                    // Date-based detection (created today by automated processes)
                    (a.CreatedDate.Date == DateTime.Today && string.IsNullOrEmpty(a.AssignedToUserId))
                ).ToList();

                var testResults = allAlerts.Select(a => new {
                    Id = a.AlertId,
                    Title = a.Title ?? "NULL",
                    Description = a.Description ?? "NULL",
                    CreatedDate = a.CreatedDate,
                    AssignedToUserId = a.AssignedToUserId ?? "NULL",
                    WouldBeCleared = autoGeneratedAlerts.Any(auto => auto.AlertId == a.AlertId)
                }).ToList();

                ViewBag.TestResults = testResults;
                ViewBag.TotalAlerts = allAlerts.Count;
                ViewBag.WouldClear = autoGeneratedAlerts.Count;
                
                return View();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error testing clear criteria: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Raw SQL cleanup method for stubborn auto-generated data
        [HttpPost]
        public async Task<IActionResult> SqlCleanupAutoGenerated()
        {
            try
            {
                // Count before cleanup
                var alertCountBefore = await _context.Alerts.CountAsync();
                var taskCountBefore = await _context.MaintenanceTasks.CountAsync();

                // Use raw SQL to delete auto-generated maintenance tasks first
                await _context.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM MaintenanceTasks 
                    WHERE Description LIKE '%Address Alert:%' 
                       OR Description LIKE '%Perform overdue maintenance%'
                       OR Description LIKE '%Investigate equipment status%'
                       OR CreatedFromAlertId IS NOT NULL
                ");

                // Use raw SQL to delete auto-generated alerts
                await _context.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM Alerts 
                    WHERE Title LIKE '%overdue maintenance%' 
                       OR Title LIKE '%Equipment Status Alert%'
                       OR Title LIKE '%Equipment Became Inactive%'
                       OR Title LIKE '%Equipment Retired%'
                       OR Title LIKE '%Equipment Restored%'
                       OR Title LIKE '%Maintenance Overdue%'
                       OR Description LIKE '%automatically generated%'
                       OR Description LIKE '%has been inactive%'
                       OR Description LIKE '%overdue for maintenance%'
                       OR Description LIKE '%changed from Active to Inactive%'
                       OR Description LIKE '%has not received maintenance%'
                       OR Description LIKE '%Recommended maintenance interval%'
                       OR (CAST(CreatedDate AS DATE) = CAST(GETDATE() AS DATE) AND AssignedToUserId IS NULL)
                ");

                // Count after cleanup
                var alertCountAfter = await _context.Alerts.CountAsync();
                var taskCountAfter = await _context.MaintenanceTasks.CountAsync();

                var alertsCleared = alertCountBefore - alertCountAfter;
                var tasksCleared = taskCountBefore - taskCountAfter;

                TempData["SuccessMessage"] = $"SQL Cleanup completed: {alertsCleared} alerts and {tasksCleared} tasks removed.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error during SQL cleanup: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Test method to add sample data
        public async Task<IActionResult> AddTestData()
        {
            try
            {
                // Check if equipment exists, if not create some
                if (!await _context.Equipment.AnyAsync())
                {
                    var equipment1 = new Equipment
                    {
                        EquipmentTypeId = 1, // Projectors
                        EquipmentModelId = 1, // Projector Model A
                        BuildingId = 1, // Petroleum Building
                        RoomId = 1, // PB001
                        InstallationDate = DateTime.Now.AddYears(-2),
                        ExpectedLifespanMonths = 60,
                        Status = EquipmentStatus.Active,
                        Notes = "Classroom projector"
                    };

                    var equipment2 = new Equipment
                    {
                        EquipmentTypeId = 2, // Air Conditioners
                        EquipmentModelId = 3, // Air Conditioner Model A
                        BuildingId = 1, // Petroleum Building
                        RoomId = 2, // PB012
                        InstallationDate = DateTime.Now.AddYears(-1),
                        ExpectedLifespanMonths = 120,
                        Status = EquipmentStatus.Active,
                        Notes = "Main classroom AC unit"
                    };

                    _context.Equipment.AddRange(equipment1, equipment2);
                    await _context.SaveChangesAsync();
                }
                
                // Check if alerts exist, if not create some
                if (!await _context.Alerts.AnyAsync())
                {
                    var equipment = await _context.Equipment.FirstAsync();
                    
                    var alert1 = new Alert
                    {
                        EquipmentId = equipment.EquipmentId,
                        Title = "Equipment Maintenance Required",
                        Description = "Projector showing signs of overheating - bulb needs replacement",
                        Priority = AlertPriority.High,
                        Status = AlertStatus.Open,
                        CreatedDate = DateTime.Now.AddDays(-2)
                    };

                    var alert2 = new Alert
                    {
                        EquipmentId = equipment.EquipmentId,
                        Title = "Filter Replacement Due",
                        Description = "Air conditioner filter needs to be replaced as per schedule",
                        Priority = AlertPriority.Medium,
                        Status = AlertStatus.Open,
                        CreatedDate = DateTime.Now.AddDays(-1)
                    };

                    _context.Alerts.AddRange(alert1, alert2);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Test data added successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error adding test data: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AssignToMe(int id)
        {
            var alert = await _context.Alerts.FindAsync(id);
            if (alert == null)
                return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            // Assign alert to current user
            alert.AssignedToUserId = currentUser.Id;
            _context.Update(alert);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Alert has been assigned to you.";
            return RedirectToAction(nameof(Index));
        }
    }
}