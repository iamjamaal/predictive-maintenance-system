using FEENALOoFINALE.Models;

namespace FEENALOoFINALE.Models.DTOs
{
    public class AlertNotificationDto
    {
        public int AlertId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AlertPriority Priority { get; set; }
        public AlertStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? AssignedToName { get; set; }
        public int? EquipmentId { get; set; }
        public string? EquipmentName { get; set; }
        public string? EquipmentLocation { get; set; }
        
        public static AlertNotificationDto FromAlert(Alert alert)
        {
            return new AlertNotificationDto
            {
                AlertId = alert.AlertId,
                Title = alert.Title ?? string.Empty,
                Description = alert.Description ?? string.Empty,
                Priority = alert.Priority,
                Status = alert.Status,
                CreatedDate = alert.CreatedDate,
                AssignedToName = alert.AssignedTo?.UserName,
                EquipmentId = alert.EquipmentId,
                EquipmentName = alert.Equipment?.EquipmentModel?.ModelName,
                EquipmentLocation = $"{alert.Equipment?.Building?.BuildingName} - {alert.Equipment?.Room?.RoomName}"
            };
        }
    }
}
