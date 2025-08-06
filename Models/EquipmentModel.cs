using FEENALOoFINALE.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FEENALOoFINALE.Models
{
    public class EquipmentModel
    {
        public int EquipmentModelId { get; set; }

        [Required]
        public int EquipmentTypeId { get; set; }

        [Required]
        public string ModelName { get; set; } = string.Empty;

        // Navigation property
        public virtual EquipmentType? EquipmentType { get; set; }

        public virtual ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
        
        // Manufacturer documents shared across all equipment of this model
        public virtual ICollection<ManufacturerDocument> ManufacturerDocuments { get; set; } = new List<ManufacturerDocument>();
    }
}