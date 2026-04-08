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
    public class EquipmentApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EquipmentApiController> _logger;

        public EquipmentApiController(ApplicationDbContext context, ILogger<EquipmentApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all equipment with optional filtering
        /// </summary>
        /// <param name="status">Filter by equipment status</param>
        /// <param name="roomId">Filter by room ID</param>
        /// <param name="equipmentTypeId">Filter by equipment type ID</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 20)</param>
        [HttpGet]
        public async Task<IActionResult> GetEquipment(
            [FromQuery] EquipmentStatus? status = null,
            [FromQuery] int? roomId = null,
            [FromQuery] int? equipmentTypeId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Equipment
                    .Include(e => e.Room)
                        .ThenInclude(r => r.Building)
                    .Include(e => e.EquipmentType)
                    .Include(e => e.EquipmentModel)
                    .AsQueryable();

                // Apply filters
                if (status.HasValue)
                    query = query.Where(e => e.Status == status.Value);

                if (roomId.HasValue)
                    query = query.Where(e => e.RoomId == roomId.Value);

                if (equipmentTypeId.HasValue)
                    query = query.Where(e => e.EquipmentTypeId == equipmentTypeId.Value);

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Apply pagination
                var equipment = await query
                    .OrderBy(e => e.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.SerialNumber,
                        e.Status,
                        e.InstallationDate,
                        e.LastMaintenanceDate,
                        e.NextMaintenanceDate,
                        e.PerformanceScore,
                        Room = new { e.Room.Id, e.Room.RoomNumber, e.Room.RoomName },
                        Building = new { e.Room.Building.Id, e.Room.Building.BuildingName },
                        EquipmentType = new { e.EquipmentType.Id, e.EquipmentType.TypeName },
                        EquipmentModel = new { e.EquipmentModel.Id, e.EquipmentModel.ModelName, e.EquipmentModel.Manufacturer }
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = equipment,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving equipment list");
                return StatusCode(500, new { message = "An error occurred while retrieving equipment data" });
            }
        }

        /// <summary>
        /// Get equipment by ID
        /// </summary>
        /// <param name="id">Equipment ID</param>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEquipment(int id)
        {
            try
            {
                var equipment = await _context.Equipment
                    .Include(e => e.Room)
                        .ThenInclude(r => r.Building)
                    .Include(e => e.EquipmentType)
                    .Include(e => e.EquipmentModel)
                    .Where(e => e.Id == id)
                    .Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.SerialNumber,
                        e.Status,
                        e.InstallationDate,
                        e.LastMaintenanceDate,
                        e.NextMaintenanceDate,
                        e.PerformanceScore,
                        e.Notes,
                        Room = new { e.Room.Id, e.Room.RoomNumber, e.Room.RoomName },
                        Building = new { e.Room.Building.Id, e.Room.Building.BuildingName },
                        EquipmentType = new { e.EquipmentType.Id, e.EquipmentType.TypeName },
                        EquipmentModel = new { e.EquipmentModel.Id, e.EquipmentModel.ModelName, e.EquipmentModel.Manufacturer }
                    })
                    .FirstOrDefaultAsync();

                if (equipment == null)
                {
                    return NotFound(new { message = $"Equipment with ID {id} not found" });
                }

                return Ok(equipment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving equipment with ID {EquipmentId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving equipment data" });
            }
        }

        /// <summary>
        /// Create new equipment
        /// </summary>
        /// <param name="equipmentDto">Equipment data</param>
        [HttpPost]
        public async Task<IActionResult> CreateEquipment([FromBody] CreateEquipmentDto equipmentDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate foreign keys
                var room = await _context.Rooms.FindAsync(equipmentDto.RoomId);
                if (room == null)
                {
                    return BadRequest(new { message = "Invalid room ID" });
                }

                var equipmentType = await _context.EquipmentTypes.FindAsync(equipmentDto.EquipmentTypeId);
                if (equipmentType == null)
                {
                    return BadRequest(new { message = "Invalid equipment type ID" });
                }

                var equipmentModel = await _context.EquipmentModels.FindAsync(equipmentDto.EquipmentModelId);
                if (equipmentModel == null)
                {
                    return BadRequest(new { message = "Invalid equipment model ID" });
                }

                var equipment = new Equipment
                {
                    Name = equipmentDto.Name,
                    SerialNumber = equipmentDto.SerialNumber,
                    Status = equipmentDto.Status,
                    InstallationDate = equipmentDto.InstallationDate,
                    RoomId = equipmentDto.RoomId,
                    EquipmentTypeId = equipmentDto.EquipmentTypeId,
                    EquipmentModelId = equipmentDto.EquipmentModelId,
                    Notes = equipmentDto.Notes,
                    PerformanceScore = 100 // Default performance score for new equipment
                };

                _context.Equipment.Add(equipment);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetEquipment), new { id = equipment.Id }, new { equipment.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating equipment");
                return StatusCode(500, new { message = "An error occurred while creating equipment" });
            }
        }

        /// <summary>
        /// Update equipment
        /// </summary>
        /// <param name="id">Equipment ID</param>
        /// <param name="equipmentDto">Updated equipment data</param>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEquipment(int id, [FromBody] UpdateEquipmentDto equipmentDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var equipment = await _context.Equipment.FindAsync(id);
                if (equipment == null)
                {
                    return NotFound(new { message = $"Equipment with ID {id} not found" });
                }

                // Validate foreign keys if provided
                if (equipmentDto.RoomId.HasValue)
                {
                    var room = await _context.Rooms.FindAsync(equipmentDto.RoomId.Value);
                    if (room == null)
                    {
                        return BadRequest(new { message = "Invalid room ID" });
                    }
                    equipment.RoomId = equipmentDto.RoomId.Value;
                }

                if (equipmentDto.EquipmentTypeId.HasValue)
                {
                    var equipmentType = await _context.EquipmentTypes.FindAsync(equipmentDto.EquipmentTypeId.Value);
                    if (equipmentType == null)
                    {
                        return BadRequest(new { message = "Invalid equipment type ID" });
                    }
                    equipment.EquipmentTypeId = equipmentDto.EquipmentTypeId.Value;
                }

                if (equipmentDto.EquipmentModelId.HasValue)
                {
                    var equipmentModel = await _context.EquipmentModels.FindAsync(equipmentDto.EquipmentModelId.Value);
                    if (equipmentModel == null)
                    {
                        return BadRequest(new { message = "Invalid equipment model ID" });
                    }
                    equipment.EquipmentModelId = equipmentDto.EquipmentModelId.Value;
                }

                // Update properties
                if (!string.IsNullOrEmpty(equipmentDto.Name))
                    equipment.Name = equipmentDto.Name;

                if (!string.IsNullOrEmpty(equipmentDto.SerialNumber))
                    equipment.SerialNumber = equipmentDto.SerialNumber;

                if (equipmentDto.Status.HasValue)
                    equipment.Status = equipmentDto.Status.Value;

                if (equipmentDto.InstallationDate.HasValue)
                    equipment.InstallationDate = equipmentDto.InstallationDate.Value;

                if (equipmentDto.LastMaintenanceDate.HasValue)
                    equipment.LastMaintenanceDate = equipmentDto.LastMaintenanceDate.Value;

                if (equipmentDto.NextMaintenanceDate.HasValue)
                    equipment.NextMaintenanceDate = equipmentDto.NextMaintenanceDate.Value;

                if (equipmentDto.PerformanceScore.HasValue)
                    equipment.PerformanceScore = equipmentDto.PerformanceScore.Value;

                if (equipmentDto.Notes != null)
                    equipment.Notes = equipmentDto.Notes;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Equipment updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating equipment with ID {EquipmentId}", id);
                return StatusCode(500, new { message = "An error occurred while updating equipment" });
            }
        }

        /// <summary>
        /// Delete equipment
        /// </summary>
        /// <param name="id">Equipment ID</param>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEquipment(int id)
        {
            try
            {
                var equipment = await _context.Equipment.FindAsync(id);
                if (equipment == null)
                {
                    return NotFound(new { message = $"Equipment with ID {id} not found" });
                }

                // Check if equipment has related maintenance logs
                var hasMaintenanceLogs = await _context.MaintenanceLogs.AnyAsync(ml => ml.EquipmentId == id);
                if (hasMaintenanceLogs)
                {
                    return BadRequest(new { message = "Cannot delete equipment with existing maintenance logs" });
                }

                _context.Equipment.Remove(equipment);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Equipment deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting equipment with ID {EquipmentId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting equipment" });
            }
        }

        /// <summary>
        /// Get equipment maintenance history
        /// </summary>
        /// <param name="id">Equipment ID</param>
        [HttpGet("{id}/maintenance-history")]
        public async Task<IActionResult> GetMaintenanceHistory(int id)
        {
            try
            {
                var equipment = await _context.Equipment.FindAsync(id);
                if (equipment == null)
                {
                    return NotFound(new { message = $"Equipment with ID {id} not found" });
                }

                var maintenanceHistory = await _context.MaintenanceLogs
                    .Where(ml => ml.EquipmentId == id)
                    .Include(ml => ml.MaintenanceTask)
                    .Include(ml => ml.User)
                    .OrderByDescending(ml => ml.DateCompleted)
                    .Select(ml => new
                    {
                        ml.Id,
                        ml.DateCompleted,
                        ml.Description,
                        ml.Notes,
                        ml.Cost,
                        MaintenanceTask = new { ml.MaintenanceTask.Id, ml.MaintenanceTask.TaskName, ml.MaintenanceTask.TaskType },
                        CompletedBy = new { ml.User.Id, ml.User.FirstName, ml.User.LastName }
                    })
                    .ToListAsync();

                return Ok(maintenanceHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving maintenance history for equipment ID {EquipmentId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving maintenance history" });
            }
        }
    }

    // DTOs for API
    public class CreateEquipmentDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string SerialNumber { get; set; } = string.Empty;

        [Required]
        public EquipmentStatus Status { get; set; }

        [Required]
        public DateTime InstallationDate { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required]
        public int EquipmentTypeId { get; set; }

        [Required]
        public int EquipmentModelId { get; set; }

        public string? Notes { get; set; }
    }

    public class UpdateEquipmentDto
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(50)]
        public string? SerialNumber { get; set; }

        public EquipmentStatus? Status { get; set; }

        public DateTime? InstallationDate { get; set; }

        public DateTime? LastMaintenanceDate { get; set; }

        public DateTime? NextMaintenanceDate { get; set; }

        [Range(0, 100)]
        public decimal? PerformanceScore { get; set; }

        public int? RoomId { get; set; }

        public int? EquipmentTypeId { get; set; }

        public int? EquipmentModelId { get; set; }

        public string? Notes { get; set; }
    }
}
