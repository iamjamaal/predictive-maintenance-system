namespace FEENALOoFINALE.Services
{
    public interface IPerformanceMonitoringService
    {
        Task<long> MeasureExecutionTimeAsync(Func<Task> operation);
        Task<T> MeasureExecutionTimeAsync<T>(Func<Task<T>> operation);
        Task LogPerformanceMetricAsync(string operation, long executionTimeMs);
        Task<Dictionary<string, object>> GetPerformanceMetricsAsync();
        
        // Additional methods for PerformanceController
        void StartTimer(string operationName);
        void StopTimer(string operationName);
        Task<PerformanceReport> GetReportAsync();
        
        // Method for tracking operations
        void TrackOperation(string operationName, TimeSpan duration, bool success = true);
    }
}
