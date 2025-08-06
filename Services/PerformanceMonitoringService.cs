using System.Diagnostics;

namespace FEENALOoFINALE.Services
{
    public class PerformanceMonitoringService : IPerformanceMonitoringService
    {
        private readonly Dictionary<string, long> _performanceMetrics;
        private readonly Dictionary<string, Stopwatch> _activeTimers;
        private readonly object _lock = new object();

        public PerformanceMonitoringService()
        {
            _performanceMetrics = new Dictionary<string, long>();
            _activeTimers = new Dictionary<string, Stopwatch>();
        }

        public async Task<long> MeasureExecutionTimeAsync(Func<Task> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            await operation();
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }

        public async Task<T> MeasureExecutionTimeAsync<T>(Func<Task<T>> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await operation();
            stopwatch.Stop();
            return result;
        }

        public Task LogPerformanceMetricAsync(string operation, long executionTimeMs)
        {
            lock (_lock)
            {
                _performanceMetrics[operation] = executionTimeMs;
            }
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, object>> GetPerformanceMetricsAsync()
        {
            lock (_lock)
            {
                var metrics = new Dictionary<string, object>(_performanceMetrics.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => (object)kvp.Value));
                return Task.FromResult(metrics);
            }
        }

        public void StartTimer(string operationName)
        {
            lock (_lock)
            {
                if (_activeTimers.ContainsKey(operationName))
                {
                    _activeTimers[operationName].Restart();
                }
                else
                {
                    _activeTimers[operationName] = Stopwatch.StartNew();
                }
            }
        }

        public void StopTimer(string operationName)
        {
            lock (_lock)
            {
                if (_activeTimers.TryGetValue(operationName, out var stopwatch))
                {
                    stopwatch.Stop();
                    _performanceMetrics[operationName] = stopwatch.ElapsedMilliseconds;
                    _activeTimers.Remove(operationName);
                }
            }
        }

        public void TrackOperation(string operationName, TimeSpan duration, bool success = true)
        {
            lock (_lock)
            {
                _performanceMetrics[operationName] = (long)duration.TotalMilliseconds;
            }
        }

        public Task<PerformanceReport> GetReportAsync()
        {
            lock (_lock)
            {
                var report = new PerformanceReport
                {
                    OperationStats = new Dictionary<string, long>(_performanceMetrics),
                    TotalOperations = _performanceMetrics.Count,
                    SlowOperations = _performanceMetrics
                        .Where(m => m.Value > 1000) // Operations taking more than 1 second
                        .Select(m => new SlowOperation 
                        { 
                            OperationName = m.Key, 
                            ExecutionTime = m.Value,
                            Timestamp = DateTime.UtcNow
                        })
                        .ToList(),
                    AverageResponseTime = _performanceMetrics.Count > 0 ? _performanceMetrics.Values.Average() : 0,
                    LastUpdated = DateTime.UtcNow,
                    GeneratedAt = DateTime.UtcNow,
                    ActiveTimers = _activeTimers.Keys.ToList(),
                    SystemMetrics = new Dictionary<string, object>
                    {
                        ["TotalQueries"] = _performanceMetrics.Count,
                        ["ActiveTimers"] = _activeTimers.Count
                    }
                };
                return Task.FromResult(report);
            }
        }
    }
}
