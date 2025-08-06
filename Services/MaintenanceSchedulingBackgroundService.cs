namespace FEENALOoFINALE.Services
{
    public class MaintenanceSchedulingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MaintenanceSchedulingBackgroundService> _logger;

        public MaintenanceSchedulingBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<MaintenanceSchedulingBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait 6 minutes before first run to reduce startup load
            await Task.Delay(TimeSpan.FromMinutes(6), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Maintenance scheduling background service running at: {time}", DateTimeOffset.Now);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var schedulingService = scope.ServiceProvider.GetRequiredService<MaintenanceSchedulingService>();
                        
                        var tasksCreated = await schedulingService.ScheduleRoutineMaintenanceAsync();
                        
                        if (tasksCreated > 0)
                        {
                            _logger.LogInformation("Created {count} new maintenance tasks", tasksCreated);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing maintenance scheduling background service");
                }

                // Run every 6 hours
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
    }
}
