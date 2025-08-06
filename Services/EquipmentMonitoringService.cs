using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Hubs;

namespace FEENALOoFINALE.Services
{
    public class EquipmentMonitoringService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<EquipmentMonitoringService> _logger;
        private readonly IHubContext<MaintenanceHub> _hubContext;
        private readonly TimeSpan _monitoringPeriod = TimeSpan.FromMinutes(5); // Monitor every 5 minutes

        public EquipmentMonitoringService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<EquipmentMonitoringService> logger,
            IHubContext<MaintenanceHub> hubContext)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Equipment Monitoring Service started");

            // Wait 2 minutes before first monitoring to reduce startup load
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorEquipment();
                    await Task.Delay(_monitoringPeriod, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during equipment monitoring");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait 1 minute before retry
                }
            }
        }

        private async Task MonitorEquipment()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get all equipment that needs monitoring
            var equipment = await dbContext.Equipment
                .Include(e => e.MaintenanceLogs)
                .Include(e => e.EquipmentModel)
                .Where(e => e.Status != EquipmentStatus.Retired)
                .ToListAsync();

            var statusChanges = new List<object>();
            var performanceIssues = new List<object>();

            foreach (var item in equipment)
            {
                try
                {
                    // Simulate equipment monitoring (in real world, this would connect to IoT sensors)
                    var monitoringResult = SimulateEquipmentMonitoring(item);
                    
                    if (monitoringResult.StatusChanged)
                    {
                        statusChanges.Add(new
                        {
                            EquipmentId = item.EquipmentId,
                            Name = item.EquipmentModel?.ModelName ?? "Unknown",
                            OldStatus = monitoringResult.OldStatus,
                            NewStatus = monitoringResult.NewStatus,
                            Timestamp = DateTime.Now
                        });

                        // Update equipment status in database
                        item.Status = monitoringResult.NewStatus;
                        await dbContext.SaveChangesAsync();
                    }

                    if (monitoringResult.PerformanceIssue)
                    {
                        performanceIssues.Add(new
                        {
                            EquipmentId = item.EquipmentId,
                            Name = item.EquipmentModel?.ModelName ?? "Unknown",
                            Issue = monitoringResult.IssueDescription,
                            Severity = monitoringResult.IssueSeverity,
                            Timestamp = DateTime.Now
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring equipment {EquipmentId}", item.EquipmentId);
                }
            }

            // Notify clients about status changes
            if (statusChanges.Any())
            {
                await _hubContext.Clients.All.SendAsync("EquipmentStatusChanged", statusChanges);
                _logger.LogInformation("Equipment status changes detected: {Count}", statusChanges.Count);
            }

            // Notify clients about performance issues
            if (performanceIssues.Any())
            {
                await _hubContext.Clients.All.SendAsync("PerformanceIssuesDetected", performanceIssues);
                _logger.LogInformation("Performance issues detected: {Count}", performanceIssues.Count);
            }
        }

        private EquipmentMonitoringResult SimulateEquipmentMonitoring(Equipment equipment)
        {
            var result = new EquipmentMonitoringResult
            {
                EquipmentId = equipment.EquipmentId,
                OldStatus = equipment.Status
            };

            // Simulate IoT sensor data and monitoring logic
            var random = new Random();
            
            // Simulate equipment behavior based on current status
            switch (equipment.Status)
            {
                case EquipmentStatus.Active:
                    // 2% chance of equipment going into maintenance or failing
                    var statusChange = random.NextDouble();
                    if (statusChange < 0.005) // 0.5% chance of failure
                    {
                        result.NewStatus = EquipmentStatus.Inactive;
                        result.StatusChanged = true;
                    }
                    else if (statusChange < 0.02) // 1.5% chance of needing maintenance
                    {
                        result.NewStatus = EquipmentStatus.Inactive;
                        result.StatusChanged = true;
                    }
                    else
                    {
                        result.NewStatus = equipment.Status;
                    }

                    // 5% chance of performance issues
                    if (random.NextDouble() < 0.05)
                    {
                        result.PerformanceIssue = true;
                        result.IssueDescription = GeneratePerformanceIssue(equipment);
                        result.IssueSeverity = random.NextDouble() < 0.3 ? "High" : "Medium";
                    }
                    break;

                case EquipmentStatus.Inactive:
                    // 10% chance of maintenance completion
                    if (random.NextDouble() < 0.1)
                    {
                        result.NewStatus = EquipmentStatus.Active;
                        result.StatusChanged = true;
                    }
                    else
                    {
                        result.NewStatus = equipment.Status;
                    }
                    break;

                case EquipmentStatus.Retired:
                    // 5% chance of repair completion
                    if (random.NextDouble() < 0.05)
                    {
                        result.NewStatus = EquipmentStatus.Active;
                        result.StatusChanged = true;
                    }
                    else
                    {
                        result.NewStatus = equipment.Status;
                    }
                    break;

                default:
                    result.NewStatus = equipment.Status;
                    break;
            }

            return result;
        }

        private string GeneratePerformanceIssue(Equipment equipment)
        {
            var issues = new[]
            {
                "Temperature reading above normal range",
                "Vibration levels elevated",
                "Power consumption increased by 15%",
                "Response time degraded",
                "Unusual noise detected",
                "Efficiency below optimal levels",
                "Memory usage high",
                "Network connectivity intermittent",
                "Sensor calibration drift detected",
                "Operational frequency variance"
            };

            var random = new Random();
            return issues[random.Next(issues.Length)];
        }

        private class EquipmentMonitoringResult
        {
            public int EquipmentId { get; set; }
            public EquipmentStatus OldStatus { get; set; }
            public EquipmentStatus NewStatus { get; set; }
            public bool StatusChanged { get; set; }
            public bool PerformanceIssue { get; set; }
            public string IssueDescription { get; set; } = string.Empty;
            public string IssueSeverity { get; set; } = "Low";
        }
    }
}
