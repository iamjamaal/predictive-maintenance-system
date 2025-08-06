using FEENALOoFINALE.Models;
using FEENALOoFINALE.Models.ViewModels;

namespace FEENALOoFINALE.Models.ViewModels
{
    // Analytics Dashboard ViewModels
    public class AnalyticsDashboardViewModel
    {
        public string PageTitle { get; set; } = "";
        public List<TrendDataPoint> EquipmentTrends { get; set; } = new();
        public List<TrendDataPoint> MaintenanceTrends { get; set; } = new();
        public List<TrendDataPoint> AlertTrends { get; set; } = new();
        public CostAnalysisData CostAnalysis { get; set; } = new();
        public EfficiencyMetricsData EfficiencyMetrics { get; set; } = new();
        public string PeriodFilter { get; set; } = "last30days";
    }

    public class EfficiencyMetricsData
    {
        public double EquipmentUtilizationRate { get; set; }
        public double MaintenanceComplianceRate { get; set; }
        public double SystemReliability { get; set; }
        public double OverallEfficiency { get; set; }
    }

    // Performance Dashboard ViewModels
    public class PerformanceDashboardViewModel
    {
        public string PageTitle { get; set; } = "";
        public double OverallEfficiency { get; set; }
        public List<EquipmentUtilizationData> EquipmentUtilization { get; set; } = new();
        public double MaintenanceEfficiency { get; set; }
        public DowntimeAnalysisData? DowntimeAnalysis { get; set; }
        public List<KPIMetric> KPIMetrics { get; set; } = new();
    }

    public class EquipmentUtilizationData
    {
        public string EquipmentName { get; set; } = "";
        public string Location { get; set; } = "";
        public double UtilizationRate { get; set; }
        public string Status { get; set; } = "";
    }

    public class DowntimeAnalysisData
    {
        public double TotalDowntimeHours { get; set; }
        public double AverageDowntimeHours { get; set; }
        public double PlannedDowntimeHours { get; set; }
        public double UnplannedDowntimeHours { get; set; }
        public int DowntimeIncidents { get; set; }
    }

    public class KPIMetric
    {
        public string MetricName { get; set; } = "";
        public string Description { get; set; } = "";
        public double Value { get; set; }
        public string Unit { get; set; } = "";
        public bool IsGood { get; set; }
    }

    // Predictions Dashboard ViewModels
    public class PredictionsDashboardViewModel
    {
        public string PageTitle { get; set; } = "";
        public List<FailurePredictionData> FailurePredictions { get; set; } = new();
        public List<MaintenanceForecast> MaintenanceForecasts { get; set; } = new();
        public List<RiskAssessment> RiskAssessments { get; set; } = new();
        public List<RecommendedAction> RecommendedActions { get; set; } = new();
        public PredictionAccuracyData PredictionAccuracy { get; set; } = new();
    }

    public class FailurePredictionData
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; } = "";
        public string EquipmentType { get; set; } = "";
        public string Location { get; set; } = "";
        public DateTime? PredictedFailureDate { get; set; }
        public double Confidence { get; set; }
        public string RiskLevel { get; set; } = "";
    }

    public class MaintenanceForecast
    {
        public DateTime ScheduledDate { get; set; }
        public string MaintenanceType { get; set; } = "";
        public string EquipmentName { get; set; } = "";
        public decimal EstimatedCost { get; set; }
    }

    public class RiskAssessment
    {
        public string RiskCategory { get; set; } = "";
        public string Description { get; set; } = "";
        public string ImpactLevel { get; set; } = "";
        public double Probability { get; set; }
    }

    public class RecommendedAction
    {
        public int RecommendationId { get; set; }
        public string ActionTitle { get; set; } = "";
        public string Description { get; set; } = "";
        public string EquipmentName { get; set; } = "";
        public string Priority { get; set; } = "";
    }

    public class PredictionAccuracyData
    {
        public double Accuracy { get; set; }
        public int SampleSize { get; set; }
    }

    // Report ViewModels
    public class EquipmentReportViewModel
    {
        public string PageTitle { get; set; } = "";
        public DateTime GeneratedDate { get; set; }
        public int TotalEquipment { get; set; }
        public int ActiveEquipment { get; set; }
        public int InactiveEquipment { get; set; }
        public int RetiredEquipment { get; set; }
        public double AverageAge { get; set; }
        public decimal TotalMaintenanceCost { get; set; }
        public List<EquipmentSummaryItem> EquipmentList { get; set; } = new();
    }

    public class EquipmentSummaryItem
    {
        public int EquipmentId { get; set; }
        public string TypeName { get; set; } = "";
        public string ModelName { get; set; } = "";
        public string Location { get; set; } = "";
        public EquipmentStatus Status { get; set; }
        public DateTime? InstallationDate { get; set; }
        public DateTime? LastMaintenanceDate { get; set; }
        public int ActiveAlertsCount { get; set; }
    }

    public class MaintenanceReportViewModel
    {
        public string PageTitle { get; set; } = "";
        public DateTime GeneratedDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalMaintenanceTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int InProgressTasks { get; set; }
        public decimal TotalCost { get; set; }
        public decimal PreventiveCost { get; set; }
        public decimal ReactiveCost { get; set; }
        public double AverageCostPerTask { get; set; }
        public double AverageCompletionTime { get; set; }
        public List<MaintenanceSummaryItem> MaintenanceList { get; set; } = new();
        public List<MaintenanceTypeBreakdown> TypeBreakdown { get; set; } = new();
    }

    public class MaintenanceSummaryItem
    {
        public int LogId { get; set; }
        public string EquipmentName { get; set; } = "";
        public string Location { get; set; } = "";
        public MaintenanceType MaintenanceType { get; set; }
        public MaintenanceStatus Status { get; set; }
        public DateTime LogDate { get; set; }
        public decimal Cost { get; set; }
        public string Technician { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class MaintenanceTypeBreakdown
    {
        public MaintenanceType Type { get; set; }
        public int Count { get; set; }
        public decimal TotalCost { get; set; }
        public double AverageCost { get; set; }
    }
}
