using FEENALOoFINALE.Models;

namespace FEENALOoFINALE.Models
{
    // Advanced Analytics View Model for Step 9
    public class AdvancedAnalyticsViewModel
    {
        public List<EquipmentPerformanceMetrics> EquipmentPerformance { get; set; } = new();
        public List<PredictiveAnalyticsData> PredictiveInsights { get; set; } = new();
        public List<KPIProgressIndicator> KPIProgress { get; set; } = new();
        public EquipmentHeatmapData HeatmapData { get; set; } = new();
        public List<MaintenanceTrendData> MaintenanceTrends { get; set; } = new();
        public List<CostAnalysisData> CostAnalysis { get; set; } = new();
        public Dictionary<string, object> RealTimeMetrics { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    // Equipment Performance Metrics for detailed analysis
    public class EquipmentPerformanceMetrics
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public string EquipmentType { get; set; } = string.Empty;
        public string Building { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public EquipmentStatus Status { get; set; }
        
        // Performance Indicators
        public double UptimePercentage { get; set; }
        public double EfficiencyScore { get; set; }
        public double MaintenanceCostPerMonth { get; set; }
        public int FailureCount { get; set; }
        public double MeanTimeBetweenFailures { get; set; } // In days
        public double MeanTimeToRepair { get; set; } // In hours
        
        // Predictive Indicators
        public PredictionStatus PredictedRisk { get; set; }
        public DateTime? NextMaintenanceDue { get; set; }
        public double HealthScore { get; set; } // 0-100
        
        // Financial Metrics
        public decimal TotalMaintenanceCost { get; set; }
        public decimal AverageDailyCost { get; set; }
        public decimal EstimatedReplacementCost { get; set; }
        
        // Usage Metrics
        public int TotalMaintenanceHours { get; set; }
        public DateTime LastMaintenanceDate { get; set; }
        public int DaysSinceLastMaintenance { get; set; }
    }

    // Predictive Analytics Data for AI-driven insights
    public class PredictiveAnalyticsData
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public PredictionStatus RiskLevel { get; set; }
        public DateTime PredictedFailureDate { get; set; }
        public double ConfidenceScore { get; set; } // 0-1
        public string PredictionReason { get; set; } = string.Empty;
        public List<string> RecommendedActions { get; set; } = new();
        public double FinancialImpact { get; set; }
        public int DaysUntilPredictedFailure { get; set; }
        public Dictionary<string, double> FactorContributions { get; set; } = new();
    }

    // KPI Progress Indicators with targets
    public class KPIProgressIndicator
    {
        public string KPIName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // "Equipment", "Maintenance", "Financial", "Operational"
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public double PreviousValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty; // "up", "down", "stable"
        public double PercentageChange { get; set; }
        public double ProgressPercentage { get; set; }
        public string Status { get; set; } = string.Empty; // "on-track", "warning", "critical"
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }

    // Equipment Heatmap Data for visual building overview
    public class EquipmentHeatmapData
    {
        public List<BuildingHeatmapData> Buildings { get; set; } = new();
        public Dictionary<string, int> StatusCounts { get; set; } = new();
        public Dictionary<string, string> StatusColors { get; set; } = new();
        public double OverallHealthScore { get; set; }
        public int TotalEquipment { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    public class BuildingHeatmapData
    {
        public int BuildingId { get; set; }
        public string BuildingName { get; set; } = string.Empty;
        public List<RoomHeatmapData> Rooms { get; set; } = new();
        public double BuildingHealthScore { get; set; }
        public int TotalEquipment { get; set; }
        public Dictionary<string, int> StatusCounts { get; set; } = new();
    }

    public class RoomHeatmapData
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public List<EquipmentHeatmapItem> Equipment { get; set; } = new();
        public double RoomHealthScore { get; set; }
        public string DominantStatus { get; set; } = string.Empty;
    }

    public class EquipmentHeatmapItem
    {
        public int EquipmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public EquipmentStatus Status { get; set; }
        public double HealthScore { get; set; }
        public PredictionStatus RiskLevel { get; set; }
        public string StatusColor { get; set; } = string.Empty;
        public string ToolTip { get; set; } = string.Empty;
    }

    // Maintenance Trend Data for trending analysis
    public class MaintenanceTrendData
    {
        public DateTime Date { get; set; }
        public int PreventiveMaintenanceCount { get; set; }
        public int CorrectiveMaintenanceCount { get; set; }
        public int InspectionMaintenanceCount { get; set; }
        public decimal TotalCost { get; set; }
        public double AverageDowntime { get; set; }
        public int EquipmentAffected { get; set; }
        public double EfficiencyScore { get; set; }
    }

    // Cost Analysis Data for financial insights
    public class CostAnalysisData
    {
        public string Category { get; set; } = string.Empty; // "Equipment Type", "Building", "Maintenance Type"
        public string CategoryValue { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public decimal AverageCost { get; set; }
        public int MaintenanceCount { get; set; }
        public double CostPercentage { get; set; }
        public decimal ProjectedCost { get; set; }
        public string Trend { get; set; } = string.Empty; // "increasing", "decreasing", "stable"
        
        // Additional properties for dashboard analytics
        public decimal TotalMaintenanceCost { get; set; }
        public decimal PreventiveCost { get; set; }
        public decimal ReactiveCost { get; set; }
        public decimal CostSavings { get; set; }
    }

    // Real-Time Dashboard Update Model
    public class RealTimeDashboardUpdate
    {
        public Dictionary<string, object> Metrics { get; set; } = new();
        public List<object> AlertUpdates { get; set; } = new();
        public List<object> EquipmentUpdates { get; set; } = new();
        public Dictionary<string, object> ChartUpdates { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string UpdateType { get; set; } = string.Empty; // "metrics", "alerts", "equipment", "charts"
    }

    // Report Generation Models
    public class ReportTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TemplateType { get; set; } = string.Empty; // "dashboard", "equipment", "maintenance", "financial"
        public string Configuration { get; set; } = string.Empty; // JSON configuration
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class ReportSchedule
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ReportTemplateId { get; set; }
        public ReportTemplate? ReportTemplate { get; set; }
        public string Schedule { get; set; } = string.Empty; // Cron expression
        public string Recipients { get; set; } = string.Empty; // JSON array of email addresses
        public string OutputFormat { get; set; } = "PDF"; // PDF, Excel, CSV
        public bool IsActive { get; set; } = true;
        public DateTime? LastRun { get; set; }
        public DateTime? NextRun { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // Notification Models
    public class NotificationTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "email", "sms", "push", "webhook"
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Conditions { get; set; } = string.Empty; // JSON conditions
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class EscalationRule
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TriggerConditions { get; set; } = string.Empty; // JSON conditions
        public int EscalationLevel { get; set; }
        public int DelayMinutes { get; set; }
        public string Recipients { get; set; } = string.Empty; // JSON array
        public string NotificationMethod { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class AlertHistory
    {
        public int Id { get; set; }
        public int AlertId { get; set; }
        public Alert? Alert { get; set; }
        public string Action { get; set; } = string.Empty; // "created", "acknowledged", "escalated", "resolved"
        public string ActionBy { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; } = DateTime.Now;
    }

    // Enhanced Dashboard View Model for Step 9
    public class Step9DashboardViewModel : EnhancedDashboardViewModel
    {
        // Advanced Analytics
        public AdvancedAnalyticsViewModel AdvancedAnalytics { get; set; } = new();
        
        // Real-Time Features
        public bool RealTimeEnabled { get; set; } = true;
        public int RefreshInterval { get; set; } = 30; // seconds
        
        // Reporting Features
        public List<ReportTemplate> AvailableReports { get; set; } = new();
        public List<ReportSchedule> ScheduledReports { get; set; } = new();
        
        // Notification Features
        public List<Alert> RecentNotifications { get; set; } = new();
        public int UnreadNotificationCount { get; set; }
        public bool NotificationsEnabled { get; set; } = true;
        
        // Performance Metrics
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
        public DateTime LastDataRefresh { get; set; } = DateTime.Now;
    }

    // Custom Report Template Models for Advanced Reporting
    public class CustomReportTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public bool IsPublic { get; set; } = false;
        public List<ReportSection> Sections { get; set; } = new();
        public ReportFormat DefaultFormat { get; set; } = ReportFormat.PDF;
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    public class ReportSection
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReportDataType DataType { get; set; }
        public int Order { get; set; }
        public bool IncludeCharts { get; set; } = true;
        public bool IncludeTable { get; set; } = true;
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    public enum ReportDataType
    {
        Equipment,
        Maintenance,
        Alerts,
        Inventory,
        Analytics,
        Performance,
        Costs,
        Trends,
        Predictions,
        KPIs
    }

    public enum ReportFormat
    {
        PDF,
        Excel,
        CSV,
        JSON
    }

    public enum ScheduleFrequency
    {
        Daily,
        Weekly,
        Monthly,
        Quarterly,
        Yearly
    }
}
