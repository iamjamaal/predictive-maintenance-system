using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FEENALOoFINALE.Models
{
    public class ManufacturerDocument
    {
        [Key]
        public int DocumentId { get; set; }

        // Link to equipment model instead of individual equipment for sharing across same models
        [Required]
        public int EquipmentModelId { get; set; }
        public EquipmentModel? EquipmentModel { get; set; }

        // Keep equipment reference for backward compatibility and tracking upload source
        public int? UploadedByEquipmentId { get; set; }
        public Equipment? UploadedByEquipment { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public long FileSize { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? DocumentType { get; set; } // Manual, Specifications, Service Guide, etc.

        public bool IsProcessed { get; set; } = false;

        public DateTime? ProcessedDate { get; set; }

        [StringLength(1000)]
        public string? ProcessingNotes { get; set; }

        public string? ExtractedText { get; set; }

        // Navigation property
        public ICollection<MaintenanceRecommendation>? MaintenanceRecommendations { get; set; }
    }

    public class MaintenanceRecommendation
    {
        [Key]
        public int RecommendationId { get; set; }

        public int? DocumentId { get; set; }
        public ManufacturerDocument? Document { get; set; }

        // Link to equipment model for recommendations that apply to all equipment of the same model
        public int? EquipmentModelId { get; set; }
        public EquipmentModel? EquipmentModel { get; set; }

        // Keep equipment ID for backward compatibility and specific equipment recommendations
        public int? EquipmentId { get; set; }
        public Equipment? Equipment { get; set; }

        [Required]
        [StringLength(100)]
        public string RecommendationType { get; set; } = string.Empty; // Preventive, Scheduled, Condition-based

        [Required]
        public string RecommendationText { get; set; } = string.Empty;

        public int? IntervalDays { get; set; } // Recommended maintenance interval

        [StringLength(50)]
        public string? Priority { get; set; } // Low, Medium, High, Critical

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? LastAppliedDate { get; set; }

        [StringLength(500)]
        public string? Source { get; set; } // Which part of document this came from

        public decimal? ConfidenceScore { get; set; } // How confident we are in this extraction (0-1)
    }
}
