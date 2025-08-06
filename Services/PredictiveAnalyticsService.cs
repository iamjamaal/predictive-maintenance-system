using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Hubs;

namespace FEENALOoFINALE.Services
{
    public class PredictiveAnalyticsService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<PredictiveAnalyticsService> _logger;
        private readonly IHubContext<MaintenanceHub> _hubContext;
        private readonly TimeSpan _analysisPeriod = TimeSpan.FromMinutes(30); // Run every 30 minutes

        public PredictiveAnalyticsService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<PredictiveAnalyticsService> logger,
            IHubContext<MaintenanceHub> hubContext)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Predictive Analytics Service started");

            // Wait 3 minutes before first analysis to reduce startup load
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformPredictiveAnalysis();
                    await Task.Delay(_analysisPeriod, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during predictive analysis");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retry
                }
            }
        }

        private async Task PerformPredictiveAnalysis()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            _logger.LogInformation("Starting predictive analysis cycle");

            // Get all active equipment
            var equipment = await dbContext.Equipment
                .Include(e => e.MaintenanceLogs)
                .Where(e => e.Status == EquipmentStatus.Active)
                .ToListAsync();

            var newPredictions = new List<FailurePrediction>();

            foreach (var item in equipment)
            {
                try
                {
                    var prediction = await AnalyzeEquipmentFailureRisk(item, dbContext);
                    if (prediction != null)
                    {
                        newPredictions.Add(prediction);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing equipment {EquipmentId}", item.EquipmentId);
                }
            }

            // Save new predictions
            if (newPredictions.Any())
            {
                dbContext.FailurePredictions.AddRange(newPredictions);
                await dbContext.SaveChangesAsync();

                // Notify clients about new predictions
                await _hubContext.Clients.All.SendAsync("PredictionsUpdated", newPredictions.Count);

                _logger.LogInformation("Generated {Count} new failure predictions", newPredictions.Count);
            }
        }

        private async Task<FailurePrediction?> AnalyzeEquipmentFailureRisk(Equipment equipment, ApplicationDbContext dbContext)
        {
            // Simple predictive algorithm based on maintenance history and equipment age
            var recentMaintenanceLogs = equipment.MaintenanceLogs?
                .Where(ml => ml.LogDate >= DateTime.Now.AddDays(-90))
                .OrderByDescending(ml => ml.LogDate)
                .ToList() ?? new List<MaintenanceLog>();

            // Calculate failure probability based on various factors
            double failureProbability = 0.0;
            string riskFactors = "";

            // Factor 1: Age of equipment (assuming InstallationDate represents when equipment was put into service)
            var equipmentAge = equipment.InstallationDate.HasValue 
                ? (DateTime.Now - equipment.InstallationDate.Value).TotalDays 
                : 0; // If no installation date, assume new equipment
            if (equipmentAge > 365 * 5) // Over 5 years old
            {
                failureProbability += 0.2;
                riskFactors += "Equipment age over 5 years; ";
            }
            else if (equipmentAge > 365 * 3) // Over 3 years old
            {
                failureProbability += 0.1;
                riskFactors += "Equipment age over 3 years; ";
            }

            // Factor 2: Frequency of recent maintenance
            var maintenanceFrequency = recentMaintenanceLogs.Count;
            if (maintenanceFrequency > 5) // More than 5 maintenance logs in 90 days
            {
                failureProbability += 0.3;
                riskFactors += "High maintenance frequency; ";
            }
            else if (maintenanceFrequency > 3)
            {
                failureProbability += 0.15;
                riskFactors += "Moderate maintenance frequency; ";
            }

            // Factor 3: Time since last maintenance
            var lastMaintenance = recentMaintenanceLogs.FirstOrDefault();
            if (lastMaintenance != null)
            {
                var daysSinceLastMaintenance = (DateTime.Now - lastMaintenance.LogDate).TotalDays;
                if (daysSinceLastMaintenance > 180) // No maintenance in 6 months
                {
                    failureProbability += 0.25;
                    riskFactors += "No recent maintenance; ";
                }
                else if (daysSinceLastMaintenance > 90)
                {
                    failureProbability += 0.1;
                    riskFactors += "Limited recent maintenance; ";
                }
            }
            else
            {
                // No maintenance history
                failureProbability += 0.2;
                riskFactors += "No maintenance history; ";
            }

            // Factor 4: Critical equipment types get higher priority
            if (equipment.EquipmentModel?.ModelName?.Contains("Critical", StringComparison.OrdinalIgnoreCase) == true ||
                equipment.EquipmentModel?.ModelName?.Contains("Server", StringComparison.OrdinalIgnoreCase) == true ||
                equipment.EquipmentModel?.ModelName?.Contains("Generator", StringComparison.OrdinalIgnoreCase) == true)
            {
                failureProbability += 0.1;
                riskFactors += "Critical equipment type; ";
            }

            // Factor 5: Usage hours (new)
            double avgUsage = equipment.AverageWeeklyUsageHours ?? 0;

            // Optionally, use recent history for more granularity:
            var recentUsage = await dbContext.EquipmentUsageHistories
                .Where(u => u.EquipmentId == equipment.EquipmentId && u.WeekStart >= DateTime.Now.AddDays(-90))
                .ToListAsync();

            double recentAvgUsage = recentUsage.Any() ? recentUsage.Average(u => u.UsageHours) : avgUsage;

            if (recentAvgUsage > 40) // Example: over 40 hours/week is heavy use
            {
                failureProbability += 0.2;
                riskFactors += "High weekly usage; ";
            }
            else if (recentAvgUsage > 20)
            {
                failureProbability += 0.1;
                riskFactors += "Moderate weekly usage; ";
            }

            // Only create prediction if probability is significant
            if (failureProbability >= 0.3)
            {
                // Check if we already have a recent prediction for this equipment
                var existingPrediction = await dbContext.FailurePredictions
                    .Where(fp => fp.EquipmentId == equipment.EquipmentId && 
                                fp.CreatedDate >= DateTime.Now.AddDays(-7))
                    .FirstOrDefaultAsync();

                if (existingPrediction == null)
                {
                    var riskLevel = failureProbability switch
                    {
                        >= 0.7 => PredictionStatus.High,
                        >= 0.5 => PredictionStatus.High,
                        >= 0.3 => PredictionStatus.Medium,
                        _ => PredictionStatus.Low
                    };

                    return new FailurePrediction
                    {
                        EquipmentId = equipment.EquipmentId,
                        CreatedDate = DateTime.Now,
                        PredictedFailureDate = DateTime.Now.AddDays(30 / failureProbability), // Higher probability = sooner failure
                        ConfidenceLevel = (int)Math.Min(failureProbability * 100, 95), // Cap at 95%
                        Status = riskLevel,
                        AnalysisNotes = $"Predictive analysis indicates {riskLevel.ToString().ToLower()} risk of failure. Risk factors: {riskFactors.TrimEnd(' ', ';')}"
                    };
                }
            }

            return null;
        }

        // Add this method to allow retraining/updating the model with new maintenance logs
        public async Task UpdateModelWithMaintenanceLogAsync(MaintenanceLog log)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Gather all relevant logs and usage data for the equipment
            var logs = await dbContext.MaintenanceLogs
                .Where(l => l.EquipmentId == log.EquipmentId)
                .ToListAsync();

            var usage = await dbContext.EquipmentUsageHistories
                .Where(u => u.EquipmentId == log.EquipmentId)
                .ToListAsync();

            // Call your ML retraining/update logic here
            // e.g., MLModelTrainer.UpdateModel(logs, usage);

            await Task.CompletedTask;
        }
    }
}
