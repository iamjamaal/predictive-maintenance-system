using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Text;

namespace FEENALOoFINALE.Controllers
{
    [Authorize]
    public class AssetController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AssetController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Main Unified Asset Management Dashboard
        public async Task<IActionResult> Index()
        {
            var viewModel = new UnifiedAssetDashboardViewModel
            {
                // Equipment Summary (from Equipment table)
                TotalEquipment = await _context.Equipment.CountAsync(),
                OperationalEquipment = await _context.Equipment
                    .CountAsync(e => e.Status == EquipmentStatus.Active),
                EquipmentUnderMaintenance = await _context.Equipment
                    .CountAsync(e => e.Status == EquipmentStatus.Inactive),

                // Inventory Summary (from InventoryItem table)
                TotalInventoryItems = await _context.InventoryItems.CountAsync(),
                LowStockItems = await GetLowStockCount(),
                OutOfStockItems = await GetOutOfStockCount(),

                // Recent maintenance activity
                RecentMaintenanceLogs = await _context.MaintenanceLogs
                    .Include(ml => ml.Equipment)
                    .OrderByDescending(ml => ml.LogDate)
                    .Take(5)
                    .ToListAsync(),

                // Active alerts (all types: equipment, inventory, and general)
                ActiveAlerts = await _context.Alerts
                    .Include(a => a.Equipment)
                    .Include(a => a.InventoryItem)
                    .Where(a => a.Status == AlertStatus.Open)
                    .OrderByDescending(a => a.Priority)
                    .ThenByDescending(a => a.CreatedDate)
                    .Take(5)
                    .ToListAsync(),

                // Combined asset list (Equipment + Inventory as unified view)
                RecentAssets = await GetRecentUnifiedAssets(10)
            };

            return View(viewModel);
        }

        // Equipment Management - List all equipment
        public async Task<IActionResult> Equipment()
        {
            try
            {
                var equipment = await _context.Equipment
                    .Include(e => e.EquipmentModel)
                    .Include(e => e.EquipmentType)
                    .Include(e => e.Building)
                    .Include(e => e.Room)
                    .ToListAsync();

                var viewModel = equipment.Select(e => new UnifiedAssetViewModel
                {
                    Id = e.EquipmentId,
                    Name = e.EquipmentModel?.ModelName ?? "Unknown Model",
                    Type = "Equipment",
                    Category = e.EquipmentType?.EquipmentTypeName ?? "Unknown Type",
                    Status = e.Status.ToString(),
                    Location = $"{e.Building?.BuildingName} - {e.Room?.RoomName}",
                    Details = $"Installed: {e.InstallationDate:yyyy-MM-dd}",
                    LastUpdated = e.InstallationDate
                }).ToList();

                // Always return a list, even if empty
                return View("EquipmentNew", viewModel ?? new List<UnifiedAssetViewModel>());
            }
            catch (Exception ex)
            {
                // Return empty list and error message if something goes wrong
                ViewBag.ErrorMessage = "Unable to load equipment. " + ex.Message;
                return View("EquipmentNew", new List<UnifiedAssetViewModel>());
            }
        }

        // Inventory Management - List all inventory items
        public async Task<IActionResult> Inventory()
        {
            try
            {
                var inventory = await _context.InventoryItems
                    .Include(i => i.InventoryStocks)
                    .ToListAsync();

                var viewModel = inventory.Select(i => new UnifiedAssetViewModel
                {
                    Id = i.ItemId,
                    Name = i.Name,
                    Type = "Inventory",
                    Category = i.Category.ToString(),
                    Status = GetInventoryStatus(i),
                    Location = "Warehouse"
                }).ToList();

                // Always return a list, even if empty
                return View("InventoryNew", viewModel ?? new List<UnifiedAssetViewModel>());
            }
            catch (Exception ex)
            {
                // Return empty list and error message if something goes wrong
                ViewBag.ErrorMessage = "Unable to load inventory. " + ex.Message;
                return View("InventoryNew", new List<UnifiedAssetViewModel>());
            }
        }

        // Helper method to get recent unified assets (Equipment + Inventory)
        private async Task<List<UnifiedAssetViewModel>> GetRecentUnifiedAssets(int count)
        {
            var assets = new List<UnifiedAssetViewModel>();

            // Get recent equipment (by installation date)
            var recentEquipment = await _context.Equipment
                .Include(e => e.EquipmentModel)
                .Include(e => e.EquipmentType)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .OrderByDescending(e => e.InstallationDate)
                .Take(count / 2)
                .ToListAsync();

            assets.AddRange(recentEquipment.Select(e => new UnifiedAssetViewModel
            {
                Id = e.EquipmentId,
                Name = e.EquipmentModel?.ModelName ?? "Unknown Model",
                Type = "Equipment",
                Category = e.EquipmentType?.EquipmentTypeName ?? "Unknown Type",
                Status = e.Status.ToString(),
                Location = $"{e.Building?.BuildingName} - {e.Room?.RoomName}",
                Details = $"Installed: {e.InstallationDate:yyyy-MM-dd}",
                LastUpdated = e.InstallationDate
            }));

            // Get recent inventory items (by ID since there's no created date)
            var recentInventory = await _context.InventoryItems
                .Include(i => i.InventoryStocks)
                .OrderByDescending(i => i.ItemId)
                .Take(count / 2)
                .ToListAsync();

            assets.AddRange(recentInventory.Select(i => new UnifiedAssetViewModel
            {
                Id = i.ItemId,
                Name = i.Name,
                Type = "Inventory",
                Category = i.Category.ToString(),
                Status = GetInventoryStatus(i),
                Location = "Warehouse",
                Details = $"Stock: {GetCurrentStock(i)} | Min: {i.MinimumStockLevel}",
                LastUpdated = DateTime.Now
            }));

            return assets.OrderByDescending(a => a.LastUpdated).Take(count).ToList();
        }

        // Helper method to get current stock for an inventory item
        private int GetCurrentStock(InventoryItem item)
        {
            return (int)(item.InventoryStocks?.Sum(s => s.Quantity) ?? 0);
        }

        // Helper method to determine inventory status
        private string GetInventoryStatus(InventoryItem item)
        {
            var currentStock = GetCurrentStock(item);
            
            if (currentStock == 0)
                return "Out of Stock";
            else if (currentStock <= item.MinimumStockLevel)
                return "Low Stock";
            else
                return "In Stock";
        }

        // Helper method to get low stock count
        private async Task<int> GetLowStockCount()
        {
            var inventoryItems = await _context.InventoryItems
                .Include(i => i.InventoryStocks)
                .ToListAsync();

            return inventoryItems.Count(i => GetCurrentStock(i) <= i.MinimumStockLevel);
        }

        // Helper method to get out of stock count
        private async Task<int> GetOutOfStockCount()
        {
            var inventoryItems = await _context.InventoryItems
                .Include(i => i.InventoryStocks)
                .ToListAsync();

            return inventoryItems.Count(i => GetCurrentStock(i) == 0);
        }

        // Asset details - handles both equipment and inventory
        public IActionResult Details(int id, string type)
        {
            if (type?.ToLower() == "equipment")
            {
                return RedirectToAction("Details", "Equipment", new { id = id });
            }
            else if (type?.ToLower() == "inventory")
            {
                return RedirectToAction("Details", "Inventory", new { id = id });
            }

            return NotFound();
        }

        // Quick Actions
        public IActionResult CreateEquipment()
        {
            return RedirectToAction("Create", "Equipment");
        }

        public IActionResult CreateInventory()
        {
            return RedirectToAction("Create", "Inventory");
        }

        public IActionResult ManageAlerts()
        {
            return RedirectToAction("Index", "Alert");
        }

        public IActionResult MaintenanceSchedule()
        {
            return RedirectToAction("Index", "MaintenanceLog");
        }

        public IActionResult Reports()
        {
            return RedirectToAction("Index", "Report");
        }

        // Enhanced Asset Management with advanced features
        public async Task<IActionResult> Enhanced(
            string? search = null,
            string[]? types = null,
            string[]? categories = null,
            string[]? statuses = null,
            string[]? locations = null,
            string[]? manufacturers = null,
            DateTime? installationFrom = null,
            DateTime? installationTo = null,
            DateTime? maintenanceFrom = null,
            DateTime? maintenanceTo = null,
            decimal? priceFrom = null,
            decimal? priceTo = null,
            int? stockFrom = null,
            int? stockTo = null,
            bool? showLowStock = null,
            bool? showCriticalOnly = null,
            bool? showMaintenanceDue = null,
            bool? showWithAlerts = null,
            string sortBy = "name",
            string sortDirection = "asc",
            int page = 1,
            int pageSize = 25)
        {
            try
            {
                var viewModel = new EnhancedAssetManagementViewModel
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    SearchTerm = search ?? string.Empty
                };

                // Build combined query for equipment and inventory
                var equipmentQuery = _context.Equipment
                    .Include(e => e.EquipmentModel)
                    .Include(e => e.EquipmentType)
                    .Include(e => e.Building)
                    .Include(e => e.Room)
                    .AsQueryable();

                var inventoryQuery = _context.InventoryItems
                    .Include(i => i.InventoryStocks)
                    .AsQueryable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    equipmentQuery = equipmentQuery.Where(e => 
                        e.EquipmentModel!.ModelName.Contains(search) ||
                        e.EquipmentType!.EquipmentTypeName.Contains(search) ||
                        e.Building!.BuildingName.Contains(search) ||
                        e.Room!.RoomName.Contains(search));
                    
                    inventoryQuery = inventoryQuery.Where(i => 
                        i.Name.Contains(search) ||
                        (i.Description != null && i.Description.Contains(search)));
                }

                // Apply type filter
                var includeEquipment = types == null || types.Length == 0 || types.Contains("Equipment");
                var includeInventory = types == null || types.Length == 0 || types.Contains("Inventory");

                // Apply status filters
                if (statuses != null && statuses.Length > 0)
                {
                    if (includeEquipment)
                    {
                        var equipmentStatuses = statuses.Where(s => Enum.TryParse<EquipmentStatus>(s, out _))
                                                      .Select(s => Enum.Parse<EquipmentStatus>(s));
                        if (equipmentStatuses.Any())
                        {
                            equipmentQuery = equipmentQuery.Where(e => equipmentStatuses.Contains(e.Status));
                        }
                    }
                }

                // Apply category filters
                if (categories != null && categories.Length > 0)
                {
                    if (includeEquipment)
                    {
                        equipmentQuery = equipmentQuery.Where(e => categories.Contains(e.EquipmentType!.EquipmentTypeName));
                    }
                    if (includeInventory)
                    {
                        var inventoryCategories = categories.Where(c => Enum.TryParse<ItemCategory>(c, out _))
                                                          .Select(c => Enum.Parse<ItemCategory>(c));
                        if (inventoryCategories.Any())
                        {
                            inventoryQuery = inventoryQuery.Where(i => inventoryCategories.Contains(i.Category));
                        }
                    }
                }

                // Apply location filters
                if (locations != null && locations.Length > 0 && includeEquipment)
                {
                    equipmentQuery = equipmentQuery.Where(e => locations.Contains(e.Building!.BuildingName));
                }

                // Apply date filters
                if (installationFrom.HasValue && includeEquipment)
                {
                    equipmentQuery = equipmentQuery.Where(e => e.InstallationDate >= installationFrom.Value);
                }
                if (installationTo.HasValue && includeEquipment)
                {
                    equipmentQuery = equipmentQuery.Where(e => e.InstallationDate <= installationTo.Value);
                }

                // Convert to unified asset models
                var equipmentAssets = includeEquipment ? 
                    await equipmentQuery.Select(e => new AssetItemViewModel
                    {
                        Id = e.EquipmentId,
                        Name = e.EquipmentModel!.ModelName,
                        Type = "Equipment",
                        Category = e.EquipmentType!.EquipmentTypeName,
                        Status = e.Status.ToString(),
                        Location = $"{e.Building!.BuildingName} - {e.Room!.RoomName}",
                        Description = e.Notes ?? string.Empty,
                        InstallationDate = e.InstallationDate,
                        Manufacturer = "N/A", // Equipment model doesn't have manufacturer in current schema
                        Model = e.EquipmentModel.ModelName,
                        SerialNumber = "N/A", // Equipment doesn't have serial number in current schema
                        LastUpdated = e.InstallationDate ?? DateTime.Now,
                        AlertCount = _context.Alerts.Count(a => a.EquipmentId == e.EquipmentId && a.Status == AlertStatus.Open),
                        HasCriticalAlerts = _context.Alerts.Any(a => a.EquipmentId == e.EquipmentId && 
                                                                   a.Status == AlertStatus.Open && 
                                                                   a.Priority == AlertPriority.High)
                    }).ToListAsync() : new List<AssetItemViewModel>();

                var inventoryAssets = includeInventory ?
                    await inventoryQuery.Select(i => new AssetItemViewModel
                    {
                        Id = i.ItemId,
                        Name = i.Name,
                        Type = "Inventory",
                        Category = i.Category.ToString(),
                        Status = GetInventoryStatus(i),
                        Location = "Warehouse",
                        Description = i.Description ?? string.Empty,
                        CurrentStock = i.InventoryStocks!.Sum(s => (int)s.Quantity),
                        MinimumStock = i.MinimumStockLevel,
                        MaximumStock = i.MaxStockLevel,
                        IsLowStock = i.InventoryStocks!.Sum(s => (int)s.Quantity) <= i.MinimumStockLevel,
                        IsOutOfStock = i.InventoryStocks!.Sum(s => (int)s.Quantity) == 0,
                        UnitPrice = 0, // InventoryItem doesn't have UnitPrice in current schema
                        Unit = "units", // InventoryItem doesn't have Unit in current schema
                        LastUpdated = DateTime.Now
                    }).ToListAsync() : new List<AssetItemViewModel>();

                // Combine and apply additional filters
                var allAssets = equipmentAssets.Concat(inventoryAssets).ToList();

                // Apply critical only filter
                if (showCriticalOnly == true)
                {
                    allAssets = allAssets.Where(a => a.HasCriticalAlerts || a.IsOutOfStock).ToList();
                }

                // Apply low stock filter
                if (showLowStock == true)
                {
                    allAssets = allAssets.Where(a => a.Type == "Equipment" || a.IsLowStock).ToList();
                }

                // Apply alerts filter
                if (showWithAlerts == true)
                {
                    allAssets = allAssets.Where(a => a.AlertCount > 0).ToList();
                }

                // Apply price range filters
                if (priceFrom.HasValue)
                {
                    allAssets = allAssets.Where(a => (a.PurchasePrice ?? a.UnitPrice) >= priceFrom.Value).ToList();
                }
                if (priceTo.HasValue)
                {
                    allAssets = allAssets.Where(a => (a.PurchasePrice ?? a.UnitPrice) <= priceTo.Value).ToList();
                }

                // Apply stock range filters
                if (stockFrom.HasValue)
                {
                    allAssets = allAssets.Where(a => a.Type == "Equipment" || a.CurrentStock >= stockFrom.Value).ToList();
                }
                if (stockTo.HasValue)
                {
                    allAssets = allAssets.Where(a => a.Type == "Equipment" || a.CurrentStock <= stockTo.Value).ToList();
                }

                // Apply sorting
                allAssets = ApplyAssetSorting(allAssets, sortBy, sortDirection);

                // Calculate statistics
                viewModel.Statistics = await CalculateAssetStatistics();

                // Set up pagination
                viewModel.TotalRecords = allAssets.Count;
                viewModel.Assets = allAssets
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Set up filter options
                viewModel.FilterOptions = await GetAssetFilterOptions();
                viewModel.FilterOptions.SelectedTypes = types?.ToList() ?? new List<string>();
                viewModel.FilterOptions.SelectedCategories = categories?.ToList() ?? new List<string>();
                viewModel.FilterOptions.SelectedStatuses = statuses?.ToList() ?? new List<string>();
                viewModel.FilterOptions.SelectedLocations = locations?.ToList() ?? new List<string>();
                viewModel.FilterOptions.SelectedManufacturers = manufacturers?.ToList() ?? new List<string>();
                viewModel.FilterOptions.InstallationDateFrom = installationFrom;
                viewModel.FilterOptions.InstallationDateTo = installationTo;
                viewModel.FilterOptions.LastMaintenanceDateFrom = maintenanceFrom;
                viewModel.FilterOptions.LastMaintenanceDateTo = maintenanceTo;
                viewModel.FilterOptions.PriceFrom = priceFrom;
                viewModel.FilterOptions.PriceTo = priceTo;
                viewModel.FilterOptions.StockFrom = stockFrom;
                viewModel.FilterOptions.StockTo = stockTo;
                viewModel.FilterOptions.ShowLowStock = showLowStock;
                viewModel.FilterOptions.ShowCriticalOnly = showCriticalOnly;
                viewModel.FilterOptions.ShowMaintenanceDue = showMaintenanceDue;
                viewModel.FilterOptions.ShowWithAlerts = showWithAlerts;

                // Set up sort options
                viewModel.SortOptions.SortBy = Enum.TryParse<AssetSortField>(sortBy, true, out var sortField) ? sortField : AssetSortField.Name;
                viewModel.SortOptions.Direction = sortDirection?.ToLower() == "desc" ? SortDirection.Descending : SortDirection.Ascending;

                // Set up bulk actions
                viewModel.AvailableBulkActions = GetAssetBulkActions();

                // Set up export options
                viewModel.ExportOptions = GetAssetExportOptions();

                // Set up column options
                viewModel.ColumnOptions = GetAssetColumnOptions();

                viewModel.Notifications.Add(new NotificationMessage
                {
                    Type = NotificationType.Success,
                    Message = $"Loaded {viewModel.Assets.Count} assets from {viewModel.TotalRecords} total records"
                });

                return View(viewModel);
            }
            catch (Exception ex)
            {
                var errorViewModel = new EnhancedAssetManagementViewModel();
                errorViewModel.Notifications.Add(new NotificationMessage
                {
                    Type = NotificationType.Error,
                    Message = $"Error loading assets: {ex.Message}"
                });
                return View(errorViewModel);
            }
        }

        // Helper method to apply sorting to assets
        private List<AssetItemViewModel> ApplyAssetSorting(List<AssetItemViewModel> assets, string sortBy, string sortDirection)
        {
            var ascending = sortDirection?.ToLower() != "desc";
            
            return sortBy?.ToLower() switch
            {
                "name" => ascending ? assets.OrderBy(a => a.Name).ToList() : assets.OrderByDescending(a => a.Name).ToList(),
                "type" => ascending ? assets.OrderBy(a => a.Type).ToList() : assets.OrderByDescending(a => a.Type).ToList(),
                "category" => ascending ? assets.OrderBy(a => a.Category).ToList() : assets.OrderByDescending(a => a.Category).ToList(),
                "status" => ascending ? assets.OrderBy(a => a.Status).ToList() : assets.OrderByDescending(a => a.Status).ToList(),
                "location" => ascending ? assets.OrderBy(a => a.Location).ToList() : assets.OrderByDescending(a => a.Location).ToList(),
                "installationdate" => ascending ? assets.OrderBy(a => a.InstallationDate).ToList() : assets.OrderByDescending(a => a.InstallationDate).ToList(),
                "value" => ascending ? assets.OrderBy(a => a.PurchasePrice ?? a.UnitPrice).ToList() : assets.OrderByDescending(a => a.PurchasePrice ?? a.UnitPrice).ToList(),
                "stock" => ascending ? assets.OrderBy(a => a.CurrentStock).ToList() : assets.OrderByDescending(a => a.CurrentStock).ToList(),
                _ => ascending ? assets.OrderBy(a => a.Name).ToList() : assets.OrderByDescending(a => a.Name).ToList()
            };
        }

        // Helper method to calculate asset statistics
        private async Task<AssetStatistics> CalculateAssetStatistics()
        {
            var statistics = new AssetStatistics();

            // Total counts
            statistics.TotalEquipment = await _context.Equipment.CountAsync();
            statistics.TotalInventoryItems = await _context.InventoryItems.CountAsync();
            statistics.TotalAssets = statistics.TotalEquipment + statistics.TotalInventoryItems;

            // Equipment statistics
            statistics.OperationalEquipment = await _context.Equipment.CountAsync(e => e.Status == EquipmentStatus.Active);
            statistics.EquipmentUnderMaintenance = await _context.Equipment.CountAsync(e => e.Status == EquipmentStatus.Inactive);
            statistics.CriticalEquipment = await _context.Alerts.CountAsync(a => a.EquipmentId.HasValue && a.Priority == AlertPriority.High && a.Status == AlertStatus.Open);

            // Inventory statistics  
            var inventoryItems = await _context.InventoryItems.Include(i => i.InventoryStocks).ToListAsync();
            statistics.InStockItems = inventoryItems.Count(i => (i.InventoryStocks?.Sum(s => s.Quantity) ?? 0) > i.MinimumStockLevel);
            statistics.LowStockItems = inventoryItems.Count(i => (i.InventoryStocks?.Sum(s => s.Quantity) ?? 0) <= i.MinimumStockLevel && (i.InventoryStocks?.Sum(s => s.Quantity) ?? 0) > 0);
            statistics.OutOfStockItems = inventoryItems.Count(i => (i.InventoryStocks?.Sum(s => s.Quantity) ?? 0) == 0);

            // Financial metrics
            statistics.TotalAssetValue = inventoryItems.Sum(i => (i.InventoryStocks?.Sum(s => s.Quantity * s.UnitCost) ?? 0));
            statistics.MonthlyMaintenanceCost = 0; // Would need to calculate from maintenance logs

            // Performance metrics
            statistics.EquipmentUptime = statistics.TotalEquipment > 0 ? (double)statistics.OperationalEquipment / statistics.TotalEquipment * 100 : 0;
            statistics.MaintenanceTasksCompleted = await _context.MaintenanceLogs.CountAsync(ml => ml.LogDate >= DateTime.Now.AddMonths(-1));

            return statistics;
        }

        // Helper method to get asset filter options
        private async Task<AssetFilterOptions> GetAssetFilterOptions()
        {
            var filterOptions = new AssetFilterOptions();

            // Asset types
            filterOptions.Types = new List<AssetTypeOption>
            {
                new() { Value = "Equipment", Text = "Equipment", Count = await _context.Equipment.CountAsync(), Icon = "bi-gear" },
                new() { Value = "Inventory", Text = "Inventory", Count = await _context.InventoryItems.CountAsync(), Icon = "bi-box" }
            };

            // Equipment categories
            var equipmentCategories = await _context.EquipmentTypes
                .Select(et => new AssetCategoryOption
                {
                    Value = et.EquipmentTypeName,
                    Text = et.EquipmentTypeName,
                    Count = et.Equipments != null ? et.Equipments.Count : 0
                })
                .ToListAsync();

            // Inventory categories
            var inventoryCategories = Enum.GetValues<ItemCategory>()
                .Select(ic => new AssetCategoryOption
                {
                    Value = ic.ToString(),
                    Text = ic.ToString(),
                    Count = _context.InventoryItems.Count(i => i.Category == ic)
                })
                .ToList();

            filterOptions.Categories = equipmentCategories.Concat(inventoryCategories).ToList();

            // Equipment statuses
            var equipmentStatuses = Enum.GetValues<EquipmentStatus>()
                .Select(es => new AssetStatusOption
                {
                    Value = es.ToString(),
                    Text = es.ToString(),
                    Count = _context.Equipment.Count(e => e.Status == es),
                    Color = es == EquipmentStatus.Active ? "success" : "warning",
                    Icon = es == EquipmentStatus.Active ? "bi-check-circle" : "bi-exclamation-triangle"
                })
                .ToList();

            // Inventory statuses
            var inventoryStatuses = new List<AssetStatusOption>
            {
                new() { Value = "In Stock", Text = "In Stock", Color = "success", Icon = "bi-check-circle" },
                new() { Value = "Low Stock", Text = "Low Stock", Color = "warning", Icon = "bi-exclamation-triangle" },
                new() { Value = "Out of Stock", Text = "Out of Stock", Color = "danger", Icon = "bi-x-circle" }
            };

            filterOptions.Statuses = equipmentStatuses.Concat(inventoryStatuses).ToList();

            // Locations (buildings)
            filterOptions.Locations = await _context.Buildings
                .Select(b => new AssetLocationOption
                {
                    Value = b.BuildingName,
                    Text = b.BuildingName,
                    Count = b.Equipments != null ? b.Equipments.Count : 0,
                    Building = b.BuildingName
                })
                .ToListAsync();

            return filterOptions;
        }

        // Helper method to get available bulk actions
        private List<AssetBulkAction> GetAssetBulkActions()
        {
            return new List<AssetBulkAction>
            {
                new()
                {
                    Id = "activate",
                    Name = "Activate",
                    Description = "Activate selected equipment",
                    Icon = "bi-play-circle",
                    Color = "success",
                    RequiresConfirmation = true,
                    ConfirmationMessage = "Are you sure you want to activate the selected equipment?",
                    ApplicableTypes = new List<string> { "Equipment" }
                },
                new()
                {
                    Id = "deactivate",
                    Name = "Deactivate",
                    Description = "Deactivate selected equipment",
                    Icon = "bi-pause-circle",
                    Color = "warning",
                    RequiresConfirmation = true,
                    ConfirmationMessage = "Are you sure you want to deactivate the selected equipment?",
                    ApplicableTypes = new List<string> { "Equipment" }
                },
                new()
                {
                    Id = "schedule-maintenance",
                    Name = "Schedule Maintenance",
                    Description = "Schedule maintenance for selected equipment",
                    Icon = "bi-calendar-plus",
                    Color = "info",
                    RequiresConfirmation = true,
                    ConfirmationMessage = "Schedule maintenance for selected equipment?",
                    ApplicableTypes = new List<string> { "Equipment" }
                },
                new()
                {
                    Id = "restock",
                    Name = "Add to Restock List",
                    Description = "Add selected items to restock list",
                    Icon = "bi-plus-circle",
                    Color = "primary",
                    RequiresConfirmation = false,
                    ApplicableTypes = new List<string> { "Inventory" }
                },
                new()
                {
                    Id = "delete",
                    Name = "Delete",
                    Description = "Delete selected items",
                    Icon = "bi-trash",
                    Color = "danger",
                    RequiresConfirmation = true,
                    ConfirmationMessage = "Are you sure you want to delete the selected items? This action cannot be undone.",
                    ApplicableTypes = new List<string> { "Equipment", "Inventory" }
                }
            };
        }

        // Helper method to get export options
        private List<AssetExportOption> GetAssetExportOptions()
        {
            return new List<AssetExportOption>
            {
                new()
                {
                    Id = "csv",
                    Name = "Export as CSV",
                    Format = "CSV",
                    Icon = "bi-filetype-csv",
                    IncludesImages = false,
                    AvailableFields = new List<string> { "Name", "Type", "Category", "Status", "Location", "Description" }
                },
                new()
                {
                    Id = "excel",
                    Name = "Export as Excel",
                    Format = "Excel",
                    Icon = "bi-filetype-xlsx",
                    IncludesImages = false,
                    AvailableFields = new List<string> { "Name", "Type", "Category", "Status", "Location", "Description", "Value", "Stock" }
                },
                new()
                {
                    Id = "pdf",
                    Name = "Export as PDF",
                    Format = "PDF",
                    Icon = "bi-filetype-pdf",
                    IncludesImages = true,
                    AvailableFields = new List<string> { "Name", "Type", "Category", "Status", "Location" }
                }
            };
        }

        // Helper method to get column options
        private List<AssetColumnOption> GetAssetColumnOptions()
        {
            return new List<AssetColumnOption>
            {
                new() { Id = "name", Name = "Name", IsVisible = true, IsRequired = true, Order = 1 },
                new() { Id = "type", Name = "Type", IsVisible = true, IsRequired = true, Order = 2 },
                new() { Id = "category", Name = "Category", IsVisible = true, IsRequired = false, Order = 3 },
                new() { Id = "status", Name = "Status", IsVisible = true, IsRequired = false, Order = 4 },
                new() { Id = "location", Name = "Location", IsVisible = true, IsRequired = false, Order = 5 },
                new() { Id = "description", Name = "Description", IsVisible = false, IsRequired = false, Order = 6 },
                new() { Id = "installation-date", Name = "Installation Date", IsVisible = false, IsRequired = false, Order = 7 },
                new() { Id = "value", Name = "Value", IsVisible = false, IsRequired = false, Order = 8 },
                new() { Id = "stock", Name = "Stock", IsVisible = true, IsRequired = false, Order = 9 },
                new() { Id = "alerts", Name = "Alerts", IsVisible = true, IsRequired = false, Order = 10 }
            };
        }
    }
}
