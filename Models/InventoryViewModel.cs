using System.ComponentModel.DataAnnotations;

namespace FEENALOoFINALE.Models
{
    public class InventoryViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public ItemCategory Category { get; set; }

        // This is the amount to add at creation time.
        [Range(0, int.MaxValue, ErrorMessage = "Initial stock must be non-negative.")]
        public int InitialStock { get; set; }

        // Added to fix missing properties for Create.cshtml
        public int? EquipmentTypeId { get; set; }
        public string EquipmentModelName { get; set; }
    }
}