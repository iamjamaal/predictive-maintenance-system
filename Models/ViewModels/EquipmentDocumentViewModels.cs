using System.ComponentModel.DataAnnotations;

namespace FEENALOoFINALE.Models.ViewModels
{
    public class EquipmentWithDocumentsViewModel
    {
        public Equipment Equipment { get; set; } = new Equipment();

        [Display(Name = "Manufacturer Documents")]
        public List<IFormFile>? DocumentFiles { get; set; }

        [Display(Name = "Document Types")]
        public List<string>? DocumentTypes { get; set; }

        public bool ProcessDocumentsAutomatically { get; set; } = true;

        // For displaying existing documents during edit
        public List<ManufacturerDocument>? ExistingDocuments { get; set; }
    }

    public class DocumentUploadResult
    {
        public bool Success { get; set; }
        public List<string> UploadedFiles { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public int ExtractedRecommendationsCount { get; set; }
    }
}
