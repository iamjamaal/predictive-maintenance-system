using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FEENALOoFINALE.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AlertApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AlertApiController> _logger;

        public AlertApiController(ApplicationDbContext context, ILogger<AlertApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all alerts with optional filtering
        /// </summary>
        /// <param name="priority">Filter by alert priority</param>
        /// <param name="status">Filter by alert status</param>
        /// <param name="equipmentId">Filter by equipment ID</param>
        /// <param name="startDate">Filter by start date</param>
        /// <param name="endDate">Filter by end date</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 20)</param>
        [HttpGet]
        public async Task<IActionResult> GetAlerts(
            [FromQuery] AlertPriority? priority = null,
            [FromQuery] AlertStatus? status = null,
            [FromQuery] int? equipmentId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Alerts
                    .Include(a => a.Equipment)
                        .ThenInclude(e => e.Room)
                            .ThenInclude(r => r.Building)
                    .AsQueryable();

                // Apply filters
                if (priority.HasValue)
                    query = query.Where(a => a.Priority == priority.Value);

                if (status.HasValue)
                    query = query.Where(a => a.Status == status.Value);

                if (equipmentId.HasValue)
                    query = query.Where(a => a.EquipmentId == equipmentId.Value);

                if (startDate.HasValue)
                    query = query.Where(a => a.CreatedDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.CreatedDate <= endDate.Value);

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Apply pagination
                var alerts = await query
                    .OrderByDescending(a => a.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new
                    {
                        a.AlertId,
                        a.Title,
                        a.Description,
                        a.Priority,
                        a.Status,
                        a.CreatedDate,
                        Equipment = a.Equipment != null ? new 
                        { 
                            a.Equipment.Id, 
                            a.Equipment.Name, 
                            a.Equipment.SerialNumber,
                            Room = new { a.Equipment.Room.Id, a.Equipment.Room.RoomNumber, a.Equipment.Room.RoomName },
                            Building = new { a.Equipment.Room.Building.Id, a.Equipment.Room.Building.BuildingName }
                        } : null
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = alerts,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alerts");
                return StatusCode(500, new { message = "An error occurred while retrieving alerts" });
            }
        }

        /// <summary>
        /// Get alert by ID
        /// </summary>
        /// <param name="id">Alert ID</param>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAlert(int id)
        {
            try
            {
                var alert = await _context.Alerts
                    .Include(a => a.Equipment)
                        .ThenInclude(e => e.Room)
                            .ThenInclude(r => r.Building)
                    .Where(a => a.AlertId == id)
                    .Select(a => new
                    {
                        a.AlertId,
                        a.Title,
                        a.Description,
                        a.Priority,
                        a.Status,
                        a.CreatedDate,
                        Equipment = a.Equipment != null ? new 
                        { 
                            a.Equipment.Id, 
                            a.Equipment.Name, 
                            a.Equipment.SerialNumber,
                            a.Equipment.Status,
                            Room = new { a.Equipment.Room.Id, a.Equipment.Room.RoomNumber, a.Equipment.Room.RoomName },
                            Building = new { a.Equipment.Room.Building.Id, a.Equipment.Room.Building.BuildingName }
                        } : null
                    })
                    .FirstOrDefaultAsync();

                if (alert == null)
                {
                    return NotFound(new { message = $"Alert with ID {id} not found" });
                }

                return Ok(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alert with ID {AlertId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving alert" });
            }
        }

        /// <summary>
        /// Create new alert
        /// </summary>
        /// <param name="alertDto">Alert data</param>
        [HttpPost]
        public async Task<IActionResult> CreateAlert([FromBody] CreateAlertDto alertDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate equipment ID if provided
                if (alertDto.EquipmentId.HasValue)
                {
                    var equipment = await _context.Equipment.FindAsync(alertDto.EquipmentId.Value);
                    if (equipment == null)
                    {
                        return BadRequest(new { message = "Invalid equipment ID" });
                    }
                }

                var alert = new Alert
                {
                    Title = alertDto.Title,
                    Description = alertDto.Description,
                    Priority = alertDto.Priority,
                    Status = AlertStatus.Open,
                    CreatedDate = DateTime.Now,
                    EquipmentId = alertDto.EquipmentId
                };

                _context.Alerts.Add(alert);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetAlert), new { id = alert.AlertId }, new { alert.AlertId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating alert");
                return StatusCode(500, new { message = "An error occurred while creating alert" });
            }
        }

        /// <summary>
        /// Update alert
        /// </summary>
        /// <param name="id">Alert ID</param>
        /// <param name="alertDto">Updated alert data</param>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAlert(int id, [FromBody] UpdateAlertDto alertDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var alert = await _context.Alerts.FindAsync(id);
                if (alert == null)
                {
                    return NotFound(new { message = $"Alert with ID {id} not found" });
                }

                // Validate equipment ID if provided
                if (alertDto.EquipmentId.HasValue)
                {
                    var equipment = await _context.Equipment.FindAsync(alertDto.EquipmentId.Value);
                    if (equipment == null)
                    {
                        return BadRequest(new { message = "Invalid equipment ID" });
                    }
                    alert.EquipmentId = alertDto.EquipmentId.Value;
                }

                // Update properties
                if (!string.IsNullOrEmpty(alertDto.Title))
                    alert.Title = alertDto.Title;

                if (!string.IsNullOrEmpty(alertDto.Description))
                    alert.Description = alertDto.Description;

                if (alertDto.Priority.HasValue)
                    alert.Priority = alertDto.Priority.Value;

                if (alertDto.Status.HasValue)
                {
                    alert.Status = alertDto.Status.Value;
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Alert updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alert with ID {AlertId}", id);
                return StatusCode(500, new { message = "An error occurred while updating alert" });
            }
        }

        /// <summary>
        /// Resolve alert
        /// </summary>
        /// <param name="id">Alert ID</param>
        [HttpPut("{id}/resolve")]
        public async Task<IActionResult> ResolveAlert(int id)
        {
            try
            {
                var alert = await _context.Alerts.FindAsync(id);
                if (alert == null)
                {
                    return NotFound(new { message = $"Alert with ID {id} not found" });
                }

                if (alert.Status == AlertStatus.Resolved)
                {
                    return BadRequest(new { message = "Alert is already resolved" });
                }

                alert.Status = AlertStatus.Resolved;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Alert resolved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving alert with ID {AlertId}", id);
                return StatusCode(500, new { message = "An error occurred while resolving alert" });
            }
        }

        /// <summary>
        /// Delete alert
        /// </summary>
        /// <param name="id">Alert ID</param>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlert(int id)
        {
            try
            {
                var alert = await _context.Alerts.FindAsync(id);
                if (alert == null)
                {
                    return NotFound(new { message = $"Alert with ID {id} not found" });
                }

                _context.Alerts.Remove(alert);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Alert deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting alert with ID {AlertId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting alert" });
            }
        }

        /// <summary>
        /// Get alert statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetAlertStatistics()
        {
            try
            {
                var currentDate = DateTime.Now;
                var startOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                var startOfLastMonth = startOfMonth.AddMonths(-1);

                var stats = new
                {
                    TotalAlerts = await _context.Alerts.CountAsync(),
                    OpenAlerts = await _context.Alerts.CountAsync(a => a.Status == AlertStatus.Open),
                    ResolvedAlerts = await _context.Alerts.CountAsync(a => a.Status == AlertStatus.Resolved),
                    HighAlerts = await _context.Alerts.CountAsync(a => a.Priority == AlertPriority.High && a.Status == AlertStatus.Open),
                    MediumAlerts = await _context.Alerts.CountAsync(a => a.Priority == AlertPriority.Medium && a.Status == AlertStatus.Open),
                    LowAlerts = await _context.Alerts.CountAsync(a => a.Priority == AlertPriority.Low && a.Status == AlertStatus.Open),
                    AlertsThisMonth = await _context.Alerts.CountAsync(a => a.CreatedDate >= startOfMonth),
                    AlertsLastMonth = await _context.Alerts.CountAsync(a => a.CreatedDate >= startOfLastMonth && a.CreatedDate < startOfMonth),
                    AlertsByPriority = await _context.Alerts
                        .GroupBy(a => a.Priority)
                        .Select(g => new { Priority = g.Key.ToString(), Count = g.Count() })
                        .ToListAsync(),
                    AlertsByStatus = await _context.Alerts
                        .GroupBy(a => a.Status)
                        .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                        .ToListAsync(),
                    TopEquipmentWithAlerts = await _context.Alerts
                        .Include(a => a.Equipment)
                        .Where(a => a.Equipment != null)
                        .GroupBy(a => a.Equipment.Name)
                        .Select(g => new { EquipmentName = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(10)
                        .ToListAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alert statistics");
                return StatusCode(500, new { message = "An error occurred while retrieving alert statistics" });
            }
        }

        /// <summary>
        /// Get alerts dashboard summary
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetAlertsDashboard()
        {
            try
            {
                var today = DateTime.Now.Date;
                var thisWeek = today.AddDays(-(int)today.DayOfWeek);
                var thisMonth = new DateTime(today.Year, today.Month, 1);

                var dashboard = new
                {
                    TotalActiveAlerts = await _context.Alerts.CountAsync(a => a.Status == AlertStatus.Open),
                    HighPriorityAlerts = await _context.Alerts.CountAsync(a => a.Priority == AlertPriority.High && a.Status == AlertStatus.Open),
                    AlertsToday = await _context.Alerts.CountAsync(a => a.CreatedDate.Date == today),
                    AlertsThisWeek = await _context.Alerts.CountAsync(a => a.CreatedDate >= thisWeek),
                    AlertsThisMonth = await _context.Alerts.CountAsync(a => a.CreatedDate >= thisMonth),
                    RecentAlerts = await _context.Alerts
                        .Include(a => a.Equipment)
                        .OrderByDescending(a => a.CreatedDate)
                        .Take(10)
                        .Select(a => new
                        {
                            a.AlertId,
                            a.Title,
                            a.Priority,
                            a.Status,
                            a.CreatedDate,
                            Equipment = a.Equipment != null ? a.Equipment.Name : "System"
                        })
                        .ToListAsync(),
                    AlertTrend = await _context.Alerts
                        .Where(a => a.CreatedDate >= today.AddDays(-30))
                        .GroupBy(a => a.CreatedDate.Date)
                        .Select(g => new { Date = g.Key, Count = g.Count() })
                        .OrderBy(x => x.Date)
                        .ToListAsync()
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alerts dashboard");
                return StatusCode(500, new { message = "An error occurred while retrieving alerts dashboard" });
            }
        }
    }

    // DTOs for API
    public class CreateAlertDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public AlertPriority Priority { get; set; }

        public int? EquipmentId { get; set; }
    }

    public class UpdateAlertDto
    {
        [StringLength(200)]
        public string? Title { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public AlertPriority? Priority { get; set; }

        public AlertStatus? Status { get; set; }

        public int? EquipmentId { get; set; }
    }
}
