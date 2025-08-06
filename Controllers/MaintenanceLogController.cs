using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using Microsoft.AspNetCore.Authorization;
using FEENALOoFINALE.Services;
using Microsoft.AspNetCore.Identity;

namespace FEENALOoFINALE.Controllers
{
    [Authorize]
    public class MaintenanceLogController(ApplicationDbContext context, IPredictiveAnalyticsService predictiveAnalyticsService, UserManager<User> userManager) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IPredictiveAnalyticsService _predictiveAnalyticsService = predictiveAnalyticsService;
        private readonly UserManager<User> _userManager = userManager;

        // GET: MaintenanceLog
        public async Task<IActionResult> Index()
        {
            return View(await _context.MaintenanceLogs
                .Include(m => m.Equipment)
                    .ThenInclude(e => e!.EquipmentType)
                .Include(m => m.Equipment)
                    .ThenInclude(e => e!.EquipmentModel)
                .Include(m => m.Task)
                .Include(m => m.Alert)
                .OrderByDescending(m => m.LogDate)
                .ToListAsync());
        }

        // GET: MaintenanceLog/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var maintenanceLog = await _context.MaintenanceLogs
                .Include(m => m.Equipment)
                .Include(m => m.MaintenanceInventoryLinks)
                    .ThenInclude(mil => mil.InventoryItem)
                .FirstOrDefaultAsync(m => m.LogId == id);

            if (maintenanceLog == null)
            {
                return NotFound();
            }

            return View(maintenanceLog);
        }

        // GET: MaintenanceLog/Create
        public async Task<IActionResult> Create(int? equipmentId, int? alertId = null, int? taskId = null)
        {
            await LoadEquipmentViewBag(equipmentId, alertId);
            
            // Pre-populate form if coming from alert
            var model = new MaintenanceLog();
            if (equipmentId.HasValue)
            {
                model.EquipmentId = equipmentId.Value;
            }
            if (alertId.HasValue)
            {
                model.AlertId = alertId.Value;
            }
            if (taskId.HasValue)
            {
                model.MaintenanceTaskId = taskId.Value;
            }
            
            ViewBag.AlertId = alertId;
            ViewBag.MaintenanceTaskId = taskId;
            
            return View(model);
        }

        // POST: MaintenanceLog/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EquipmentId,LogDate,MaintenanceType,Description,Technician,DowntimeHours,Cost,AlertId,MaintenanceTaskId")] MaintenanceLog maintenanceLog)
        {
            // Remove any validation errors for DowntimeDuration since we're using DowntimeHours
            ModelState.Remove("DowntimeDuration");
            
            if (ModelState.IsValid)
            {
                try
                {
                    // Ensure Technician is set if not provided
                    if (string.IsNullOrEmpty(maintenanceLog.Technician))
                    {
                        maintenanceLog.Technician = User?.Identity?.Name ?? "Unknown";
                    }

                    // Set default values for required properties
                    maintenanceLog.Status = MaintenanceStatus.Pending; // Start as pending, not completed
                    
                    // Use the provided cost or default to 0
                    if (maintenanceLog.Cost == 0 && !ModelState.ContainsKey("Cost"))
                    {
                        maintenanceLog.Cost = 0;
                    }

                    _context.Add(maintenanceLog);
                    await _context.SaveChangesAsync();

                    // Update AI model with new maintenance log
                    await _predictiveAnalyticsService.UpdateModelWithMaintenanceLogAsync(maintenanceLog);

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database error: {ex.Message}");
                    ModelState.AddModelError("", $"Database error: {ex.Message}");
                }
            }
            
            // Reload equipment data for the view in case of validation errors
            await LoadEquipmentViewBag(maintenanceLog.EquipmentId, maintenanceLog.AlertId);
            return View(maintenanceLog);
        }

        private async Task LoadEquipmentViewBag(int? selectedEquipmentId = null, int? alertId = null)
        {
            var equipment = await _context.Equipment
                .Include(e => e.EquipmentModel)
                .Include(e => e.EquipmentType)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .ToListAsync();
                
            ViewBag.Equipment = equipment;
            ViewBag.EquipmentId = selectedEquipmentId;
            ViewBag.AlertId = alertId;
        }

        // GET: MaintenanceLog/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var maintenanceLog = await _context.MaintenanceLogs.FindAsync(id);
            if (maintenanceLog == null)
            {
                return NotFound();
            }
            
            await LoadEquipmentViewBag(maintenanceLog.EquipmentId);
            return View(maintenanceLog);
        }

        // POST: MaintenanceLog/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("LogId,EquipmentId,LogDate,MaintenanceType,Description,Technician,DowntimeHours,Cost")] MaintenanceLog maintenanceLog)
        {
            if (id != maintenanceLog.LogId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(maintenanceLog);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MaintenanceLogExists(maintenanceLog.LogId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            await LoadEquipmentViewBag(maintenanceLog.EquipmentId);
            return View(maintenanceLog);
        }

        // GET: MaintenanceLog/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var maintenanceLog = await _context.MaintenanceLogs
                .Include(m => m.Equipment)
                .FirstOrDefaultAsync(m => m.LogId == id);
            if (maintenanceLog == null)
            {
                return NotFound();
            }

            return View(maintenanceLog);
        }

        // POST: MaintenanceLog/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var maintenanceLog = await _context.MaintenanceLogs.FindAsync(id);
            if (maintenanceLog != null)
            {
                _context.MaintenanceLogs.Remove(maintenanceLog);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: MaintenanceLog/ByEquipment/5
        public async Task<IActionResult> ByEquipment(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var equipment = await _context.Equipment
                .Include(e => e.MaintenanceLogs)
                .FirstOrDefaultAsync(e => e.EquipmentId == id);

            if (equipment == null)
            {
                return NotFound();
            }

            return View(equipment);
        }

        // POST: MaintenanceLog/MarkCompleted/5
        [HttpPost]
        public async Task<IActionResult> MarkCompleted(int id)
        {
            var maintenanceLog = await _context.MaintenanceLogs
                .Include(m => m.Task)
                .Include(m => m.Alert)
                .FirstOrDefaultAsync(m => m.LogId == id);

            if (maintenanceLog == null)
            {
                return NotFound();
            }

            try
            {
                // Mark the maintenance log as completed
                maintenanceLog.Status = MaintenanceStatus.Completed;

                // If this maintenance log is linked to a task, mark the task as completed too
                if (maintenanceLog.Task != null)
                {
                    maintenanceLog.Task.Status = MaintenanceStatus.Completed;
                    maintenanceLog.Task.CompletedDate = DateTime.Now;
                }

                // If this maintenance log is linked to an alert, mark the alert as resolved
                if (maintenanceLog.Alert != null)
                {
                    maintenanceLog.Alert.Status = AlertStatus.Resolved;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Maintenance marked as completed successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error marking maintenance as completed: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: MaintenanceLog/ActiveTasks - Show only pending/in-progress logs for technicians
        public async Task<IActionResult> ActiveTasks()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            
            // Get only pending and in-progress maintenance logs, prioritizing current user's tasks
            var activeTasks = await _context.MaintenanceLogs
                .Include(m => m.Equipment)
                    .ThenInclude(e => e!.EquipmentType)
                .Include(m => m.Equipment)
                    .ThenInclude(e => e!.EquipmentModel)
                .Include(m => m.Task)
                .Include(m => m.Alert)
                .Where(m => m.Status == MaintenanceStatus.Pending || m.Status == MaintenanceStatus.InProgress)
                .OrderBy(m => m.Technician == currentUser!.UserName ? 0 : 1) // Current user's tasks first
                .ThenBy(m => m.LogDate)
                .ToListAsync();

            ViewBag.CurrentUserName = currentUser?.UserName;
            ViewData["Title"] = "Active Tasks - Logs to Complete";
            
            return View(activeTasks);
        }

        private bool MaintenanceLogExists(int id)
        {
            return _context.MaintenanceLogs.Any(e => e.LogId == id);
        }
    }
}