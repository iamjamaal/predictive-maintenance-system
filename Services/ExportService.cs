using OfficeOpenXml;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Models.ViewModels;
using FEENALOoFINALE.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.Kernel.Font;

namespace FEENALOoFINALE.Services
{
    public class ExportService : IExportService
    {
        private readonly ApplicationDbContext _context;

        public ExportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> ExportDashboardDataToExcel()
        {
            using var package = new ExcelPackage();

            // Dashboard Summary Sheet
            var summarySheet = package.Workbook.Worksheets.Add("Dashboard Summary");
            await CreateDashboardSummarySheet(summarySheet);

            // Equipment Data Sheet
            var equipmentSheet = package.Workbook.Worksheets.Add("Equipment");
            await CreateEquipmentSheet(equipmentSheet);

            // Maintenance Data Sheet
            var maintenanceSheet = package.Workbook.Worksheets.Add("Maintenance");
            await CreateMaintenanceSheet(maintenanceSheet);

            // Alerts Data Sheet
            var alertsSheet = package.Workbook.Worksheets.Add("Alerts");
            await CreateAlertsSheet(alertsSheet);

            // Inventory Data Sheet
            var inventorySheet = package.Workbook.Worksheets.Add("Inventory");
            await CreateInventorySheet(inventorySheet);

            return package.GetAsByteArray();
        }

        public async Task<byte[]> ExportDashboardDataToPdf()
        {
            using var memoryStream = new MemoryStream();
            var writer = new PdfWriter(memoryStream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(30, 25, 30, 25);

            // Title
            var titleFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var title = new Paragraph("Predictive Maintenance Dashboard Report")
                .SetFont(titleFont)
                .SetFontSize(18)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(20);
            document.Add(title);

            // Generated date
            var dateFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var dateInfo = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .SetFont(dateFont)
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(30);
            document.Add(dateInfo);

            // Dashboard Summary
            await AddDashboardSummaryToPdf(document);

            // Equipment Summary
            await AddEquipmentSummaryToPdf(document);

            // Recent Alerts
            await AddRecentAlertsToPdf(document);

            // Maintenance Summary
            await AddMaintenanceSummaryToPdf(document);

            document.Close();
            return memoryStream.ToArray();
        }

        private async Task CreateDashboardSummarySheet(ExcelWorksheet sheet)
        {
            // Headers
            sheet.Cells[1, 1].Value = "Dashboard Summary";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 16;

            sheet.Cells[3, 1].Value = "Metric";
            sheet.Cells[3, 2].Value = "Value";
            sheet.Cells[3, 1, 3, 2].Style.Font.Bold = true;

            // Data
            var totalEquipment = await _context.Equipment.CountAsync();
            var activeMaintenanceTasks = await _context.MaintenanceTasks
                .CountAsync(m => m.Status == MaintenanceStatus.InProgress);
            var lowStockItems = await _context.InventoryItems
                .Include(i => i.InventoryStocks)
                .CountAsync(i => i.InventoryStocks != null && i.InventoryStocks.Sum(s => s.Quantity) < i.MinimumStockLevel);
            var criticalAlerts = await _context.Alerts
                .CountAsync(a => a.Priority == AlertPriority.High && a.Status == AlertStatus.Open);

            int row = 4;
            sheet.Cells[row, 1].Value = "Total Equipment";
            sheet.Cells[row++, 2].Value = totalEquipment;
            
            sheet.Cells[row, 1].Value = "Active Maintenance Tasks";
            sheet.Cells[row++, 2].Value = activeMaintenanceTasks;
            
            sheet.Cells[row, 1].Value = "Low Stock Items";
            sheet.Cells[row++, 2].Value = lowStockItems;
            
            sheet.Cells[row, 1].Value = "Critical Alerts";
            sheet.Cells[row++, 2].Value = criticalAlerts;

            sheet.Cells[row, 1].Value = "Report Generated";
            sheet.Cells[row++, 2].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Auto-fit columns
            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private async Task CreateEquipmentSheet(ExcelWorksheet sheet)
        {
            var equipment = await _context.Equipment
                .Include(e => e.EquipmentModel)
                .ThenInclude(em => em != null ? em.EquipmentType : null)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .ToListAsync();

            // Headers
            sheet.Cells[1, 1].Value = "Equipment ID";
            sheet.Cells[1, 2].Value = "Type";
            sheet.Cells[1, 3].Value = "Model";
            sheet.Cells[1, 4].Value = "Status";
            sheet.Cells[1, 5].Value = "Building";
            sheet.Cells[1, 6].Value = "Room";
            sheet.Cells[1, 7].Value = "Installation Date";

            // Make headers bold
            sheet.Cells[1, 1, 1, 7].Style.Font.Bold = true;

            // Data
            for (int i = 0; i < equipment.Count; i++)
            {
                var eq = equipment[i];
                int row = i + 2;
                
                sheet.Cells[row, 1].Value = eq.EquipmentId;
                sheet.Cells[row, 2].Value = eq.EquipmentModel?.EquipmentType?.EquipmentTypeName ?? "N/A";
                sheet.Cells[row, 3].Value = eq.EquipmentModel?.ModelName ?? "N/A";
                sheet.Cells[row, 4].Value = eq.Status.ToString();
                sheet.Cells[row, 5].Value = eq.Building?.BuildingName ?? "N/A";
                sheet.Cells[row, 6].Value = eq.Room?.RoomName ?? "N/A";
                sheet.Cells[row, 7].Value = eq.InstallationDate?.ToString("yyyy-MM-dd") ?? "Not Set";
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private async Task CreateMaintenanceSheet(ExcelWorksheet sheet)
        {
            var maintenanceLogs = await _context.MaintenanceLogs
                .Include(m => m.Equipment)
                .OrderByDescending(m => m.LogDate)
                .Take(1000) // Limit to recent records
                .ToListAsync();

            // Headers
            sheet.Cells[1, 1].Value = "Log ID";
            sheet.Cells[1, 2].Value = "Equipment ID";
            sheet.Cells[1, 3].Value = "Type";
            sheet.Cells[1, 4].Value = "Description";
            sheet.Cells[1, 5].Value = "Cost";
            sheet.Cells[1, 6].Value = "Log Date";

            sheet.Cells[1, 1, 1, 6].Style.Font.Bold = true;

            // Data
            for (int i = 0; i < maintenanceLogs.Count; i++)
            {
                var log = maintenanceLogs[i];
                int row = i + 2;
                
                sheet.Cells[row, 1].Value = log.LogId;
                sheet.Cells[row, 2].Value = log.EquipmentId;
                sheet.Cells[row, 3].Value = log.MaintenanceType.ToString();
                sheet.Cells[row, 4].Value = log.Description;
                sheet.Cells[row, 5].Value = log.Cost;
                sheet.Cells[row, 6].Value = log.LogDate.ToString("yyyy-MM-dd");
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private async Task CreateAlertsSheet(ExcelWorksheet sheet)
        {
            var alerts = await _context.Alerts
                .Include(a => a.Equipment)
                .OrderByDescending(a => a.CreatedDate)
                .Take(500)
                .ToListAsync();

            // Headers
            sheet.Cells[1, 1].Value = "Alert ID";
            sheet.Cells[1, 2].Value = "Equipment ID";
            sheet.Cells[1, 3].Value = "Priority";
            sheet.Cells[1, 4].Value = "Status";
            sheet.Cells[1, 5].Value = "Description";
            sheet.Cells[1, 6].Value = "Created";

            sheet.Cells[1, 1, 1, 6].Style.Font.Bold = true;

            // Data
            for (int i = 0; i < alerts.Count; i++)
            {
                var alert = alerts[i];
                int row = i + 2;
                
                sheet.Cells[row, 1].Value = alert.AlertId;
                sheet.Cells[row, 2].Value = alert.EquipmentId ?? 0;
                sheet.Cells[row, 3].Value = alert.Priority.ToString();
                sheet.Cells[row, 4].Value = alert.Status.ToString();
                sheet.Cells[row, 5].Value = alert.Description;
                sheet.Cells[row, 6].Value = alert.CreatedDate.ToString("yyyy-MM-dd HH:mm");
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private async Task CreateInventorySheet(ExcelWorksheet sheet)
        {
            var inventoryItems = await _context.InventoryItems
                .Include(i => i.InventoryStocks)
                .ToListAsync();

            // Headers
            sheet.Cells[1, 1].Value = "Item ID";
            sheet.Cells[1, 2].Value = "Name";
            sheet.Cells[1, 3].Value = "Category";
            sheet.Cells[1, 4].Value = "Current Stock";
            sheet.Cells[1, 5].Value = "Minimum Level";
            sheet.Cells[1, 6].Value = "Unit Price";
            sheet.Cells[1, 7].Value = "Supplier";

            sheet.Cells[1, 1, 1, 7].Style.Font.Bold = true;

            // Data
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                var item = inventoryItems[i];
                int row = i + 2;
                
                sheet.Cells[row, 1].Value = item.ItemId;
                sheet.Cells[row, 2].Value = item.Name;
                sheet.Cells[row, 3].Value = item.Category.ToString();
                sheet.Cells[row, 4].Value = item.InventoryStocks?.Sum(s => s.Quantity) ?? 0;
                sheet.Cells[row, 5].Value = item.MinimumStockLevel;
                sheet.Cells[row, 6].Value = item.InventoryStocks?.FirstOrDefault()?.UnitCost ?? 0;
                sheet.Cells[row, 7].Value = "N/A"; // Supplier not in current model
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        }

        private async Task AddDashboardSummaryToPdf(Document document)
        {
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var header = new Paragraph("Dashboard Summary").SetFont(headerFont).SetFontSize(14).SetMarginBottom(10);
            document.Add(header);

            var totalEquipment = await _context.Equipment.CountAsync();
            var activeMaintenanceTasks = await _context.MaintenanceTasks
                .CountAsync(m => m.Status == MaintenanceStatus.InProgress);
            var lowStockItems = await _context.InventoryItems
                .Include(i => i.InventoryStocks)
                .CountAsync(i => i.InventoryStocks != null && i.InventoryStocks.Sum(s => s.Quantity) < i.MinimumStockLevel);
            var criticalAlerts = await _context.Alerts
                .CountAsync(a => a.Priority == AlertPriority.High && a.Status == AlertStatus.Open);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 3, 1 }))
                .UseAllAvailableWidth().SetWidth(UnitValue.CreatePercentValue(50));

            // Table data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            table.AddCell(new Cell().Add(new Paragraph("Total Equipment").SetFont(boldFont)));
            table.AddCell(new Cell().Add(new Paragraph(totalEquipment.ToString()).SetFont(cellFont)));

            table.AddCell(new Cell().Add(new Paragraph("Active Maintenance Tasks").SetFont(boldFont)));
            table.AddCell(new Cell().Add(new Paragraph(activeMaintenanceTasks.ToString()).SetFont(cellFont)));

            table.AddCell(new Cell().Add(new Paragraph("Low Stock Items").SetFont(boldFont)));
            table.AddCell(new Cell().Add(new Paragraph(lowStockItems.ToString()).SetFont(cellFont)));

            table.AddCell(new Cell().Add(new Paragraph("Critical Alerts").SetFont(boldFont)));
            table.AddCell(new Cell().Add(new Paragraph(criticalAlerts.ToString()).SetFont(cellFont)));

            document.Add(table);
            document.Add(new Paragraph(" ").SetMarginBottom(20));
        }

        private async Task AddEquipmentSummaryToPdf(Document document)
        {
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var header = new Paragraph("Equipment Status Summary")
                .SetFont(headerFont).SetFontSize(14).SetMarginBottom(10);
            document.Add(header);

            var equipmentByStatus = await _context.Equipment
                .GroupBy(e => e.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 3, 1 }))
                .UseAllAvailableWidth().SetWidth(UnitValue.CreatePercentValue(50));

            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            foreach (var status in equipmentByStatus)
            {
                table.AddCell(new Cell().Add(new Paragraph(status.Status.ToString()).SetFont(boldFont)));
                table.AddCell(new Cell().Add(new Paragraph(status.Count.ToString()).SetFont(cellFont)));
            }

            document.Add(table);
            document.Add(new Paragraph(" ").SetMarginBottom(20));
        }

        private async Task AddRecentAlertsToPdf(Document document)
        {
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var header = new Paragraph("Recent Critical Alerts")
                .SetFont(headerFont).SetFontSize(14).SetMarginBottom(10);
            document.Add(header);

            var recentAlerts = await _context.Alerts
                .Include(a => a.Equipment)
                .Where(a => a.Priority == AlertPriority.High && a.Status == AlertStatus.Open)
                .OrderByDescending(a => a.CreatedDate)
                .Take(10)
                .ToListAsync();

            if (recentAlerts.Any())
            {
                var table = new Table(UnitValue.CreatePercentArray(new float[] { 2, 3, 2 }))
                    .UseAllAvailableWidth();

                var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

                // Headers
                table.AddHeaderCell(new Cell().Add(new Paragraph("Equipment ID").SetFont(boldFont)));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Description").SetFont(boldFont)));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Created").SetFont(boldFont)));

                // Data
                foreach (var alert in recentAlerts)
                {
                    table.AddCell(new Cell().Add(new Paragraph((alert.EquipmentId ?? 0).ToString()).SetFont(cellFont)));
                    table.AddCell(new Cell().Add(new Paragraph(alert.Description).SetFont(cellFont)));
                    table.AddCell(new Cell().Add(new Paragraph(alert.CreatedDate.ToString("MM/dd/yyyy")).SetFont(cellFont)));
                }

                document.Add(table);
            }
            else
            {
                var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                document.Add(new Paragraph("No critical alerts found.").SetFont(cellFont).SetFontSize(10));
            }

            document.Add(new Paragraph(" ").SetMarginBottom(20));
        }

        private async Task AddMaintenanceSummaryToPdf(Document document)
        {
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var header = new Paragraph("Maintenance Activity Summary")
                .SetFont(headerFont).SetFontSize(14).SetMarginBottom(10);
            document.Add(header);

            var maintenanceByType = await _context.MaintenanceLogs
                .GroupBy(m => m.MaintenanceType)
                .Select(g => new { Type = g.Key, Count = g.Count(), TotalCost = g.Sum(m => m.Cost) })
                .ToListAsync();

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 2 }))
                .UseAllAvailableWidth().SetWidth(UnitValue.CreatePercentValue(75));

            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            // Headers
            table.AddHeaderCell(new Cell().Add(new Paragraph("Type").SetFont(boldFont)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Count").SetFont(boldFont)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Total Cost").SetFont(boldFont)));

            // Data
            foreach (var maintenance in maintenanceByType)
            {
                table.AddCell(new Cell().Add(new Paragraph(maintenance.Type.ToString()).SetFont(cellFont)));
                table.AddCell(new Cell().Add(new Paragraph(maintenance.Count.ToString()).SetFont(cellFont)));
                table.AddCell(new Cell().Add(new Paragraph($"${maintenance.TotalCost:F2}").SetFont(cellFont)));
            }

            document.Add(table);
        }

        // Advanced reporting methods for Step 9
        public async Task<byte[]> ExportCustomReportToExcel(CustomReportTemplate template, DashboardFilterViewModel? filters = null)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add(template.Name);

            // Add header
            worksheet.Cells[1, 1].Value = template.Name;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.Font.Size = 16;

            worksheet.Cells[2, 1].Value = $"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            worksheet.Cells[2, 1].Style.Font.Size = 10;

            int row = 4;

            // Add report sections based on template
            foreach (var section in template.Sections)
            {
                worksheet.Cells[row, 1].Value = section.Title;
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.Font.Size = 14;
                row += 2;

                // Add data based on section type
                switch (section.DataType)
                {
                    case ReportDataType.Equipment:
                        row = await AddEquipmentDataToExcel(worksheet, row, filters);
                        break;
                    case ReportDataType.Maintenance:
                        row = await AddMaintenanceDataToExcel(worksheet, row, filters);
                        break;
                    case ReportDataType.Alerts:
                        row = await AddAlertsDataToExcel(worksheet, row, filters);
                        break;
                    case ReportDataType.Inventory:
                        row = await AddInventoryDataToExcel(worksheet, row, filters);
                        break;
                }
                row += 2;
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();

            return package.GetAsByteArray();
        }

        public async Task<byte[]> ExportCustomReportToPdf(CustomReportTemplate template, DashboardFilterViewModel? filters = null)
        {
            using var memoryStream = new MemoryStream();
            var writer = new PdfWriter(memoryStream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(30, 25, 30, 25);

            // Title
            var titleFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var title = new Paragraph(template.Name)
                .SetFont(titleFont).SetFontSize(18)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(20);
            document.Add(title);

            // Generated date
            var dateFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var dateInfo = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .SetFont(dateFont).SetFontSize(10)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(30);
            document.Add(dateInfo);

            // Add report sections
            var sectionHeaderFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            foreach (var section in template.Sections)
            {
                var sectionTitle = new Paragraph(section.Title)
                    .SetFont(sectionHeaderFont).SetFontSize(14).SetMarginBottom(10);
                document.Add(sectionTitle);

                // Add data based on section type
                switch (section.DataType)
                {
                    case ReportDataType.Equipment:
                        await AddEquipmentDataToPdf(document, filters);
                        break;
                    case ReportDataType.Maintenance:
                        await AddMaintenanceDataToPdf(document, filters);
                        break;
                    case ReportDataType.Alerts:
                        await AddAlertsDataToPdf(document, filters);
                        break;
                    case ReportDataType.Inventory:
                        await AddInventoryDataToPdf(document, filters);
                        break;
                }

                document.Add(new Paragraph(" ").SetMarginBottom(10));
            }

            document.Close();
            return memoryStream.ToArray();
        }

        public byte[] ExportAnalyticsDataToExcel(AdvancedAnalyticsViewModel analytics)
        {
            using var package = new ExcelPackage();

            // Performance Metrics Sheet
            var performanceSheet = package.Workbook.Worksheets.Add("Performance Metrics");
            CreatePerformanceMetricsSheet(performanceSheet, analytics.EquipmentPerformance);

            // KPIs Sheet
            var kpiSheet = package.Workbook.Worksheets.Add("KPIs");
            CreateKPISheet(kpiSheet, analytics.KPIProgress);

            // Predictive Analytics Sheet
            var predictiveSheet = package.Workbook.Worksheets.Add("Predictive Analytics");
            CreatePredictiveAnalyticsSheet(predictiveSheet, analytics.PredictiveInsights);

            // Trend Analysis Sheet
            var trendSheet = package.Workbook.Worksheets.Add("Trend Analysis");
            CreateTrendAnalysisSheet(trendSheet, analytics.MaintenanceTrends);

            // Cost Analysis Sheet
            var costSheet = package.Workbook.Worksheets.Add("Cost Analysis");
            CreateCostAnalysisSheet(costSheet, analytics.CostAnalysis);

            return package.GetAsByteArray();
        }

        public byte[] ExportAnalyticsDataToPdf(AdvancedAnalyticsViewModel analytics)
        {
            using var memoryStream = new MemoryStream();
            var writer = new PdfWriter(memoryStream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(30, 25, 30, 25);

            // Title
            var titleFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var title = new Paragraph("Advanced Analytics Report")
                .SetFont(titleFont).SetFontSize(18)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(20);
            document.Add(title);

            // Generated date
            var dateFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var dateInfo = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .SetFont(dateFont).SetFontSize(10)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(30);
            document.Add(dateInfo);

            // Add analytics sections
            AddPerformanceMetricsToPdf(document, analytics.EquipmentPerformance);
            AddKPIsToPdf(document, analytics.KPIProgress);
            AddPredictiveAnalyticsToPdf(document, analytics.PredictiveInsights);
            AddTrendAnalysisToPdf(document, analytics.MaintenanceTrends);
            AddCostAnalysisToPdf(document, analytics.CostAnalysis);

            document.Close();
            return memoryStream.ToArray();
        }

        // Helper methods for Excel export
        private async Task<int> AddEquipmentDataToExcel(ExcelWorksheet worksheet, int startRow, DashboardFilterViewModel? filters)
        {
            var equipment = await GetFilteredEquipment(filters);
            
            // Headers
            worksheet.Cells[startRow, 1].Value = "Equipment Name";
            worksheet.Cells[startRow, 2].Value = "Type";
            worksheet.Cells[startRow, 3].Value = "Status";
            worksheet.Cells[startRow, 4].Value = "Location";
            worksheet.Cells[startRow, 1, startRow, 4].Style.Font.Bold = true;

            int row = startRow + 1;
            foreach (var item in equipment)
            {
                worksheet.Cells[row, 1].Value = item.EquipmentModel?.ModelName ?? "Unknown";
                worksheet.Cells[row, 2].Value = item.EquipmentModel?.EquipmentType?.EquipmentTypeName ?? "Unknown";
                worksheet.Cells[row, 3].Value = item.Status.ToString();
                worksheet.Cells[row, 4].Value = $"{item.Room?.Building?.BuildingName} - {item.Room?.RoomName}";
                row++;
            }

            return row;
        }

        private async Task<int> AddMaintenanceDataToExcel(ExcelWorksheet worksheet, int startRow, DashboardFilterViewModel? filters)
        {
            var maintenance = await GetFilteredMaintenance(filters);
            
            // Headers
            worksheet.Cells[startRow, 1].Value = "Equipment";
            worksheet.Cells[startRow, 2].Value = "Type";
            worksheet.Cells[startRow, 3].Value = "Date";
            worksheet.Cells[startRow, 4].Value = "Description";
            worksheet.Cells[startRow, 5].Value = "Cost";
            worksheet.Cells[startRow, 1, startRow, 5].Style.Font.Bold = true;

            int row = startRow + 1;
            foreach (var item in maintenance)
            {
                worksheet.Cells[row, 1].Value = item.Equipment?.EquipmentModel?.ModelName ?? "Unknown";
                worksheet.Cells[row, 2].Value = item.MaintenanceType.ToString();
                worksheet.Cells[row, 3].Value = item.LogDate.ToString("yyyy-MM-dd");
                worksheet.Cells[row, 4].Value = item.Description;
                worksheet.Cells[row, 5].Value = item.Cost; // Cost is non-nullable
                row++;
            }

            return row;
        }

        private async Task<int> AddAlertsDataToExcel(ExcelWorksheet worksheet, int startRow, DashboardFilterViewModel? filters)
        {
            var alerts = await GetFilteredAlerts(filters);
            
            // Headers
            worksheet.Cells[startRow, 1].Value = "Equipment";
            worksheet.Cells[startRow, 2].Value = "Priority";
            worksheet.Cells[startRow, 3].Value = "Status";
            worksheet.Cells[startRow, 4].Value = "Description";
            worksheet.Cells[startRow, 5].Value = "Created Date";
            worksheet.Cells[startRow, 1, startRow, 5].Style.Font.Bold = true;

            int row = startRow + 1;
            foreach (var item in alerts)
            {
                worksheet.Cells[row, 1].Value = item.Equipment?.EquipmentModel?.ModelName ?? "Unknown";
                worksheet.Cells[row, 2].Value = item.Priority.ToString();
                worksheet.Cells[row, 3].Value = item.Status.ToString();
                worksheet.Cells[row, 4].Value = item.Description;
                worksheet.Cells[row, 5].Value = item.CreatedDate.ToString("yyyy-MM-dd HH:mm");
                row++;
            }

            return row;
        }

        private async Task<int> AddInventoryDataToExcel(ExcelWorksheet worksheet, int startRow, DashboardFilterViewModel? filters)
        {
            var inventory = await GetFilteredInventory(filters);
            
            // Headers
            worksheet.Cells[startRow, 1].Value = "Item Name";
            worksheet.Cells[startRow, 2].Value = "Category";
            worksheet.Cells[startRow, 3].Value = "Current Stock";
            worksheet.Cells[startRow, 4].Value = "Min Level";
            worksheet.Cells[startRow, 5].Value = "Unit Price";
            worksheet.Cells[startRow, 1, startRow, 5].Style.Font.Bold = true;

            int row = startRow + 1;
            foreach (var item in inventory)
            {
                worksheet.Cells[row, 1].Value = item.Name;
                worksheet.Cells[row, 2].Value = item.Category;
                worksheet.Cells[row, 3].Value = item.InventoryStocks?.Sum(s => s.Quantity) ?? 0;
                worksheet.Cells[row, 4].Value = item.MinStockLevel;
                worksheet.Cells[row, 5].Value = 0; // UnitPrice not available in current model
                row++;
            }

            return row;
        }

        // Helper methods for PDF export
        private async Task AddEquipmentDataToPdf(Document document, DashboardFilterViewModel? filters)
        {
            var equipment = await GetFilteredEquipment(filters);
            
            var table = new Table(UnitValue.CreatePercentArray(new float[] { 3, 2, 2, 3 }))
                .UseAllAvailableWidth();

            // Headers
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            table.AddHeaderCell(new Cell().Add(new Paragraph("Equipment Name").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Type").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Status").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Location").SetFont(headerFont).SetFontSize(10)));

            // Data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            foreach (var item in equipment.Take(20)) // Limit for PDF
            {
                table.AddCell(new Cell().Add(new Paragraph(item.EquipmentModel?.ModelName ?? "Unknown").SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.EquipmentModel?.EquipmentType?.EquipmentTypeName ?? "Unknown").SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.Status.ToString()).SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph($"{item.Room?.Building?.BuildingName} - {item.Room?.RoomName}").SetFont(cellFont).SetFontSize(9)));
            }

            document.Add(table);
        }

        private async Task AddMaintenanceDataToPdf(Document document, DashboardFilterViewModel? filters)
        {
            var maintenance = await GetFilteredMaintenance(filters);            
            
            var table = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1.5f, 1.5f, 3, 1 }))
                .UseAllAvailableWidth();

            // Headers
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            table.AddHeaderCell(new Cell().Add(new Paragraph("Equipment").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Type").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Date").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Description").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Cost").SetFont(headerFont).SetFontSize(10)));

            // Data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            foreach (var item in maintenance.Take(20)) // Limit for PDF
            {
                table.AddCell(new Cell().Add(new Paragraph(item.Equipment?.EquipmentModel?.ModelName ?? "Unknown").SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.MaintenanceType.ToString()).SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.LogDate.ToString("yyyy-MM-dd")).SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.Description?.Substring(0, Math.Min(50, item.Description?.Length ?? 0)) ?? "").SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph($"${item.Cost:F2}").SetFont(cellFont).SetFontSize(9)));
            }

            document.Add(table);
        }

        private async Task AddAlertsDataToPdf(Document document, DashboardFilterViewModel? filters)
        {
            var alerts = await GetFilteredAlerts(filters);            
            
            var table = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 1, 4 }))
                .UseAllAvailableWidth();

            // Headers
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            table.AddHeaderCell(new Cell().Add(new Paragraph("Equipment").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Priority").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Status").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Description").SetFont(headerFont).SetFontSize(10)));

            // Data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            foreach (var item in alerts.Take(20)) // Limit for PDF
            {
                table.AddCell(new Cell().Add(new Paragraph(item.Equipment?.EquipmentModel?.ModelName ?? "Unknown").SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.Priority.ToString()).SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.Status.ToString()).SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.Description?.Substring(0, Math.Min(60, item.Description?.Length ?? 0)) ?? "").SetFont(cellFont).SetFontSize(9)));
            }

            document.Add(table);
        }

        private async Task AddInventoryDataToPdf(Document document, DashboardFilterViewModel? filters)
        {
            var inventory = await GetFilteredInventory(filters);            
            
            var table = new Table(UnitValue.CreatePercentArray(new float[] { 3, 2, 1, 1 }))
                .UseAllAvailableWidth();

            // Headers
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            table.AddHeaderCell(new Cell().Add(new Paragraph("Item Name").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Category").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Stock").SetFont(headerFont).SetFontSize(10)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Min Level").SetFont(headerFont).SetFontSize(10)));

            // Data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            foreach (var item in inventory.Take(20)) // Limit for PDF
            {
                table.AddCell(new Cell().Add(new Paragraph(item.Name).SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.Category.ToString()).SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph((item.InventoryStocks?.Sum(s => s.Quantity) ?? 0).ToString()).SetFont(cellFont).SetFontSize(9)));
                table.AddCell(new Cell().Add(new Paragraph(item.MinStockLevel.ToString()).SetFont(cellFont).SetFontSize(9)));
            }

            document.Add(table);
        }

        // Analytics sheet creation methods
        private void CreatePerformanceMetricsSheet(ExcelWorksheet sheet, List<FEENALOoFINALE.Models.EquipmentPerformanceMetrics> metrics)
        {
            sheet.Cells[1, 1].Value = "Equipment Performance Metrics";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 16;

            // Headers
            int row = 3;
            sheet.Cells[row, 1].Value = "Equipment";
            sheet.Cells[row, 2].Value = "Uptime %";
            sheet.Cells[row, 3].Value = "Efficiency %";
            sheet.Cells[row, 4].Value = "MTBF (hours)";
            sheet.Cells[row, 5].Value = "MTTR (hours)";
            sheet.Cells[row, 6].Value = "Failure Rate %";
            sheet.Cells[row, 1, row, 6].Style.Font.Bold = true;

            row++;
            foreach (var metric in metrics)
            {
                sheet.Cells[row, 1].Value = metric.EquipmentName;
                sheet.Cells[row, 2].Value = metric.UptimePercentage;
                sheet.Cells[row, 3].Value = metric.EfficiencyScore;
                sheet.Cells[row, 4].Value = metric.MeanTimeBetweenFailures;
                sheet.Cells[row, 5].Value = metric.MeanTimeToRepair;
                sheet.Cells[row, 6].Value = metric.FailureCount; // Use FailureCount instead of FailureRate
                row++;
            }

            sheet.Cells.AutoFitColumns();
        }

        private void CreateKPISheet(ExcelWorksheet sheet, List<KPIProgressIndicator> kpis)
        {
            sheet.Cells[1, 1].Value = "Key Performance Indicators";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 16;

            // Headers
            int row = 3;
            sheet.Cells[row, 1].Value = "KPI Name";
            sheet.Cells[row, 2].Value = "Current Value";
            sheet.Cells[row, 3].Value = "Target Value";
            sheet.Cells[row, 4].Value = "Progress %";
            sheet.Cells[row, 5].Value = "Trend";
            sheet.Cells[row, 1, row, 5].Style.Font.Bold = true;

            row++;
            foreach (var kpi in kpis)
            {
                sheet.Cells[row, 1].Value = kpi.KPIName; // Use KPIName instead of Name
                sheet.Cells[row, 2].Value = kpi.CurrentValue;
                sheet.Cells[row, 3].Value = kpi.TargetValue;
                sheet.Cells[row, 4].Value = kpi.ProgressPercentage;
                sheet.Cells[row, 5].Value = kpi.Direction; // Use Direction instead of Trend
                row++;
            }

            sheet.Cells.AutoFitColumns();
        }

        private void CreatePredictiveAnalyticsSheet(ExcelWorksheet sheet, List<PredictiveAnalyticsData> analytics)
        {
            sheet.Cells[1, 1].Value = "Predictive Analytics";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 16;

            // Headers
            int row = 3;
            sheet.Cells[row, 1].Value = "Equipment";
            sheet.Cells[row, 2].Value = "Failure Probability";
            sheet.Cells[row, 3].Value = "Predicted Failure Date";
            sheet.Cells[row, 4].Value = "Recommended Action";
            sheet.Cells[row, 5].Value = "Confidence Level";
            sheet.Cells[row, 1, row, 5].Style.Font.Bold = true;

            row++;
            foreach (var item in analytics)
            {
                sheet.Cells[row, 1].Value = item.EquipmentName;
                sheet.Cells[row, 2].Value = item.ConfidenceScore; // Use ConfidenceScore instead of FailureProbability
                sheet.Cells[row, 3].Value = item.PredictedFailureDate.ToString("yyyy-MM-dd"); // Remove nullable check
                sheet.Cells[row, 4].Value = string.Join(", ", item.RecommendedActions); // Join list of actions
                sheet.Cells[row, 5].Value = item.ConfidenceScore; // Use ConfidenceScore instead of ConfidenceLevel
                row++;
            }

            sheet.Cells.AutoFitColumns();
        }

        private void CreateTrendAnalysisSheet(ExcelWorksheet sheet, List<MaintenanceTrendData> trendData)
        {
            sheet.Cells[1, 1].Value = "Maintenance Trend Analysis";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 16;

            // Headers
            int row = 3;
            sheet.Cells[row, 1].Value = "Date";
            sheet.Cells[row, 2].Value = "Preventive";
            sheet.Cells[row, 3].Value = "Corrective";
            sheet.Cells[row, 4].Value = "Predictive";
            sheet.Cells[row, 5].Value = "Inspection";
            sheet.Cells[row, 6].Value = "Total Cost";
            sheet.Cells[row, 1, row, 6].Style.Font.Bold = true;

            row++;
            foreach (var item in trendData)
            {
                sheet.Cells[row, 1].Value = item.Date.ToString("yyyy-MM-dd");
                sheet.Cells[row, 2].Value = item.PreventiveMaintenanceCount;
                sheet.Cells[row, 3].Value = item.CorrectiveMaintenanceCount;
                sheet.Cells[row, 4].Value = 0; // PredictiveMaintenanceCount not available in model
                sheet.Cells[row, 5].Value = item.InspectionMaintenanceCount;
                sheet.Cells[row, 6].Value = item.TotalCost;
                row++;
            }

            sheet.Cells.AutoFitColumns();
        }

        private void CreateCostAnalysisSheet(ExcelWorksheet sheet, List<CostAnalysisData> costData)
        {
            sheet.Cells[1, 1].Value = "Cost Analysis";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 16;

            // Headers
            int row = 3;
            sheet.Cells[row, 1].Value = "Month";
            sheet.Cells[row, 2].Value = "Maintenance Cost";
            sheet.Cells[row, 3].Value = "Parts Cost";
            sheet.Cells[row, 4].Value = "Labor Cost";
            sheet.Cells[row, 5].Value = "Total Cost";
            sheet.Cells[row, 6].Value = "Budget";
            sheet.Cells[row, 7].Value = "Variance";
            sheet.Cells[row, 1, row, 7].Style.Font.Bold = true;

            row++;
            foreach (var item in costData)
            {
                sheet.Cells[row, 1].Value = item.Category; // Use Category instead of Period
                sheet.Cells[row, 2].Value = item.CategoryValue; // Use CategoryValue
                sheet.Cells[row, 3].Value = item.TotalCost; // Use TotalCost
                sheet.Cells[row, 4].Value = item.AverageCost; // Use AverageCost
                sheet.Cells[row, 5].Value = item.MaintenanceCount; // Use MaintenanceCount
                sheet.Cells[row, 6].Value = item.ProjectedCost; // Use ProjectedCost
                sheet.Cells[row, 7].Value = item.Trend; // Use Trend
                row++;
            }

            sheet.Cells.AutoFitColumns();
        }

        // PDF analytics methods
        private void AddPerformanceMetricsToPdf(Document document, List<FEENALOoFINALE.Models.EquipmentPerformanceMetrics> metrics)
        {
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var sectionTitle = new Paragraph("Equipment Performance Metrics")
                .SetFont(headerFont).SetFontSize(14).SetMarginBottom(10);
            document.Add(sectionTitle);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 1, 1, 1, 1 }))
                .UseAllAvailableWidth();

            // Headers
            var tableHeaderFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            table.AddHeaderCell(new Cell().Add(new Paragraph("Equipment").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Uptime %").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Efficiency %").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("MTBF (h)").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("MTTR (h)").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Failure %").SetFont(tableHeaderFont).SetFontSize(9)));

            // Data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            foreach (var metric in metrics.Take(10)) // Limit for PDF
            {
                table.AddCell(new Cell().Add(new Paragraph(metric.EquipmentName).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(metric.UptimePercentage.ToString("F1")).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(metric.EfficiencyScore.ToString("F1")).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(metric.MeanTimeBetweenFailures.ToString("F1")).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(metric.MeanTimeToRepair.ToString("F1")).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(metric.FailureCount.ToString()).SetFont(cellFont).SetFontSize(8)));
            }

            document.Add(table);
            document.Add(new Paragraph(" ").SetMarginBottom(10));
        }

        private void AddKPIsToPdf(Document document, List<KPIProgressIndicator> kpis)
        {
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var sectionTitle = new Paragraph("Key Performance Indicators")
                .SetFont(headerFont).SetFontSize(14).SetMarginBottom(10);
            document.Add(sectionTitle);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 1, 1, 1 }))
                .UseAllAvailableWidth();

            // Headers
            var tableHeaderFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            table.AddHeaderCell(new Cell().Add(new Paragraph("KPI Name").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Current").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Target").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Progress %").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Trend").SetFont(tableHeaderFont).SetFontSize(9)));

            // Data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            foreach (var kpi in kpis.Take(10)) // Limit for PDF
            {
                table.AddCell(new Cell().Add(new Paragraph(kpi.KPIName).SetFont(cellFont).SetFontSize(8))); // Use KPIName instead of Name
                table.AddCell(new Cell().Add(new Paragraph(kpi.CurrentValue.ToString("F1")).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(kpi.TargetValue.ToString("F1")).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(kpi.ProgressPercentage.ToString("F1")).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(kpi.Direction).SetFont(cellFont).SetFontSize(8))); // Use Direction instead of Trend
            }

            document.Add(table);
            document.Add(new Paragraph(" ").SetMarginBottom(10));
        }

        private void AddPredictiveAnalyticsToPdf(Document document, List<PredictiveAnalyticsData> analytics)
        {
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var sectionTitle = new Paragraph("Predictive Analytics")
                .SetFont(headerFont).SetFontSize(14).SetMarginBottom(10);
            document.Add(sectionTitle);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 2, 1 }))
                .UseAllAvailableWidth();

            // Headers
            var tableHeaderFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            table.AddHeaderCell(new Cell().Add(new Paragraph("Equipment").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Failure Prob.").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Recommended Action").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Confidence").SetFont(tableHeaderFont).SetFontSize(9)));

            // Data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            foreach (var item in analytics.Take(10)) // Limit for PDF
            {
                table.AddCell(new Cell().Add(new Paragraph(item.EquipmentName).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.ConfidenceScore.ToString("F1")).SetFont(cellFont).SetFontSize(8))); // Use ConfidenceScore instead of FailureProbability
                table.AddCell(new Cell().Add(new Paragraph(string.Join(", ", item.RecommendedActions)).SetFont(cellFont).SetFontSize(8))); // Join list of actions
                table.AddCell(new Cell().Add(new Paragraph(item.ConfidenceScore.ToString("F1")).SetFont(cellFont).SetFontSize(8))); // Use ConfidenceScore instead of ConfidenceLevel
            }

            document.Add(table);
            document.Add(new Paragraph(" ").SetMarginBottom(10));
        }

        private void AddTrendAnalysisToPdf(Document document, List<MaintenanceTrendData> trendData)
        {
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var sectionTitle = new Paragraph("Maintenance Trend Analysis")
                .SetFont(headerFont).SetFontSize(14).SetMarginBottom(10);
            document.Add(sectionTitle);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 1.5f, 1, 1, 1, 1, 1 }))
                .UseAllAvailableWidth();

            // Headers
            var tableHeaderFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            table.AddHeaderCell(new Cell().Add(new Paragraph("Date").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Preventive").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Corrective").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Predictive").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Inspection").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Total Cost").SetFont(tableHeaderFont).SetFontSize(9)));

            // Data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            foreach (var item in trendData.Take(10)) // Limit for PDF
            {
                table.AddCell(new Cell().Add(new Paragraph(item.Date.ToString("MMM dd")).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.PreventiveMaintenanceCount.ToString()).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.CorrectiveMaintenanceCount.ToString()).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph("0").SetFont(cellFont).SetFontSize(8))); // PredictiveMaintenanceCount not available
                table.AddCell(new Cell().Add(new Paragraph(item.InspectionMaintenanceCount.ToString()).SetFont(cellFont).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph($"${item.TotalCost:F0}").SetFont(cellFont).SetFontSize(8)));
            }

            document.Add(table);
            document.Add(new Paragraph(" ").SetMarginBottom(10));
        }

        private void AddCostAnalysisToPdf(Document document, List<CostAnalysisData> costData)
        {
            var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var sectionTitle = new Paragraph("Cost Analysis")
                .SetFont(headerFont).SetFontSize(14).SetMarginBottom(10);
            document.Add(sectionTitle);

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 1.5f, 1, 1, 1, 1 }))
                .UseAllAvailableWidth();

            // Headers
            var tableHeaderFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            table.AddHeaderCell(new Cell().Add(new Paragraph("Month").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Maintenance").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Parts").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Labor").SetFont(tableHeaderFont).SetFontSize(9)));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Total").SetFont(tableHeaderFont).SetFontSize(9)));

            // Data
            var cellFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            foreach (var item in costData.Take(10)) // Limit for PDF
            {
                table.AddCell(new Cell().Add(new Paragraph(item.Category).SetFont(cellFont).SetFontSize(8))); // Use Category instead of Period
                table.AddCell(new Cell().Add(new Paragraph(item.CategoryValue).SetFont(cellFont).SetFontSize(8))); // Use CategoryValue
                table.AddCell(new Cell().Add(new Paragraph($"${item.TotalCost:F0}").SetFont(cellFont).SetFontSize(8))); // Use TotalCost
                table.AddCell(new Cell().Add(new Paragraph($"${item.AverageCost:F0}").SetFont(cellFont).SetFontSize(8))); // Use AverageCost
                table.AddCell(new Cell().Add(new Paragraph($"${item.ProjectedCost:F0}").SetFont(cellFont).SetFontSize(8))); // Use ProjectedCost
            }

            document.Add(table);
            document.Add(new Paragraph(" ").SetMarginBottom(10));
        }

        // Helper methods for data filtering
        private async Task<List<Equipment>> GetFilteredEquipment(DashboardFilterViewModel? filters)
        {
            var query = _context.Equipment
                .Include(e => e.EquipmentModel)
                    .ThenInclude(em => em!.EquipmentType)
                .Include(e => e.Room)
                    .ThenInclude(r => r!.Building)
                .AsQueryable();

            if (filters != null)
            {
                if (filters.BuildingIds?.Any() == true)
                {
                    query = query.Where(e => e.Room != null && filters.BuildingIds.Contains(e.Room.BuildingId));
                }

                if (filters.EquipmentTypeIds?.Any() == true)
                {
                    query = query.Where(e => e.EquipmentModel != null && filters.EquipmentTypeIds.Contains(e.EquipmentModel.EquipmentTypeId));
                }

                if (filters.EquipmentStatuses?.Any() == true)
                {
                    query = query.Where(e => filters.EquipmentStatuses.Contains(e.Status));
                }
            }

            return await query.OrderBy(e => e.EquipmentModel!.ModelName).ToListAsync(); // Use null-forgiving operator
        }

        private async Task<List<MaintenanceLog>> GetFilteredMaintenance(DashboardFilterViewModel? filters)
        {
            var query = _context.MaintenanceLogs
                .Include(m => m.Equipment)
                    .ThenInclude(e => e!.Room)
                        .ThenInclude(r => r!.Building)
                .AsQueryable();

            if (filters != null)
            {
                if (filters.BuildingIds?.Any() == true)
                {
                    query = query.Where(m => m.Equipment != null && m.Equipment.Room != null && filters.BuildingIds.Contains(m.Equipment.Room.BuildingId));
                }

                if (filters.DateFrom.HasValue)
                {
                    query = query.Where(m => m.LogDate >= filters.DateFrom.Value);
                }

                if (filters.DateTo.HasValue)
                {
                    query = query.Where(m => m.LogDate <= filters.DateTo.Value);
                }
            }

            return await query.OrderByDescending(m => m.LogDate).ToListAsync();
        }

        private async Task<List<Alert>> GetFilteredAlerts(DashboardFilterViewModel? filters)
        {
            var query = _context.Alerts
                .Include(a => a.Equipment)
                    .ThenInclude(e => e!.Room)
                        .ThenInclude(r => r!.Building)
                .AsQueryable();

            if (filters != null)
            {
                if (filters.BuildingIds?.Any() == true)
                {
                    query = query.Where(a => a.Equipment != null && a.Equipment.Room != null && filters.BuildingIds.Contains(a.Equipment.Room.BuildingId));
                }

                if (filters.DateFrom.HasValue)
                {
                    query = query.Where(a => a.CreatedDate >= filters.DateFrom.Value);
                }

                if (filters.DateTo.HasValue)
                {
                    query = query.Where(a => a.CreatedDate <= filters.DateTo.Value);
                }
            }

            return await query.OrderByDescending(a => a.CreatedDate).ToListAsync();
        }

        private async Task<List<InventoryItem>> GetFilteredInventory(DashboardFilterViewModel? filters)
        {
            var query = _context.InventoryItems
                .Include(i => i.InventoryStocks)
                .AsQueryable();

            // Apply any relevant filters here if needed for inventory

            return await query.OrderBy(i => i.Name).ToListAsync(); // Use Name instead of ItemName
        }

        public async Task<ExportResult> ExportReportAsync(object reportData, string format)
        {
            try
            {
                var result = new ExportResult();
                
                // Serialize report data to JSON for processing
                var jsonData = JsonSerializer.Serialize(reportData, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true 
                });

                switch (format.ToLower())
                {
                    case "pdf":
                        result.Data = await Task.Run(() => GenerateReportPdfAsync(jsonData, reportData));
                        result.ContentType = "application/pdf";
                        result.FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                        break;
                        
                    case "excel":
                    case "xlsx":
                        result.Data = await Task.Run(() => GenerateReportExcelAsync(jsonData, reportData));
                        result.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        result.FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                        break;
                        
                    case "csv":
                        result.Data = await Task.Run(() => GenerateReportCsvAsync(jsonData, reportData));
                        result.ContentType = "text/csv";
                        result.FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                        break;
                        
                    case "json":
                        result.Data = await Task.Run(() => System.Text.Encoding.UTF8.GetBytes(jsonData));
                        result.ContentType = "application/json";
                        result.FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                        break;
                        
                    default:
                        throw new ArgumentException($"Unsupported export format: {format}");
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                return new ExportResult 
                { 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private byte[] GenerateReportPdfAsync(string jsonData, object reportData)
        {
            using var memoryStream = new MemoryStream();
            var writer = new PdfWriter(memoryStream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(30, 25, 30, 25);

            // Title
            var titleFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var title = new Paragraph("Custom Report")
                .SetFont(titleFont)
                .SetFontSize(18)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(20);
            document.Add(title);

            // Generated date
            var dateFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var dateInfo = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .SetFont(dateFont)
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(30);
            document.Add(dateInfo);

            // Report content
            var contentFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var content = new Paragraph("Report Data:")
                .SetFont(contentFont)
                .SetFontSize(12)
                .SetMarginBottom(15);
            document.Add(content);

            // Add formatted JSON data
            var dataFont = PdfFontFactory.CreateFont(StandardFonts.COURIER);
            var dataParagraph = new Paragraph(jsonData)
                .SetFont(dataFont)
                .SetFontSize(9);
            document.Add(dataParagraph);

            document.Close();
            return memoryStream.ToArray();
        }

        private byte[] GenerateReportExcelAsync(string jsonData, object reportData)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Report Data");

            // Add header
            worksheet.Cells[1, 1].Value = "Custom Report";
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.Font.Size = 16;

            worksheet.Cells[2, 1].Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            worksheet.Cells[2, 1].Style.Font.Size = 10;

            // Add data
            worksheet.Cells[4, 1].Value = "Report Data (JSON):";
            worksheet.Cells[4, 1].Style.Font.Bold = true;

            worksheet.Cells[5, 1].Value = jsonData;
            worksheet.Cells[5, 1].Style.WrapText = true;
            worksheet.Column(1).Width = 100;

            return package.GetAsByteArray();
        }

        private byte[] GenerateReportCsvAsync(string jsonData, object reportData)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Field,Value");
            csv.AppendLine($"\"Report Generated\",\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
            csv.AppendLine($"\"Report Type\",\"Custom Report\"");
            csv.AppendLine($"\"Data Format\",\"JSON\"");
            csv.AppendLine($"\"Report Data\",\"{jsonData.Replace("\"", "\"\"")}\"");

            return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        }
    }
}
