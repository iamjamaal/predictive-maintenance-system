namespace FEENALOoFINALE.Models
{
    public class DashboardViewModel
    {
        public int TotalEquipment { get; set; }
        public int ActiveMaintenanceTasks { get; set; }
        public int LowStockItems { get; set; }
        public int CriticalAlerts { get; set; }
        public int OverdueMaintenances { get; set; }
        public int EquipmentNeedingAttention { get; set; }
        public List<Alert>? RecentAlerts { get; set; }
        public List<MaintenanceTask>? UpcomingMaintenances { get; set; }
        public List<Equipment>? CriticalEquipment { get; set; }
        public List<QuickAction>? SuggestedActions { get; set; }
        public required List<EquipmentStatusCount> EquipmentStatus { get; set; }
        
        // Enhanced workflow status properties
        public int ActiveAlerts { get; set; }
        public int PendingTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int ResolvedToday { get; set; }
        public int AutoGeneratedTasks { get; set; }
        
        // Additional properties for filtering
        public int TotalRecordsBeforeFilter { get; set; }
        public int TotalRecordsAfterFilter { get; set; }
        public Dictionary<string, object> FilteredAnalytics { get; set; } = new();
    }

    public class EquipmentStatusCount
    {
        public EquipmentStatus Status { get; set; }
        public int Count { get; set; }
    }

    public class QuickAction
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? RouteValue { get; set; }
        public string Priority { get; set; } = "normal"; // normal, urgent, critical
        public string BadgeText { get; set; } = string.Empty;
    }
}