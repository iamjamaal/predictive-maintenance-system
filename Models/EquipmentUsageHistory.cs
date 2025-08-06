using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace FEENALOoFINALE.Models
{
    public class EquipmentUsageHistory
    {
        [Key]
        public int Id { get; set; }
        public int EquipmentId { get; set; }
        public Equipment Equipment { get; set; }
        public DateTime WeekStart { get; set; }
        public double UsageHours { get; set; }
    }
}