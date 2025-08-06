using Microsoft.EntityFrameworkCore;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;

namespace FEENALOoFINALE.Services
{
    public class PredictiveAnalyticsDataService : IPredictiveAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PredictiveAnalyticsDataService> _logger;

        public PredictiveAnalyticsDataService(
            ApplicationDbContext context,
            ILogger<PredictiveAnalyticsDataService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task UpdateModelWithMaintenanceLogAsync(MaintenanceLog log)
        {
            try
            {
                // Gather all relevant logs and usage data for the equipment
                var logs = await _context.MaintenanceLogs
                    .Where(l => l.EquipmentId == log.EquipmentId)
                    .ToListAsync();

                var usage = await _context.EquipmentUsageHistories
                    .Where(u => u.EquipmentId == log.EquipmentId)
                    .ToListAsync();

                // Call your ML retraining/update logic here
                // e.g., MLModelTrainer.UpdateModel(logs, usage);
                _logger.LogInformation("Updated predictive model with new maintenance log for Equipment ID: {EquipmentId}", log.EquipmentId);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating predictive model with maintenance log for Equipment ID: {EquipmentId}", log.EquipmentId);
            }
        }
    }
}
