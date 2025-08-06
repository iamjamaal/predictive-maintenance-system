using FEENALOoFINALE.Models;

namespace FEENALOoFINALE.Services
{
    public interface IPredictiveAnalyticsService
    {
        Task UpdateModelWithMaintenanceLogAsync(MaintenanceLog log);
    }
}
