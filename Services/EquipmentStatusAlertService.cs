using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using Microsoft.EntityFrameworkCore;

namespace FEENALOoFINALE.Services
{
    public class EquipmentStatusAlertService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EquipmentStatusAlertService> _logger;

        public EquipmentStatusAlertService(ApplicationDbContext context, ILogger<EquipmentStatusAlertService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Creates alerts when equipment status changes to Inactive or Retired
        /// Call this method after updating equipment status
        /// </summary>
        public async Task CheckAndCreateStatusAlertsAsync(int equipmentId, EquipmentStatus newStatus, EquipmentStatus? previousStatus = null)
        {
            try
            {
                // Only create alerts for Retired status (not Inactive to reduce alert volume)
                if (newStatus != EquipmentStatus.Retired)
                    return;

                // Don't create duplicate alerts if status hasn't actually changed
                if (previousStatus == newStatus)
                    return;

                // Get equipment details first
                var equipment = await _context.Equipment
                    .Include(e => e.EquipmentModel)
                    .Include(e => e.EquipmentType)
                    .Include(e => e.Building)
                    .Include(e => e.Room)
                    .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId);

                if (equipment == null)
                {
                    _logger.LogWarning("Equipment with ID {EquipmentId} not found", equipmentId);
                    return;
                }

                // Check if this is critical equipment worth alerting about
                var isCriticalEquipment = equipment.EquipmentType?.EquipmentTypeName?.Contains("Critical", StringComparison.OrdinalIgnoreCase) == true ||
                                         equipment.EquipmentModel?.ModelName?.Contains("Critical", StringComparison.OrdinalIgnoreCase) == true;

                if (!isCriticalEquipment)
                {
                    _logger.LogInformation("Equipment {EquipmentId} status changed to {Status} but is not critical equipment, skipping alert", equipmentId, newStatus);
                    return;
                }

                // Check if alert already exists for this equipment and status
                var existingAlert = await _context.Alerts
                    .FirstOrDefaultAsync(a => a.EquipmentId == equipmentId && 
                                            a.Status == AlertStatus.Open &&
                                            a.Description.Contains(newStatus.ToString()));

                if (existingAlert != null)
                {
                    _logger.LogInformation("Alert already exists for equipment {EquipmentId} with status {Status}", equipmentId, newStatus);
                    return;
                }

                // Create appropriate alert based on status
                var alert = new Alert
                {
                    EquipmentId = equipmentId,
                    Priority = GetAlertPriority(newStatus),
                    Title = GetAlertTitle(equipment, newStatus),
                    Description = GetAlertDescription(equipment, newStatus, previousStatus),
                    CreatedDate = DateTime.UtcNow,
                    Status = AlertStatus.Open
                };

                _context.Alerts.Add(alert);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created {Status} alert for equipment {EquipmentId}: {Equipment}", 
                    newStatus, equipmentId, equipment.EquipmentModel?.ModelName ?? "Unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating status alert for equipment {EquipmentId}", equipmentId);
                throw;
            }
        }

        /// <summary>
        /// Bulk check for equipment that should have status alerts
        /// </summary>
        public async Task CreateMissingStatusAlertsAsync()
        {
            try
            {
                var equipmentNeedingAlerts = await _context.Equipment
                    .Include(e => e.EquipmentModel)
                    .Include(e => e.EquipmentType)
                    .Include(e => e.Building)
                    .Include(e => e.Room)
                    .Where(e => e.Status == EquipmentStatus.Inactive || e.Status == EquipmentStatus.Retired)
                    .ToListAsync();

                foreach (var equipment in equipmentNeedingAlerts)
                {
                    // Check if alert already exists
                    var hasAlert = await _context.Alerts
                        .AnyAsync(a => a.EquipmentId == equipment.EquipmentId && 
                                     a.Status == AlertStatus.Open &&
                                     a.Description.Contains(equipment.Status.ToString()));

                    if (!hasAlert)
                    {
                        await CheckAndCreateStatusAlertsAsync(equipment.EquipmentId, equipment.Status);
                    }
                }

                _logger.LogInformation("Completed bulk status alert check for {Count} equipment items", equipmentNeedingAlerts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk status alert creation");
                throw;
            }
        }

        private AlertPriority GetAlertPriority(EquipmentStatus status)
        {
            return status switch
            {
                // Only retired equipment gets critical priority
                EquipmentStatus.Retired => AlertPriority.High,
                // Inactive equipment gets medium priority (not critical)
                EquipmentStatus.Inactive => AlertPriority.Medium,
                _ => AlertPriority.Low
            };
        }

        private string GetAlertTitle(Equipment equipment, EquipmentStatus status)
        {
            var equipmentName = equipment.EquipmentModel?.ModelName ?? $"Equipment #{equipment.EquipmentId}";
            return status switch
            {
                EquipmentStatus.Retired => $"Equipment Retired: {equipmentName}",
                EquipmentStatus.Inactive => $"Equipment Inactive: {equipmentName}",
                _ => $"Equipment Status Change: {equipmentName}"
            };
        }

        private string GetAlertDescription(Equipment equipment, EquipmentStatus newStatus, EquipmentStatus? previousStatus)
        {
            var equipmentName = equipment.EquipmentModel?.ModelName ?? "Unknown Equipment";
            var location = $"{equipment.Building?.BuildingName} - {equipment.Room?.RoomName}";
            var statusChange = previousStatus.HasValue ? $"changed from {previousStatus} to {newStatus}" : $"is now {newStatus}";

            var description = $"Equipment '{equipmentName}' located at {location} {statusChange}.";

            if (newStatus == EquipmentStatus.Retired)
            {
                description += " This equipment should be removed from service and may need replacement planning.";
            }
            else if (newStatus == EquipmentStatus.Inactive)
            {
                description += " This equipment requires attention to restore to active status.";
            }

            if (equipment.InstallationDate.HasValue)
            {
                var age = DateTime.Now - equipment.InstallationDate.Value;
                description += $" Equipment age: {age.Days} days.";
            }

            return description;
        }
    }
}
