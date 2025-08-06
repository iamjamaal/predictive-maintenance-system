using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FEENALOoFINALE.Models
{
    public class Equipment
    {
        [Key]
        public int EquipmentId { get; set; }
        
        // Remove Name and Model properties
        // public required string Name { get; set; }
        // public required string Model { get; set; }
        
        // New foreign keys and navigation properties
        public int EquipmentTypeId { get; set; }
        public EquipmentType? EquipmentType { get; set; }

        [Required]
        public int EquipmentModelId { get; set; }
        public EquipmentModel? EquipmentModel { get; set; }
        
        // Add a non-mapped property for model name input
        [Display(Name = "Equipment Model")]
        [NotMapped]
        public string? EquipmentModelName { get; set; }
        
        // Remove Location property
        // public required string Location { get; set; }
        
        // New foreign keys and navigation properties for location
        public int BuildingId { get; set; }
        public Building? Building { get; set; }

        public int RoomId { get; set; }
        public Room? Room { get; set; }

        public DateTime? InstallationDate { get; set; }
        public int ExpectedLifespanMonths { get; set; }
        public EquipmentStatus Status { get; set; }
        public string? Notes { get; set; }

        // New property for average weekly usage hours
        public double? AverageWeeklyUsageHours { get; set; }

        // Navigation properties
        public ICollection<MaintenanceLog>? MaintenanceLogs { get; set; }
        public ICollection<FailurePrediction>? FailurePredictions { get; set; }
        public ICollection<Alert>? Alerts { get; set; }
        public ICollection<ManufacturerDocument>? ManufacturerDocuments { get; set; }
        public ICollection<MaintenanceRecommendation>? MaintenanceRecommendations { get; set; }
    }

    public enum EquipmentStatus
    {
        Active,
        Inactive,
        Retired
    }
}
