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
    public class InventoryApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InventoryApiController> _logger;

        public InventoryApiController(ApplicationDbContext context, ILogger<InventoryApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all inventory items with optional filtering
        /// </summary>
        /// <param name="category">Filter by category</param>
        /// <param name="lowStock">Filter items with low stock</param>
        /// <param name="search">Search by name or description</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 20)</param>
        [HttpGet]
        public async Task<IActionResult> GetInventoryItems(
            [FromQuery] string? category = null,
            [FromQuery] bool? lowStock = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.InventoryItems
                    .Include(i => i.InventoryStock)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(category))
                    query = query.Where(i => i.Category.ToLower().Contains(category.ToLower()));

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(i => i.Name.ToLower().Contains(search.ToLower()) || 
                                           i.Description.ToLower().Contains(search.ToLower()));

                if (lowStock.HasValue && lowStock.Value)
                    query = query.Where(i => i.InventoryStock.Any(s => s.Quantity <= s.MinimumQuantity));

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Apply pagination
                var inventoryItems = await query
                    .OrderBy(i => i.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(i => new
                    {
                        i.Id,
                        i.Name,
                        i.Description,
                        i.Category,
                        i.UnitPrice,
                        i.Supplier,
                        TotalQuantity = i.InventoryStock.Sum(s => s.Quantity),
                        MinimumQuantity = i.InventoryStock.FirstOrDefault() != null ? i.InventoryStock.FirstOrDefault().MinimumQuantity : 0,
                        IsLowStock = i.InventoryStock.Any(s => s.Quantity <= s.MinimumQuantity),
                        LastUpdated = i.InventoryStock.OrderByDescending(s => s.LastUpdated).FirstOrDefault() != null ? 
                                     i.InventoryStock.OrderByDescending(s => s.LastUpdated).FirstOrDefault().LastUpdated : (DateTime?)null
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = inventoryItems,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inventory items");
                return StatusCode(500, new { message = "An error occurred while retrieving inventory items" });
            }
        }

        /// <summary>
        /// Get inventory item by ID
        /// </summary>
        /// <param name="id">Inventory item ID</param>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetInventoryItem(int id)
        {
            try
            {
                var inventoryItem = await _context.InventoryItems
                    .Include(i => i.InventoryStock)
                    .Where(i => i.Id == id)
                    .Select(i => new
                    {
                        i.Id,
                        i.Name,
                        i.Description,
                        i.Category,
                        i.UnitPrice,
                        i.Supplier,
                        Stock = i.InventoryStock.Select(s => new
                        {
                            s.Id,
                            s.Quantity,
                            s.MinimumQuantity,
                            s.Location,
                            s.LastUpdated
                        }).ToList(),
                        TotalQuantity = i.InventoryStock.Sum(s => s.Quantity),
                        IsLowStock = i.InventoryStock.Any(s => s.Quantity <= s.MinimumQuantity)
                    })
                    .FirstOrDefaultAsync();

                if (inventoryItem == null)
                {
                    return NotFound(new { message = $"Inventory item with ID {id} not found" });
                }

                return Ok(inventoryItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inventory item with ID {InventoryItemId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving inventory item" });
            }
        }

        /// <summary>
        /// Create new inventory item
        /// </summary>
        /// <param name="inventoryItemDto">Inventory item data</param>
        [HttpPost]
        public async Task<IActionResult> CreateInventoryItem([FromBody] CreateInventoryItemDto inventoryItemDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var inventoryItem = new InventoryItem
                {
                    Name = inventoryItemDto.Name,
                    Description = inventoryItemDto.Description,
                    Category = inventoryItemDto.Category,
                    UnitPrice = inventoryItemDto.UnitPrice,
                    Supplier = inventoryItemDto.Supplier
                };

                _context.InventoryItems.Add(inventoryItem);
                await _context.SaveChangesAsync();

                // Create initial stock entry if provided
                if (inventoryItemDto.InitialQuantity.HasValue)
                {
                    var inventoryStock = new InventoryStock
                    {
                        InventoryItemId = inventoryItem.Id,
                        Quantity = inventoryItemDto.InitialQuantity.Value,
                        MinimumQuantity = inventoryItemDto.MinimumQuantity ?? 10,
                        Location = inventoryItemDto.Location ?? "Main Warehouse",
                        LastUpdated = DateTime.Now
                    };

                    _context.InventoryStock.Add(inventoryStock);
                    await _context.SaveChangesAsync();
                }

                return CreatedAtAction(nameof(GetInventoryItem), new { id = inventoryItem.Id }, new { inventoryItem.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inventory item");
                return StatusCode(500, new { message = "An error occurred while creating inventory item" });
            }
        }

        /// <summary>
        /// Update inventory item
        /// </summary>
        /// <param name="id">Inventory item ID</param>
        /// <param name="inventoryItemDto">Updated inventory item data</param>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInventoryItem(int id, [FromBody] UpdateInventoryItemDto inventoryItemDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var inventoryItem = await _context.InventoryItems.FindAsync(id);
                if (inventoryItem == null)
                {
                    return NotFound(new { message = $"Inventory item with ID {id} not found" });
                }

                // Update properties
                if (!string.IsNullOrEmpty(inventoryItemDto.Name))
                    inventoryItem.Name = inventoryItemDto.Name;

                if (!string.IsNullOrEmpty(inventoryItemDto.Description))
                    inventoryItem.Description = inventoryItemDto.Description;

                if (!string.IsNullOrEmpty(inventoryItemDto.Category))
                    inventoryItem.Category = inventoryItemDto.Category;

                if (inventoryItemDto.UnitPrice.HasValue)
                    inventoryItem.UnitPrice = inventoryItemDto.UnitPrice.Value;

                if (!string.IsNullOrEmpty(inventoryItemDto.Supplier))
                    inventoryItem.Supplier = inventoryItemDto.Supplier;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Inventory item updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating inventory item with ID {InventoryItemId}", id);
                return StatusCode(500, new { message = "An error occurred while updating inventory item" });
            }
        }

        /// <summary>
        /// Delete inventory item
        /// </summary>
        /// <param name="id">Inventory item ID</param>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryItem(int id)
        {
            try
            {
                var inventoryItem = await _context.InventoryItems
                    .Include(i => i.InventoryStock)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (inventoryItem == null)
                {
                    return NotFound(new { message = $"Inventory item with ID {id} not found" });
                }

                // Check if item is used in maintenance logs
                var hasMaintenanceUsage = await _context.MaintenanceInventoryLinks.AnyAsync(mil => mil.InventoryItemId == id);
                if (hasMaintenanceUsage)
                {
                    return BadRequest(new { message = "Cannot delete inventory item with existing maintenance usage" });
                }

                // Remove stock entries first
                _context.InventoryStock.RemoveRange(inventoryItem.InventoryStock);
                _context.InventoryItems.Remove(inventoryItem);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Inventory item deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inventory item with ID {InventoryItemId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting inventory item" });
            }
        }

        /// <summary>
        /// Update inventory stock
        /// </summary>
        /// <param name="id">Inventory item ID</param>
        /// <param name="stockDto">Stock update data</param>
        [HttpPut("{id}/stock")]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] UpdateStockDto stockDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var inventoryItem = await _context.InventoryItems.FindAsync(id);
                if (inventoryItem == null)
                {
                    return NotFound(new { message = $"Inventory item with ID {id} not found" });
                }

                var stock = await _context.InventoryStock
                    .FirstOrDefaultAsync(s => s.InventoryItemId == id && s.Location == stockDto.Location);

                if (stock == null)
                {
                    // Create new stock entry
                    stock = new InventoryStock
                    {
                        InventoryItemId = id,
                        Quantity = stockDto.Quantity,
                        MinimumQuantity = stockDto.MinimumQuantity ?? 10,
                        Location = stockDto.Location,
                        LastUpdated = DateTime.Now
                    };
                    _context.InventoryStock.Add(stock);
                }
                else
                {
                    // Update existing stock
                    if (stockDto.IsAddition)
                    {
                        stock.Quantity += stockDto.Quantity;
                    }
                    else
                    {
                        stock.Quantity = stockDto.Quantity;
                    }

                    if (stockDto.MinimumQuantity.HasValue)
                        stock.MinimumQuantity = stockDto.MinimumQuantity.Value;

                    stock.LastUpdated = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Ok(new { 
                    message = "Stock updated successfully",
                    newQuantity = stock.Quantity,
                    isLowStock = stock.Quantity <= stock.MinimumQuantity
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for inventory item ID {InventoryItemId}", id);
                return StatusCode(500, new { message = "An error occurred while updating stock" });
            }
        }

        /// <summary>
        /// Get low stock items
        /// </summary>
        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStockItems()
        {
            try
            {
                var lowStockItems = await _context.InventoryItems
                    .Include(i => i.InventoryStock)
                    .Where(i => i.InventoryStock.Any(s => s.Quantity <= s.MinimumQuantity))
                    .Select(i => new
                    {
                        i.Id,
                        i.Name,
                        i.Category,
                        i.Supplier,
                        CurrentQuantity = i.InventoryStock.Sum(s => s.Quantity),
                        MinimumQuantity = i.InventoryStock.Min(s => s.MinimumQuantity),
                        StockLocations = i.InventoryStock.Select(s => new
                        {
                            s.Location,
                            s.Quantity,
                            s.MinimumQuantity,
                            IsLow = s.Quantity <= s.MinimumQuantity
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(lowStockItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving low stock items");
                return StatusCode(500, new { message = "An error occurred while retrieving low stock items" });
            }
        }

        /// <summary>
        /// Get inventory statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetInventoryStatistics()
        {
            try
            {
                var stats = new
                {
                    TotalItems = await _context.InventoryItems.CountAsync(),
                    TotalCategories = await _context.InventoryItems.Select(i => i.Category).Distinct().CountAsync(),
                    LowStockItems = await _context.InventoryItems
                        .CountAsync(i => i.InventoryStock.Any(s => s.Quantity <= s.MinimumQuantity)),
                    OutOfStockItems = await _context.InventoryItems
                        .CountAsync(i => i.InventoryStock.All(s => s.Quantity == 0)),
                    TotalInventoryValue = await _context.InventoryItems
                        .SumAsync(i => i.UnitPrice * i.InventoryStock.Sum(s => s.Quantity)),
                    ItemsByCategory = await _context.InventoryItems
                        .GroupBy(i => i.Category)
                        .Select(g => new { Category = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToListAsync(),
                    TopSuppliers = await _context.InventoryItems
                        .GroupBy(i => i.Supplier)
                        .Select(g => new { Supplier = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(10)
                        .ToListAsync(),
                    RecentlyUpdatedItems = await _context.InventoryStock
                        .Include(s => s.InventoryItem)
                        .OrderByDescending(s => s.LastUpdated)
                        .Take(10)
                        .Select(s => new
                        {
                            ItemName = s.InventoryItem.Name,
                            s.Location,
                            s.Quantity,
                            s.LastUpdated
                        })
                        .ToListAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inventory statistics");
                return StatusCode(500, new { message = "An error occurred while retrieving inventory statistics" });
            }
        }
    }

    // DTOs for API
    public class CreateInventoryItemDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Required]
        [StringLength(100)]
        public string Supplier { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int? InitialQuantity { get; set; }

        [Range(0, int.MaxValue)]
        public int? MinimumQuantity { get; set; }

        [StringLength(100)]
        public string? Location { get; set; }
    }

    public class UpdateInventoryItemDto
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Category { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? UnitPrice { get; set; }

        [StringLength(100)]
        public string? Supplier { get; set; }
    }

    public class UpdateStockDto
    {
        [Required]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(0, int.MaxValue)]
        public int? MinimumQuantity { get; set; }

        [Required]
        [StringLength(100)]
        public string Location { get; set; } = "Main Warehouse";

        public bool IsAddition { get; set; } = false;
    }
}
