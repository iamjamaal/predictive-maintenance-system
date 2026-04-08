using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FEENALOoFINALE.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MaintenanceLogApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MaintenanceLogApiController> _logger;

        public MaintenanceLogApiController(ApplicationDbContext context, ILogger<MaintenanceLogApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all maintenance logs with optional filtering
        /// </summary>
        /// <param name="equipmentId">Filter by equipment ID</param>
        /// <param name="userId">Filter by user ID</param>
        /// <param name="startDate">Filter by start date</param>
        /// <param name="endDate">Filter by end date</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 20)</param>
        [HttpGet]
        public async Task<IActionResult> GetMaintenanceLogs(
            [FromQuery] int? equipmentId = null,
            [FromQuery] string? userId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.MaintenanceLogs
                    .Include(ml => ml.Equipment)
                    .Include(ml => ml.MaintenanceTask)
                    .Include(ml => ml.User)
                    .AsQueryable();

                // Apply filters
                if (equipmentId.HasValue)
                    query = query.Where(ml => ml.EquipmentId == equipmentId.Value);

                if (!string.IsNullOrEmpty(userId))
                    query = query.Where(ml => ml.UserId == userId);

                if (startDate.HasValue)
                    query = query.Where(ml => ml.DateCompleted >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(ml => ml.DateCompleted <= endDate.Value);

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Apply pagination
                var maintenanceLogs = await query
                    .OrderByDescending(ml => ml.DateCompleted)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(ml => new
                    {
                        ml.Id,
                        ml.DateCompleted,
                        ml.Description,
                        ml.Notes,
                        ml.Cost,
                        Equipment = new { ml.Equipment.Id, ml.Equipment.Name, ml.Equipment.SerialNumber },
                        MaintenanceTask = new { ml.MaintenanceTask.Id, ml.MaintenanceTask.TaskName, ml.MaintenanceTask.TaskType },
                        CompletedBy = new { ml.User.Id, ml.User.FirstName, ml.User.LastName, ml.User.Email }
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = maintenanceLogs,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving maintenance logs");
                return StatusCode(500, new { message = "An error occurred while retrieving maintenance logs" });
            }
        }

        /// <summary>
        /// Get maintenance log by ID
        /// </summary>
        /// <param name="id">Maintenance log ID</param>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMaintenanceLog(int id)
        {
            try
            {
                var maintenanceLog = await _context.MaintenanceLogs
                    .Include(ml => ml.Equipment)
                        .ThenInclude(e => e.Room)
                            .ThenInclude(r => r.Building)
                    .Include(ml => ml.MaintenanceTask)
                    .Include(ml => ml.User)
                    .Where(ml => ml.Id == id)
                    .Select(ml => new
                    {
                        ml.Id,
                        ml.DateCompleted,
                        ml.Description,
                        ml.Notes,
                        ml.Cost,
                        Equipment = new 
                        { 
                            ml.Equipment.Id, 
                            ml.Equipment.Name, 
                            ml.Equipment.SerialNumber,
                            Room = new { ml.Equipment.Room.Id, ml.Equipment.Room.RoomNumber, ml.Equipment.Room.RoomName },
                            Building = new { ml.Equipment.Room.Building.Id, ml.Equipment.Room.Building.BuildingName }
                        },
                        MaintenanceTask = new 
                        { 
                            ml.MaintenanceTask.Id, 
                            ml.MaintenanceTask.TaskName, 
                            ml.MaintenanceTask.TaskType,
                            ml.MaintenanceTask.Description,
                            ml.MaintenanceTask.EstimatedDuration
                        },
                        CompletedBy = new 
                        { 
                            ml.User.Id, 
                            ml.User.FirstName, 
                            ml.User.LastName, 
                            ml.User.Email,
                            ml.User.Role
                        }
                    })
                    .FirstOrDefaultAsync();

                if (maintenanceLog == null)
                {
                    return NotFound(new { message = $"Maintenance log with ID {id} not found" });
                }

                return Ok(maintenanceLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving maintenance log with ID {MaintenanceLogId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving maintenance log" });
            }
        }

        /// <summary>
        /// Create new maintenance log
        /// </summary>
        /// <param name="maintenanceLogDto">Maintenance log data</param>
        [HttpPost]
        public async Task<IActionResult> CreateMaintenanceLog([FromBody] CreateMaintenanceLogDto maintenanceLogDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Get current user ID from claims
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                // Validate foreign keys
                var equipment = await _context.Equipment.FindAsync(maintenanceLogDto.EquipmentId);
                if (equipment == null)
                {
                    return BadRequest(new { message = "Invalid equipment ID" });
                }

                var maintenanceTask = await _context.MaintenanceTasks.FindAsync(maintenanceLogDto.MaintenanceTaskId);
                if (maintenanceTask == null)
                {
                    return BadRequest(new { message = "Invalid maintenance task ID" });
                }

                var maintenanceLog = new MaintenanceLog
                {
                    EquipmentId = maintenanceLogDto.EquipmentId,
                    MaintenanceTaskId = maintenanceLogDto.MaintenanceTaskId,
                    UserId = currentUserId,
                    DateCompleted = maintenanceLogDto.DateCompleted,
                    Description = maintenanceLogDto.Description,
                    Notes = maintenanceLogDto.Notes,
                    Cost = maintenanceLogDto.Cost
                };

                _context.MaintenanceLogs.Add(maintenanceLog);

                // Update equipment's last maintenance date
                equipment.LastMaintenanceDate = maintenanceLogDto.DateCompleted;

                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetMaintenanceLog), new { id = maintenanceLog.Id }, new { maintenanceLog.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating maintenance log");
                return StatusCode(500, new { message = "An error occurred while creating maintenance log" });
            }
        }

        /// <summary>
        /// Update maintenance log
        /// </summary>
        /// <param name="id">Maintenance log ID</param>
        /// <param name="maintenanceLogDto">Updated maintenance log data</param>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMaintenanceLog(int id, [FromBody] UpdateMaintenanceLogDto maintenanceLogDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var maintenanceLog = await _context.MaintenanceLogs.FindAsync(id);
                if (maintenanceLog == null)
                {
                    return NotFound(new { message = $"Maintenance log with ID {id} not found" });
                }

                // Validate foreign keys if provided
                if (maintenanceLogDto.EquipmentId.HasValue)
                {
                    var equipment = await _context.Equipment.FindAsync(maintenanceLogDto.EquipmentId.Value);
                    if (equipment == null)
                    {
                        return BadRequest(new { message = "Invalid equipment ID" });
                    }
                    maintenanceLog.EquipmentId = maintenanceLogDto.EquipmentId.Value;
                }

                if (maintenanceLogDto.MaintenanceTaskId.HasValue)
                {
                    var maintenanceTask = await _context.MaintenanceTasks.FindAsync(maintenanceLogDto.MaintenanceTaskId.Value);
                    if (maintenanceTask == null)
                    {
                        return BadRequest(new { message = "Invalid maintenance task ID" });
                    }
                    maintenanceLog.MaintenanceTaskId = maintenanceLogDto.MaintenanceTaskId.Value;
                }

                // Update properties
                if (maintenanceLogDto.DateCompleted.HasValue)
                    maintenanceLog.DateCompleted = maintenanceLogDto.DateCompleted.Value;

                if (!string.IsNullOrEmpty(maintenanceLogDto.Description))
                    maintenanceLog.Description = maintenanceLogDto.Description;

                if (maintenanceLogDto.Notes != null)
                    maintenanceLog.Notes = maintenanceLogDto.Notes;

                if (maintenanceLogDto.Cost.HasValue)
                    maintenanceLog.Cost = maintenanceLogDto.Cost.Value;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Maintenance log updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating maintenance log with ID {MaintenanceLogId}", id);
                return StatusCode(500, new { message = "An error occurred while updating maintenance log" });
            }
        }

        /// <summary>
        /// Delete maintenance log
        /// </summary>
        /// <param name="id">Maintenance log ID</param>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMaintenanceLog(int id)
        {
            try
            {
                var maintenanceLog = await _context.MaintenanceLogs.FindAsync(id);
                if (maintenanceLog == null)
                {
                    return NotFound(new { message = $"Maintenance log with ID {id} not found" });
                }

                _context.MaintenanceLogs.Remove(maintenanceLog);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Maintenance log deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting maintenance log with ID {MaintenanceLogId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting maintenance log" });
            }
        }

        /// <summary>
        /// Get maintenance statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetMaintenanceStatistics()
        {
            try
            {
                var currentDate = DateTime.Now;
                var startOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                var startOfLastMonth = startOfMonth.AddMonths(-1);

                var stats = new
                {
                    TotalMaintenanceLogs = await _context.MaintenanceLogs.CountAsync(),
                    MaintenanceThisMonth = await _context.MaintenanceLogs
                        .CountAsync(ml => ml.DateCompleted >= startOfMonth),
                    MaintenanceLastMonth = await _context.MaintenanceLogs
                        .CountAsync(ml => ml.DateCompleted >= startOfLastMonth && ml.DateCompleted < startOfMonth),
                    TotalCostThisMonth = await _context.MaintenanceLogs
                        .Where(ml => ml.DateCompleted >= startOfMonth)
                        .SumAsync(ml => ml.Cost),
                    TotalCostLastMonth = await _context.MaintenanceLogs
                        .Where(ml => ml.DateCompleted >= startOfLastMonth && ml.DateCompleted < startOfMonth)
                        .SumAsync(ml => ml.Cost),
                    TopMaintenanceTasks = await _context.MaintenanceLogs
                        .Include(ml => ml.MaintenanceTask)
                        .GroupBy(ml => ml.MaintenanceTask.TaskName)
                        .Select(g => new { TaskName = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(5)
                        .ToListAsync(),
                    MaintenanceByEquipment = await _context.MaintenanceLogs
                        .Include(ml => ml.Equipment)
                        .GroupBy(ml => ml.Equipment.Name)
                        .Select(g => new { EquipmentName = g.Key, Count = g.Count(), TotalCost = g.Sum(ml => ml.Cost) })
                        .OrderByDescending(x => x.Count)
                        .Take(10)
                        .ToListAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving maintenance statistics");
                return StatusCode(500, new { message = "An error occurred while retrieving maintenance statistics" });
            }
        }
    }

    // DTOs for API
    public class CreateMaintenanceLogDto
    {
        [Required]
        public int EquipmentId { get; set; }

        [Required]
        public int MaintenanceTaskId { get; set; }

        [Required]
        public DateTime DateCompleted { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public string? Notes { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Cost { get; set; }
    }

    public class UpdateMaintenanceLogDto
    {
        public int? EquipmentId { get; set; }

        public int? MaintenanceTaskId { get; set; }

        public DateTime? DateCompleted { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public string? Notes { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Cost { get; set; }
    }
}
