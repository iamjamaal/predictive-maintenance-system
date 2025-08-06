using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using FEENALOoFINALE.ViewModels;

namespace FEENALOoFINALE.Models.ViewModels
{
    public class TimetableManagementViewModel
    {
        public string PageTitle { get; set; } = "Timetable Management";
        public string PageDescription { get; set; } = "Manage semester timetables and track academic progress";

        // Current semester information
        public Semester? CurrentSemester { get; set; }
        public List<Semester> RecentSemesters { get; set; } = new List<Semester>();
        
        // Statistics
        public TimetableStatistics Statistics { get; set; } = new TimetableStatistics();

        // Quick actions
        public List<QuickActionItem> QuickActions { get; set; } = new List<QuickActionItem>();

        // Notifications
        public List<NotificationMessage> Notifications { get; set; } = new List<NotificationMessage>();

        // Progress information
        public SemesterProgressInfo? ProgressInfo { get; set; }
    }

    public class SemesterUploadViewModel
    {
        [Required]
        [Display(Name = "Semester Name")]
        [StringLength(100, ErrorMessage = "Semester name cannot exceed 100 characters")]
        public string SemesterName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Number of Weeks")]
        [Range(1, 52, ErrorMessage = "Number of weeks must be between 1 and 52")]
        public int NumberOfWeeks { get; set; } = 16;

        [Required]
        [Display(Name = "Timetable PDF File")]
        public IFormFile? TimetableFile { get; set; }

        [Display(Name = "Replace Current Active Semester")]
        public bool ReplaceCurrentSemester { get; set; } = false;

        [Display(Name = "Notes")]
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }

        // Computed properties
        public DateTime EndDate => StartDate.AddDays(NumberOfWeeks * 7);
        
        public string FormattedDuration => $"{NumberOfWeeks} weeks ({StartDate:MMM dd, yyyy} - {EndDate:MMM dd, yyyy})";
    }

    public class SemesterEditViewModel
    {
        public int SemesterId { get; set; }

        [Required]
        [Display(Name = "Semester Name")]
        [StringLength(100)]
        public string SemesterName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "Number of Weeks")]
        [Range(1, 52)]
        public int NumberOfWeeks { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Replace Timetable File")]
        public IFormFile? NewTimetableFile { get; set; }

        [Display(Name = "Current File")]
        public string? CurrentFileName { get; set; }

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }

        // Read-only properties
        public DateTime UploadDate { get; set; }
        public string? UploadedByName { get; set; }
        public SemesterProcessingStatus ProcessingStatus { get; set; }
        public string? ProcessingMessage { get; set; }
    }

    public class SemesterProgressDashboardViewModel
    {
        public string PageTitle { get; set; } = "Semester Progress Dashboard";
        public Semester? CurrentSemester { get; set; }
        public SemesterProgressInfo ProgressInfo { get; set; } = new SemesterProgressInfo();
        public List<EquipmentUsageSummary> EquipmentUsage { get; set; } = new List<EquipmentUsageSummary>();
        public List<WeeklyProgressItem> WeeklyProgress { get; set; } = new List<WeeklyProgressItem>();
        public List<FEENALOoFINALE.ViewModels.MaintenanceRecommendation> MaintenanceRecommendations { get; set; } = new List<FEENALOoFINALE.ViewModels.MaintenanceRecommendation>();
        public SemesterStatistics Statistics { get; set; } = new SemesterStatistics();
    }

    public class TimetableStatistics
    {
        public int TotalSemesters { get; set; }
        public int ActiveSemesters { get; set; }
        public int CompletedSemesters { get; set; }
        public double AverageWeeksPerSemester { get; set; }
        public int TotalEquipmentTracked { get; set; }
        public double TotalUsageHours { get; set; }
        public DateTime? LastUploadDate { get; set; }
        public int PendingProcessing { get; set; }
    }

    public class SemesterProgressInfo
    {
        public double ProgressPercentage { get; set; }
        public int WeeksElapsed { get; set; }
        public int WeeksRemaining { get; set; }
        public int DaysRemaining { get; set; }
        public int CurrentWeek { get; set; }
        public SemesterStatus Status { get; set; }
        public string StatusDescription { get; set; } = string.Empty;
        public bool IsNearingCompletion => ProgressPercentage >= 90;
        public bool RequiresNewSemester => Status == SemesterStatus.Completed || (Status == SemesterStatus.Active && ProgressPercentage >= 95);
    }

    public class EquipmentUsageSummary
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public string EquipmentType { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double WeeklyHours { get; set; }
        public double TotalSemesterHours { get; set; }
        public double UtilizationPercentage { get; set; }
        public string RiskLevel { get; set; } = "Low";
        public DateTime? LastMaintenanceDate { get; set; }
        public DateTime? NextMaintenanceDue { get; set; }
        public bool IsHighUsage => WeeklyHours > 40;
        public bool MaintenanceOverdue => NextMaintenanceDue.HasValue && NextMaintenanceDue < DateTime.Now;
    }

    public class WeeklyProgressItem
    {
        public int WeekNumber { get; set; }
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsCurrent { get; set; }
        public double TotalUsageHours { get; set; }
        public int ActiveEquipmentCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class SemesterStatistics
    {
        public double TotalUsageHours { get; set; }
        public double AverageWeeklyUsage { get; set; }
        public int TotalEquipmentUsed { get; set; }
        public int HighUsageEquipmentCount { get; set; }
        public double PredictedMaintenanceCost { get; set; }
        public int MaintenanceTasksGenerated { get; set; }
        public double EfficiencyScore { get; set; }
    }

    public class TimetableListViewModel
    {
        public string PageTitle { get; set; } = "Semester History";
        public List<SemesterListItem> Semesters { get; set; } = new List<SemesterListItem>();
        public TimetableFilterOptions FilterOptions { get; set; } = new TimetableFilterOptions();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
        public string SortBy { get; set; } = "StartDate";
        public string SortDirection { get; set; } = "desc";
    }

    public class SemesterListItem
    {
        public int SemesterId { get; set; }
        public string SemesterName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int NumberOfWeeks { get; set; }
        public SemesterStatus Status { get; set; }
        public double ProgressPercentage { get; set; }
        public bool IsActive { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime UploadDate { get; set; }
        public SemesterProcessingStatus ProcessingStatus { get; set; }
        public int EquipmentCount { get; set; }
        public double TotalUsageHours { get; set; }
        public bool CanEdit { get; set; } = true;
        public bool CanDelete { get; set; } = true;
        public bool CanActivate { get; set; } = true;
    }

    public class TimetableFilterOptions
    {
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> ProcessingStatusOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> YearOptions { get; set; } = new List<SelectListItem>();
        public DateTime? StartDateFrom { get; set; }
        public DateTime? StartDateTo { get; set; }
        public string[]? SelectedStatuses { get; set; }
        public string[]? SelectedProcessingStatuses { get; set; }
        public int? SelectedYear { get; set; }
    }

    public class SemesterDetailsViewModel
    {
        public Semester Semester { get; set; } = null!;
        public SemesterProgressInfo ProgressInfo { get; set; } = new SemesterProgressInfo();
        public List<EquipmentUsageSummary> EquipmentUsage { get; set; } = new List<EquipmentUsageSummary>();
        public List<WeeklyProgressItem> WeeklyProgress { get; set; } = new List<WeeklyProgressItem>();
        public SemesterStatistics Statistics { get; set; } = new SemesterStatistics();
        public List<string> ProcessingLogs { get; set; } = new List<string>();
        public bool CanEdit { get; set; } = true;
        public bool CanDelete { get; set; } = true;
        public bool CanReprocess { get; set; } = true;
    }

    public class TimetableUploadResultViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? SemesterId { get; set; }
        public string? SemesterName { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public TimetableProcessingResult? ProcessingResult { get; set; }
    }

    public class TimetableProcessingResult
    {
        public int EquipmentProcessed { get; set; }
        public int RoomsIdentified { get; set; }
        public double TotalUsageHours { get; set; }
        public List<string> ProcessingMessages { get; set; } = new List<string>();
        public Dictionary<string, double> RoomUsageHours { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> EquipmentUsageHours { get; set; } = new Dictionary<string, double>();
    }

    public class SemesterDeleteViewModel
    {
        public int SemesterId { get; set; }
        public string SemesterName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public int EquipmentUsageCount { get; set; }
        public int MaintenanceTaskCount { get; set; }
        public double TotalUsageHours { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime UploadDate { get; set; }
        public List<string> DeletionWarnings { get; set; } = new List<string>();
        public bool CanDelete { get; set; } = true;
        public string? DeletionBlockReason { get; set; }
    }
}