using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FEENALOoFINALE.Models
{
    public class Semester
    {
        [Key]
        public int SemesterId { get; set; }

        [Required]
        [StringLength(100)]
        public string SemesterName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "Number of Weeks")]
        [Range(1, 52, ErrorMessage = "Semester weeks must be between 1 and 52")]
        public int NumberOfWeeks { get; set; }

        [Display(Name = "End Date")]
        public DateTime EndDate => StartDate.AddDays(NumberOfWeeks * 7);

        [Display(Name = "Timetable File Path")]
        public string? TimetableFilePath { get; set; }

        [Display(Name = "Original File Name")]
        public string? OriginalFileName { get; set; }

        [Display(Name = "File Size (bytes)")]
        public long? FileSizeBytes { get; set; }

        [Display(Name = "Upload Date")]
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Uploaded By")]
        public string? UploadedByUserId { get; set; }

        [ForeignKey("UploadedByUserId")]
        public virtual User? UploadedBy { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Processing Status")]
        public SemesterProcessingStatus ProcessingStatus { get; set; } = SemesterProcessingStatus.Pending;

        [Display(Name = "Processing Message")]
        public string? ProcessingMessage { get; set; }

        [Display(Name = "Equipment Usage Data")]
        public string? EquipmentUsageDataJson { get; set; }

        [Display(Name = "Total Equipment Hours")]
        public double TotalEquipmentHours { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Modified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<SemesterEquipmentUsage> SemesterEquipmentUsages { get; set; } = new List<SemesterEquipmentUsage>();

        // Computed properties
        [NotMapped]
        [Display(Name = "Progress Percentage")]
        public double ProgressPercentage
        {
            get
            {
                if (!IsActive) return 100;
                
                var totalDays = (EndDate - StartDate).TotalDays;
                var elapsedDays = (DateTime.Now - StartDate).TotalDays;
                
                if (elapsedDays < 0) return 0;
                if (elapsedDays > totalDays) return 100;
                
                return Math.Round((elapsedDays / totalDays) * 100, 1);
            }
        }

        [NotMapped]
        [Display(Name = "Days Remaining")]
        public int DaysRemaining
        {
            get
            {
                if (!IsActive) return 0;
                var remaining = (EndDate - DateTime.Now).TotalDays;
                return remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
            }
        }

        [NotMapped]
        [Display(Name = "Status")]
        public SemesterStatus Status
        {
            get
            {
                if (!IsActive) return SemesterStatus.Inactive;
                if (DateTime.Now < StartDate) return SemesterStatus.Upcoming;
                if (DateTime.Now > EndDate) return SemesterStatus.Completed;
                return SemesterStatus.Active;
            }
        }

        [NotMapped]
        [Display(Name = "Weeks Elapsed")]
        public int WeeksElapsed
        {
            get
            {
                if (DateTime.Now < StartDate) return 0;
                var elapsedDays = (DateTime.Now - StartDate).TotalDays;
                return (int)Math.Floor(elapsedDays / 7);
            }
        }

        [NotMapped]
        [Display(Name = "Current Week")]
        public int CurrentWeek
        {
            get
            {
                var weeksElapsed = WeeksElapsed;
                return weeksElapsed >= NumberOfWeeks ? NumberOfWeeks : weeksElapsed + 1;
            }
        }
    }

    public class SemesterEquipmentUsage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SemesterId { get; set; }

        [ForeignKey("SemesterId")]
        public virtual Semester Semester { get; set; } = null!;

        [Required]
        public int EquipmentId { get; set; }

        [ForeignKey("EquipmentId")]
        public virtual Equipment Equipment { get; set; } = null!;

        [Display(Name = "Weekly Usage Hours")]
        public double WeeklyUsageHours { get; set; }

        [Display(Name = "Total Semester Hours")]
        public double TotalSemesterHours => WeeklyUsageHours * (Semester?.NumberOfWeeks ?? 0);

        [Display(Name = "Room Name")]
        public string? RoomName { get; set; }

        [Display(Name = "Usage Pattern")]
        public string? UsagePatternJson { get; set; }

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public enum SemesterProcessingStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        RequiresReview
    }

    public enum SemesterStatus
    {
        Upcoming,
        Active,
        Completed,
        Inactive
    }
}