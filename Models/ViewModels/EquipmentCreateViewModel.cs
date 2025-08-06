using System.ComponentModel.DataAnnotations;

namespace FEENALOoFINALE.Models.ViewModels
{
    public class EquipmentCreateViewModel
    {
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

        [Display(Name = "Status")]
        public EquipmentStatus Status { get; set; } = EquipmentStatus.Active;

        [Display(Name = "Notes")]
        [StringLength(1000)]
        public string? Notes { get; set; }

        // Document upload properties
        [Display(Name = "Manufacturer Documents")]
        public List<IFormFile>? ManufacturerDocuments { get; set; }

        [Display(Name = "Document Types")]
        public List<string>? DocumentTypes { get; set; }

        [Display(Name = "Process Documents Automatically")]
        public bool ProcessDocumentsAutomatically { get; set; } = true;

        // Validation for document uploads
        public const int MaxFileSize = 10 * 1024 * 1024; // 10MB
        public static readonly string[] AllowedFileTypes = { ".pdf", ".doc", ".docx", ".txt" };
        public static readonly string[] AllowedContentTypes = 
        { 
            "application/pdf", 
            "application/msword", 
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "text/plain"
        };

        public static readonly string[] DocumentTypeOptions = 
        {
            "User Manual",
            "Service Manual", 
            "Installation Guide",
            "Maintenance Schedule",
            "Technical Specifications",
            "Safety Guidelines",
            "Troubleshooting Guide",
            "Parts Catalog",
            "Warranty Information",
            "Other"
        };
    }
}
