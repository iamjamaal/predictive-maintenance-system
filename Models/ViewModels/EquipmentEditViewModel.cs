using System.ComponentModel.DataAnnotations;

namespace FEENALOoFINALE.Models.ViewModels
{
    public class EquipmentEditViewModel
    {
        public int EquipmentId { get; set; }

        // Equipment properties
        [Required]
        [Display(Name = "Equipment Type")]
        public int EquipmentTypeId { get; set; }

        [Required]
        [Display(Name = "Equipment Model")]
        [StringLength(100)]
        public string EquipmentModelName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Building")]
        public int BuildingId { get; set; }

        [Required]
        [Display(Name = "Room")]
        public int RoomId { get; set; }

        [Display(Name = "Installation Date")]
        public DateTime? InstallationDate { get; set; }

        [Required]
        [Display(Name = "Expected Lifespan (Months)")]
        [Range(1, 600, ErrorMessage = "Expected lifespan must be between 1 and 600 months")]
        public int ExpectedLifespanMonths { get; set; }

        [Display(Name = "Average Weekly Usage Hours")]
        [Range(0, 168, ErrorMessage = "Weekly usage hours must be between 0 and 168")]
        public double? AverageWeeklyUsageHours { get; set; }

        [Required]
        [Display(Name = "Status")]
        public EquipmentStatus Status { get; set; }

        [Display(Name = "Notes")]
        [StringLength(1000)]
        public string? Notes { get; set; }

        // Document management
        [Display(Name = "Upload New Documents")]
        public List<IFormFile>? ManufacturerDocuments { get; set; }

        // Current state information
        public string CurrentLocation { get; set; } = string.Empty;
        public List<DocumentInfo>? CurrentDocuments { get; set; }
        public int TotalMaintenanceTasks { get; set; }
        public int ActiveAlerts { get; set; }
        public string WeeklyUsageHours { get; set; } = "0.0";
        public DateTime? LastMaintenanceDate { get; set; }
        public DateTime? NextMaintenanceDue { get; set; }

        // Navigation properties for display
        public string? EquipmentTypeName { get; set; }
        public string? BuildingName { get; set; }
        public string? RoomName { get; set; }
    }

    public class DocumentInfo
    {
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted
        {
            get
            {
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024:F1} KB";
                return $"{FileSizeBytes / (1024 * 1024):F1} MB";
            }
        }
        public DateTime UploadDate { get; set; }
        public string DocumentType { get; set; } = string.Empty;
    }
}
