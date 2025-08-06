using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Hubs;

namespace FEENALOoFINALE.Services
{
    public class ScheduledMaintenanceService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ScheduledMaintenanceService> _logger;
        private readonly IHubContext<MaintenanceHub> _hubContext;
        private readonly TimeSpan _checkPeriod = TimeSpan.FromHours(6); // Check every 6 hours

        public ScheduledMaintenanceService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ScheduledMaintenanceService> logger,
            IHubContext<MaintenanceHub> hubContext)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled Maintenance Service started");

            // Wait 4 minutes before first check to reduce startup load
            await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessScheduledMaintenance();
                    await Task.Delay(_checkPeriod, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during scheduled maintenance processing");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Wait 30 minutes before retry
                }
            }
        }

        private async Task ProcessScheduledMaintenance()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await GenerateScheduledTasks(dbContext);
            await SendMaintenanceReminders(dbContext);
            await AutoCompleteSimpleTasks(dbContext);
            await GeneratePreventiveMaintenanceSchedules(dbContext);
        }

        private async Task GenerateScheduledTasks(ApplicationDbContext dbContext)
        {
            // Generate maintenance tasks based on equipment age and maintenance history
            var equipment = await dbContext.Equipment
                .Include(e => e.MaintenanceLogs)
                .Where(e => e.Status == EquipmentStatus.Active)
                .ToListAsync();

            var newTasks = new List<MaintenanceTask>();

            foreach (var item in equipment)
            {
                try
                {
                    var pendingTasks = await dbContext.MaintenanceTasks
                        .Where(mt => mt.EquipmentId == item.EquipmentId && mt.Status == MaintenanceStatus.Pending)
                        .ToListAsync();

                    // Don't create new tasks if there are already pending ones
                    if (pendingTasks.Any()) continue;

                    var lastMaintenance = item.MaintenanceLogs?
                        .OrderByDescending(ml => ml.LogDate)
                        .FirstOrDefault();

                    var daysSinceLastMaintenance = lastMaintenance != null 
                        ? (DateTime.Now - lastMaintenance.LogDate).TotalDays 
                        : 365; // Assume 1 year if no maintenance history

                    // Determine if maintenance is needed based on equipment age and last maintenance
                    var equipmentAge = item.InstallationDate.HasValue 
                        ? (DateTime.Now - item.InstallationDate.Value).TotalDays 
                        : 0; // If no installation date, assume new equipment
                    var maintenanceInterval = DetermineMaintenanceInterval(equipmentAge);

                    if (daysSinceLastMaintenance >= maintenanceInterval)
                    {
                        var taskType = DetermineMaintenanceType(daysSinceLastMaintenance, equipmentAge);
                        var scheduledDate = DateTime.Now.AddDays(7); // Schedule for next week

                        var task = new MaintenanceTask
                        {
                            EquipmentId = item.EquipmentId,
                            Description = GenerateMaintenanceDescription(item, taskType),
                            ScheduledDate = scheduledDate,
                            Status = MaintenanceStatus.Pending
                        };

                        newTasks.Add(task);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating scheduled task for equipment {EquipmentId}", item.EquipmentId);
                }
            }

            if (newTasks.Any())
            {
                dbContext.MaintenanceTasks.AddRange(newTasks);
                await dbContext.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("NewTasksScheduled", newTasks.Count);
                _logger.LogInformation("Generated {Count} new scheduled maintenance tasks", newTasks.Count);
            }
        }

        private async Task SendMaintenanceReminders(ApplicationDbContext dbContext)
        {
            // Send reminders for upcoming maintenance tasks
            var upcomingTasks = await dbContext.MaintenanceTasks
                .Include(mt => mt.Equipment)
                .Where(mt => mt.Status == MaintenanceStatus.Pending && 
                            mt.ScheduledDate >= DateTime.Now && 
                            mt.ScheduledDate <= DateTime.Now.AddDays(3))
                .ToListAsync();

            if (upcomingTasks.Any())
            {
                var reminders = upcomingTasks.Select(task => new
                {
                    TaskId = task.TaskId,
                    EquipmentName = task.Equipment?.EquipmentModel?.ModelName ?? "Unknown",
                    ScheduledDate = task.ScheduledDate,
                    Description = task.Description
                }).ToList();

                await _hubContext.Clients.All.SendAsync("MaintenanceReminders", reminders);
                _logger.LogInformation("Sent {Count} maintenance reminders", reminders.Count);
            }
        }

        private async Task AutoCompleteSimpleTasks(ApplicationDbContext dbContext)
        {
            // Auto-complete simple maintenance tasks that are overdue by more than 30 days
            var autoCompletableTasks = await dbContext.MaintenanceTasks
                .Where(mt => mt.Status == MaintenanceStatus.Pending && 
                            mt.ScheduledDate < DateTime.Now.AddDays(-30) &&
                            (mt.Description.Contains("Inspection") || mt.Description.Contains("Cleaning")))
                .ToListAsync();

            foreach (var task in autoCompletableTasks)
            {
                task.Status = MaintenanceStatus.Completed;

                // Create a maintenance log entry
                var maintenanceLog = new MaintenanceLog
                {
                    EquipmentId = task.EquipmentId,
                    LogDate = DateTime.Now,
                    MaintenanceType = MaintenanceType.Preventive,
                    Description = $"Auto-completed overdue maintenance: {task.Description}",
                    Technician = "System Auto-Complete",
                    Cost = 0
                };

                dbContext.MaintenanceLogs.Add(maintenanceLog);
            }

            if (autoCompletableTasks.Any())
            {
                await dbContext.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("TasksAutoCompleted", autoCompletableTasks.Count);
                _logger.LogInformation("Auto-completed {Count} overdue simple tasks", autoCompletableTasks.Count);
            }
        }

        private async Task GeneratePreventiveMaintenanceSchedules(ApplicationDbContext dbContext)
        {
            // Generate preventive maintenance schedules for the next 3 months
            var equipment = await dbContext.Equipment
                .Include(e => e.MaintenanceLogs)
                .Where(e => e.Status == EquipmentStatus.Active)
                .ToListAsync();

            var newScheduledTasks = new List<MaintenanceTask>();

            foreach (var item in equipment)
            {
                // Check if we have scheduled tasks for the next 3 months
                var futureTasksCount = await dbContext.MaintenanceTasks
                    .CountAsync(mt => mt.EquipmentId == item.EquipmentId &&
                                     mt.ScheduledDate > DateTime.Now && 
                                     mt.ScheduledDate <= DateTime.Now.AddDays(90) && 
                                     mt.Status == MaintenanceStatus.Pending);

                if (futureTasksCount < 2) // Ensure at least 2 tasks scheduled in the next 3 months
                {
                    var lastMaintenance = item.MaintenanceLogs?
                        .OrderByDescending(ml => ml.LogDate)
                        .FirstOrDefault();

                    var equipmentAge = item.InstallationDate.HasValue 
                        ? (DateTime.Now - item.InstallationDate.Value).TotalDays 
                        : 0; // If no installation date, assume new equipment
                    var maintenanceInterval = DetermineMaintenanceInterval(equipmentAge);

                    // Schedule next maintenance
                    var nextMaintenanceDate = lastMaintenance != null 
                        ? lastMaintenance.LogDate.AddDays(maintenanceInterval)
                        : DateTime.Now.AddDays(30);

                    // Ensure the date is in the future
                    if (nextMaintenanceDate <= DateTime.Now)
                    {
                        nextMaintenanceDate = DateTime.Now.AddDays(14);
                    }

                    if (nextMaintenanceDate <= DateTime.Now.AddDays(90))
                    {
                        var task = new MaintenanceTask
                        {
                            EquipmentId = item.EquipmentId,
                            Description = $"Scheduled preventive maintenance for {item.EquipmentModel?.ModelName ?? "Equipment"}",
                            ScheduledDate = nextMaintenanceDate,
                            Status = MaintenanceStatus.Pending
                        };

                        newScheduledTasks.Add(task);
                    }
                }
            }

            if (newScheduledTasks.Any())
            {
                dbContext.MaintenanceTasks.AddRange(newScheduledTasks);
                await dbContext.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("PreventiveTasksScheduled", newScheduledTasks.Count);
                _logger.LogInformation("Generated {Count} preventive maintenance schedules", newScheduledTasks.Count);
            }
        }

        private int DetermineMaintenanceInterval(double equipmentAgeInDays)
        {
            // Determine maintenance interval based on equipment age
            return equipmentAgeInDays switch
            {
                > 365 * 5 => 60,  // Every 2 months for old equipment
                > 365 * 3 => 90,  // Every 3 months for mature equipment
                > 365 => 120,     // Every 4 months for newer equipment
                _ => 180          // Every 6 months for very new equipment
            };
        }

        private string DetermineMaintenanceType(double daysSinceLastMaintenance, double equipmentAge)
        {
            if (daysSinceLastMaintenance > 365 || equipmentAge > 365 * 5)
                return "Comprehensive Maintenance";
            else if (daysSinceLastMaintenance > 180)
                return "Preventive Maintenance";
            else
                return "Inspection";
        }

        private string GenerateMaintenanceDescription(Equipment equipment, string taskType)
        {
            var equipmentName = equipment.EquipmentModel?.ModelName ?? "Equipment";
            return taskType switch
            {
                "Inspection" => $"Routine inspection of {equipmentName} - check operational status, visual examination, basic functionality test",
                "Preventive Maintenance" => $"Preventive maintenance for {equipmentName} - cleaning, lubrication, calibration, component inspection",
                "Comprehensive Maintenance" => $"Comprehensive maintenance for {equipmentName} - full system check, component replacement, performance optimization",
                _ => $"{taskType} for {equipmentName}"
            };
        }
    }
}
