using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Hubs;

namespace FEENALOoFINALE.Services
{
    public class AutomatedAlertService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<AutomatedAlertService> _logger;
        private readonly IHubContext<MaintenanceHub> _hubContext;
        private readonly TimeSpan _checkPeriod = TimeSpan.FromDays(1); // Check only once per day now

        public AutomatedAlertService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<AutomatedAlertService> logger,
            IHubContext<MaintenanceHub> hubContext)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Automated Alert Service started");

            // Wait 2 hours before first check to avoid generating alerts on startup
            await Task.Delay(TimeSpan.FromHours(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndGenerateAlerts();
                    await Task.Delay(_checkPeriod, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during alert generation");
                    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); // Wait 2 minutes before retry
                }
            }
        }

        private async Task CheckAndGenerateAlerts()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var newAlerts = new List<Alert>();

            // Check for overdue maintenance tasks
            await CheckOverdueMaintenanceTasks(dbContext, newAlerts);

            // Check for equipment status changes
            await CheckEquipmentStatusIssues(dbContext, newAlerts);

            // Check for low inventory levels
            await CheckLowInventoryLevels(dbContext, newAlerts);

            // Check for high-risk failure predictions
            await CheckHighRiskPredictions(dbContext, newAlerts);

            // Check for equipment without recent maintenance
            await CheckEquipmentMaintenanceOverdue(dbContext, newAlerts);

            // Save new alerts
            if (newAlerts.Any())
            {
                dbContext.Alerts.AddRange(newAlerts);
                await dbContext.SaveChangesAsync();

                // NEW: Auto-create maintenance tasks for critical alerts
                await AutoCreateMaintenanceTasksFromAlerts(dbContext, newAlerts);

                // Notify clients about new alerts
                await _hubContext.Clients.All.SendAsync("NewAlerts", newAlerts.Count);

                // Send critical alerts immediately
                var criticalAlerts = newAlerts.Where(a => a.Priority == AlertPriority.High).ToList();
                if (criticalAlerts.Any())
                {
                    await _hubContext.Clients.All.SendAsync("CriticalAlerts", criticalAlerts);
                }

                _logger.LogInformation("Generated {Count} new alerts ({Critical} critical)", 
                    newAlerts.Count, criticalAlerts.Count);
            }
        }

        private async Task CheckOverdueMaintenanceTasks(ApplicationDbContext dbContext, List<Alert> newAlerts)
        {
            var overdueTasks = await dbContext.MaintenanceTasks
                .Include(mt => mt.Equipment)
                .Where(mt => mt.Status == MaintenanceStatus.Pending && mt.ScheduledDate < DateTime.Now.AddDays(-1))
                .ToListAsync();

            foreach (var task in overdueTasks)
            {
                // Check if we already have a recent alert for this task (within last 30 days)
                var existingAlert = await dbContext.Alerts
                    .Where(a => a.EquipmentId == task.EquipmentId && 
                               a.Description.Contains($"Task {task.TaskId}") && 
                               a.CreatedDate >= DateTime.Now.AddDays(-30))
                    .FirstOrDefaultAsync();

                if (existingAlert == null)
                {
                    var daysOverdue = (DateTime.Now - task.ScheduledDate).TotalDays;
                    // Make critical alerts extremely rare - only after 120+ days overdue
                    var priority = daysOverdue > 120 ? AlertPriority.High : daysOverdue > 60 ? AlertPriority.Medium : AlertPriority.Low;

                    newAlerts.Add(new Alert
                    {
                        EquipmentId = task.EquipmentId,
                        Priority = priority,
                        Description = $"Maintenance task {task.TaskId} is {Math.Floor(daysOverdue)} days overdue for equipment {task.Equipment?.EquipmentModel?.ModelName ?? "Unknown"}",
                        CreatedDate = DateTime.Now,
                        Status = AlertStatus.Open
                    });
                }
            }
        }

        private async Task CheckEquipmentStatusIssues(ApplicationDbContext dbContext, List<Alert> newAlerts)
        {
            var problematicEquipment = await dbContext.Equipment
                .Where(e => e.Status == EquipmentStatus.Inactive)
                .ToListAsync();

            foreach (var equipment in problematicEquipment)
            {
                // Check if we already have a recent alert for this equipment status (within last 7 days)
                var existingAlert = await dbContext.Alerts
                    .Where(a => a.EquipmentId == equipment.EquipmentId && 
                               a.Description.Contains("status") && 
                               a.CreatedDate >= DateTime.Now.AddDays(-7))
                    .FirstOrDefaultAsync();

                if (existingAlert == null)
                {
                    // Only create alerts for truly problematic equipment, not just inactive
                    var priority = AlertPriority.Low; // Keep equipment status alerts as low priority
                    var statusMessage = equipment.Status == EquipmentStatus.Inactive ? "inactive" : "under maintenance";

                    newAlerts.Add(new Alert
                    {
                        EquipmentId = equipment.EquipmentId,
                        Priority = priority,
                        Description = $"Equipment {equipment.EquipmentModel?.ModelName ?? "Unknown"} is currently {statusMessage}",
                        CreatedDate = DateTime.Now,
                        Status = AlertStatus.Open
                    });
                }
            }
        }

        private async Task CheckLowInventoryLevels(ApplicationDbContext dbContext, List<Alert> newAlerts)
        {
            var inventoryItems = await dbContext.InventoryItems
                .Include(ii => ii.InventoryStocks)
                .ToListAsync();

            foreach (var item in inventoryItems)
            {
                var currentStock = item.InventoryStocks?.Sum(stock => stock.Quantity) ?? 0;
                
                if (currentStock <= item.MinimumStockLevel)
                {
                    // Check if we already have a recent alert for this inventory item (within last 7 days)
                    var existingAlert = await dbContext.Alerts
                        .Where(a => a.Description.Contains($"inventory item {item.Name}") && 
                                   a.CreatedDate >= DateTime.Now.AddDays(-7))
                        .FirstOrDefaultAsync();

                    if (existingAlert == null)
                    {
                        // Only mark as critical if completely out of stock AND it's a critical item
                        var priority = currentStock == 0 && item.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase) ? 
                                      AlertPriority.High : AlertPriority.Low;
                        var stockStatus = currentStock == 0 ? "out of stock" : "low stock";

                        newAlerts.Add(new Alert
                        {
                            // For inventory alerts, we set InventoryItemId instead of EquipmentId
                            InventoryItemId = item.ItemId,
                            Priority = priority,
                            Title = $"Inventory Alert - {item.Name}",
                            Description = $"Inventory item {item.Name} is {stockStatus} (Current: {currentStock}, Minimum: {item.MinimumStockLevel})",
                            CreatedDate = DateTime.Now,
                            Status = AlertStatus.Open
                        });
                    }
                }
            }
        }

        private async Task CheckHighRiskPredictions(ApplicationDbContext dbContext, List<Alert> newAlerts)
        {
            var highRiskPredictions = await dbContext.FailurePredictions
                .Include(fp => fp.Equipment)
                .Where(fp => (fp.Status == PredictionStatus.High) && 
                            fp.CreatedDate >= DateTime.Now.AddDays(-1))
                .ToListAsync();

            foreach (var prediction in highRiskPredictions)
            {
                // Check if we already have a recent alert for this prediction
                var existingAlert = await dbContext.Alerts
                    .Where(a => a.EquipmentId == prediction.EquipmentId && 
                               a.Description.Contains("failure prediction") && 
                               a.CreatedDate >= DateTime.Now.AddDays(-1))
                    .FirstOrDefaultAsync();

                if (existingAlert == null)
                {
                    // Only mark as critical if high prediction status AND high confidence
                    var priority = (prediction.Status == PredictionStatus.High && prediction.ConfidenceLevel > 80) ? 
                                  AlertPriority.High : AlertPriority.Medium;

                    newAlerts.Add(new Alert
                    {
                        EquipmentId = prediction.EquipmentId,
                        Priority = priority,
                        Description = $"High failure prediction for equipment {prediction.Equipment?.EquipmentModel?.ModelName ?? "Unknown"}: {prediction.Status} risk level with {prediction.ConfidenceLevel}% confidence",
                        CreatedDate = DateTime.Now,
                        Status = AlertStatus.Open
                    });
                }
            }
        }

        private async Task CheckEquipmentMaintenanceOverdue(ApplicationDbContext dbContext, List<Alert> newAlerts)
        {
            var equipment = await dbContext.Equipment
                .Include(e => e.MaintenanceLogs)
                .Where(e => e.Status == EquipmentStatus.Active)
                .ToListAsync();

            foreach (var item in equipment)
            {
                var lastMaintenance = item.MaintenanceLogs?
                    .OrderByDescending(ml => ml.LogDate)
                    .FirstOrDefault();

                if (lastMaintenance == null || (DateTime.Now - lastMaintenance.LogDate).TotalDays > 1095) // Only alert after 3 years, not 1
                {
                    // Check if we already have a recent alert for this equipment maintenance (within last 30 days)
                    var existingAlert = await dbContext.Alerts
                        .Where(a => a.EquipmentId == item.EquipmentId && 
                                   a.Description.Contains("maintenance overdue") && 
                                   a.CreatedDate >= DateTime.Now.AddDays(-30))
                        .FirstOrDefaultAsync();

                    if (existingAlert == null)
                    {
                        var daysSinceLastMaintenance = lastMaintenance != null 
                            ? (DateTime.Now - lastMaintenance.LogDate).TotalDays 
                            : 9999;

                        // Make critical alerts extremely rare - only after 5+ years without maintenance
                        var priority = daysSinceLastMaintenance > 1825 ? AlertPriority.High : AlertPriority.Low; // 5 years = high priority

                        var message = lastMaintenance != null 
                            ? $"Equipment {item.EquipmentModel?.ModelName ?? "Unknown"} maintenance overdue: {Math.Floor(daysSinceLastMaintenance)} days since last maintenance"
                            : $"Equipment {item.EquipmentModel?.ModelName ?? "Unknown"} has no maintenance history and may require inspection";

                        newAlerts.Add(new Alert
                        {
                            EquipmentId = item.EquipmentId,
                            Priority = priority,
                            Description = message,
                            CreatedDate = DateTime.Now,
                            Status = AlertStatus.Open
                        });
                    }
                }
            }
        }

        private async Task AutoCreateMaintenanceTasksFromAlerts(ApplicationDbContext dbContext, List<Alert> newAlerts)
        {
            var maintenanceTasks = new List<MaintenanceTask>();
            
            foreach (var alert in newAlerts.Where(a => a.Priority == AlertPriority.High || a.Priority == AlertPriority.Medium))
            {
                if (alert.EquipmentId.HasValue)
                {
                    // Check if we already have a pending task for this equipment
                    var existingTask = await dbContext.MaintenanceTasks
                        .Where(mt => mt.EquipmentId == alert.EquipmentId.Value && 
                                    mt.Status == MaintenanceStatus.Pending)
                        .FirstOrDefaultAsync();

                    if (existingTask == null)
                    {
                        // Create corresponding maintenance task
                        var task = new MaintenanceTask
                        {
                            EquipmentId = alert.EquipmentId.Value,
                            Description = GenerateTaskDescription(alert),
                            ScheduledDate = DateTime.Now.AddDays(GetMaintenanceUrgency(alert.Priority)),
                            Status = MaintenanceStatus.Pending,
                            CreatedFromAlertId = alert.AlertId,  // Link back to original alert
                            Priority = MapAlertPriorityToTaskPriority(alert.Priority)  // Set task priority
                        };
                        
                        maintenanceTasks.Add(task);
                    }
                }
            }
            
            if (maintenanceTasks.Any())
            {
                dbContext.MaintenanceTasks.AddRange(maintenanceTasks);
                await dbContext.SaveChangesAsync();
                
                // Notify dashboard of new pending tasks
                await _hubContext.Clients.All.SendAsync("NewMaintenanceTasksFromAlerts", maintenanceTasks.Count);
                _logger.LogInformation("Auto-created {Count} maintenance tasks from alerts", maintenanceTasks.Count);
            }
        }

        private int GetMaintenanceUrgency(AlertPriority priority)
        {
            return priority switch
            {
                AlertPriority.High => 1,     // Schedule for tomorrow
                AlertPriority.Medium => 3,   // Schedule for 3 days
                AlertPriority.Low => 7,      // Schedule for 1 week
                _ => 7
            };
        }

        private string GenerateTaskDescription(Alert alert)
        {
            return alert.Description switch
            {
                var desc when desc.Contains("overdue maintenance") => "Perform overdue maintenance inspection",
                var desc when desc.Contains("inactive") => "Investigate equipment status and restore operation",
                var desc when desc.Contains("failure prediction") => "Conduct preventive maintenance to avoid failure",
                var desc when desc.Contains("inventory") => "Check parts availability and restock if needed",
                _ => $"Address equipment issue: {alert.Description}"
            };
        }

        private TaskPriority MapAlertPriorityToTaskPriority(AlertPriority alertPriority)
        {
            return alertPriority switch
            {
                AlertPriority.High => TaskPriority.High,
                AlertPriority.Medium => TaskPriority.Medium,
                AlertPriority.Low => TaskPriority.Low,
                _ => TaskPriority.Low
            };
        }
    }
}
