using FEENALOoFINALE.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Text.RegularExpressions;
using System.Text;
using FEENALOoFINALE.Data;
using Microsoft.EntityFrameworkCore;

namespace FEENALOoFINALE.Services
{
    public interface IDocumentProcessingService
    {
        Task<string> ExtractTextFromDocumentAsync(string filePath, string contentType);
        Task<List<MaintenanceRecommendation>> ExtractMaintenanceRecommendationsAsync(string text, int equipmentModelId, int? documentId = null);
        Task<bool> SaveDocumentAsync(IFormFile file, int equipmentId, string documentType);
        Task ProcessDocumentAsync(int documentId);
    }

    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DocumentProcessingService> _logger;
        private readonly IWebHostEnvironment _environment;

        // Common maintenance-related keywords and patterns
        private readonly Dictionary<string, string[]> _maintenancePatterns = new()
        {
            ["preventive"] = new[] { "preventive", "routine", "scheduled", "regular", "periodic", "maintenance schedule" },
            ["inspection"] = new[] { "inspect", "check", "examine", "visual inspection", "monthly check", "weekly check" },
            ["cleaning"] = new[] { "clean", "wipe", "dust", "remove debris", "cleaning procedure" },
            ["lubrication"] = new[] { "lubricate", "oil", "grease", "lubrication point", "apply lubricant" },
            ["replacement"] = new[] { "replace", "change", "substitute", "renewal", "replacement interval" },
            ["calibration"] = new[] { "calibrate", "adjust", "calibration", "adjustment", "tune" },
            ["safety"] = new[] { "safety", "warning", "caution", "danger", "hazard", "safety procedure" }
        };

        private readonly Regex _intervalPattern = new Regex(
            @"(?:every|each)\s+(\d+)\s+(day|week|month|year)s?|(\d+)\s+(day|week|month|year)s?\s+interval|(?:monthly|weekly|daily|annually|quarterly)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public DocumentProcessingService(
            ApplicationDbContext context,
            ILogger<DocumentProcessingService> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        public async Task<string> ExtractTextFromDocumentAsync(string filePath, string contentType)
        {
            try
            {
                return contentType.ToLower() switch
                {
                    "application/pdf" => await ExtractTextFromPdfAsync(filePath),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ExtractTextFromDocx(filePath),
                    "text/plain" => await File.ReadAllTextAsync(filePath),
                    _ => throw new NotSupportedException($"Document type {contentType} is not supported for text extraction.")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from document: {FilePath}", filePath);
                throw;
            }
        }

        private async Task<string> ExtractTextFromPdfAsync(string filePath)
        {
            var text = new StringBuilder();
            
            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    var pageText = page.Text;
                    text.AppendLine(pageText);
                }
            }
            
            return text.ToString();
        }

        private string ExtractTextFromDocx(string filePath)
        {
            var text = new StringBuilder();
            
            using (var document = WordprocessingDocument.Open(filePath, false))
            {
                var body = document.MainDocumentPart?.Document?.Body;
                if (body != null)
                {
                    foreach (var paragraph in body.Elements<Paragraph>())
                    {
                        text.AppendLine(paragraph.InnerText);
                    }
                }
            }
            
            return text.ToString();
        }

        public async Task<List<MaintenanceRecommendation>> ExtractMaintenanceRecommendationsAsync(string text, int equipmentModelId, int? documentId = null)
        {
            var recommendations = new List<MaintenanceRecommendation>();
            
            try
            {
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var cleanLine = line.Trim();
                    if (string.IsNullOrEmpty(cleanLine) || cleanLine.Length < 10) continue;

                    // Check if line contains maintenance-related content
                    var maintenanceType = GetMaintenanceType(cleanLine);
                    if (maintenanceType == null) continue;

                    // Extract interval if present
                    var interval = ExtractInterval(cleanLine);
                    
                    // Determine priority based on keywords
                    var priority = DeterminePriority(cleanLine);
                    
                    // Calculate confidence score
                    var confidence = CalculateConfidenceScore(cleanLine, maintenanceType);

                    var recommendation = new MaintenanceRecommendation
                    {
                        DocumentId = documentId,
                        EquipmentModelId = equipmentModelId, // Link to equipment model instead
                        RecommendationType = maintenanceType,
                        RecommendationText = cleanLine,
                        IntervalDays = interval,
                        Priority = priority,
                        Source = $"Line extracted from document",
                        ConfidenceScore = confidence,
                        CreatedDate = DateTime.UtcNow,
                        IsActive = confidence >= 0.6m // Only activate high-confidence recommendations
                    };

                    recommendations.Add(recommendation);
                }

                // Remove duplicates and low-quality recommendations
                recommendations = FilterAndDeduplicateRecommendations(recommendations);
                
                _logger.LogInformation("Extracted {Count} maintenance recommendations from document", recommendations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting maintenance recommendations from text");
                throw;
            }

            return recommendations;
        }

        private string? GetMaintenanceType(string text)
        {
            var lowerText = text.ToLower();
            
            foreach (var category in _maintenancePatterns)
            {
                if (category.Value.Any(keyword => lowerText.Contains(keyword)))
                {
                    return char.ToUpper(category.Key[0]) + category.Key[1..];
                }
            }
            
            return null;
        }

        private int? ExtractInterval(string text)
        {
            var match = _intervalPattern.Match(text);
            if (!match.Success) return null;

            int number = 0;
            string period = "";

            if (match.Groups[1].Success)
            {
                number = int.Parse(match.Groups[1].Value);
                period = match.Groups[2].Value.ToLower();
            }
            else if (match.Groups[3].Success)
            {
                number = int.Parse(match.Groups[3].Value);
                period = match.Groups[4].Value.ToLower();
            }
            else
            {
                // Handle named periods
                var lowerText = text.ToLower();
                if (lowerText.Contains("daily")) return 1;
                if (lowerText.Contains("weekly")) return 7;
                if (lowerText.Contains("monthly")) return 30;
                if (lowerText.Contains("quarterly")) return 90;
                if (lowerText.Contains("annually")) return 365;
            }

            return period switch
            {
                "day" or "days" => number,
                "week" or "weeks" => number * 7,
                "month" or "months" => number * 30,
                "year" or "years" => number * 365,
                _ => null
            };
        }

        private string DeterminePriority(string text)
        {
            var lowerText = text.ToLower();
            
            if (lowerText.Contains("critical") || lowerText.Contains("urgent") || lowerText.Contains("immediately"))
                return "Critical";
            if (lowerText.Contains("important") || lowerText.Contains("essential") || lowerText.Contains("required"))
                return "High";
            if (lowerText.Contains("recommended") || lowerText.Contains("should"))
                return "Medium";
                
            return "Low";
        }

        private decimal CalculateConfidenceScore(string text, string maintenanceType)
        {
            decimal score = 0.5m; // Base score
            
            var lowerText = text.ToLower();
            
            // Higher score for specific maintenance actions
            if (_maintenancePatterns[maintenanceType.ToLower()].Any(keyword => lowerText.Contains(keyword)))
                score += 0.2m;
            
            // Higher score for interval mentions
            if (_intervalPattern.IsMatch(text))
                score += 0.2m;
            
            // Higher score for procedural language
            if (lowerText.Contains("procedure") || lowerText.Contains("step") || lowerText.Contains("process"))
                score += 0.1m;
            
            // Lower score for very short or very long text
            if (text.Length < 20) score -= 0.1m;
            if (text.Length > 500) score -= 0.1m;
            
            return Math.Max(0, Math.Min(1, score));
        }

        private List<MaintenanceRecommendation> FilterAndDeduplicateRecommendations(List<MaintenanceRecommendation> recommendations)
        {
            // Remove recommendations with very low confidence
            recommendations = recommendations.Where(r => r.ConfidenceScore >= 0.3m).ToList();
            
            // Group similar recommendations and keep the one with highest confidence
            var grouped = recommendations
                .GroupBy(r => new { Type = r.RecommendationType, Interval = r.IntervalDays })
                .Select(g => g.OrderByDescending(r => r.ConfidenceScore).First())
                .ToList();
            
            return grouped.Take(20).ToList(); // Limit to 20 recommendations per document
        }

        public async Task<bool> SaveDocumentAsync(IFormFile file, int equipmentId, string documentType)
        {
            try
            {
                // Get the equipment and its model ID
                var equipment = await _context.Equipment
                    .Include(e => e.EquipmentModel)
                    .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId);

                if (equipment?.EquipmentModelId == null)
                {
                    _logger.LogError("Equipment {EquipmentId} not found or has no model", equipmentId);
                    return false;
                }

                // Check if this document already exists for this model
                var existingDocument = await _context.ManufacturerDocuments
                    .FirstOrDefaultAsync(d => d.EquipmentModelId == equipment.EquipmentModelId && 
                                             d.FileName == file.FileName);

                if (existingDocument != null)
                {
                    _logger.LogInformation("Document {FileName} already exists for model {ModelId}", 
                        file.FileName, equipment.EquipmentModelId);
                    return true; // Consider this a success since the document is already there
                }

                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "documents");
                Directory.CreateDirectory(uploadsPath);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // Save file to disk
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Save document record to database - linked to equipment model
                var document = new ManufacturerDocument
                {
                    EquipmentModelId = equipment.EquipmentModelId,
                    UploadedByEquipmentId = equipmentId, // Track which equipment was used to upload
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    FilePath = Path.Combine("uploads", "documents", fileName),
                    DocumentType = documentType,
                    UploadDate = DateTime.UtcNow
                };

                _context.ManufacturerDocuments.Add(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Document {FileName} saved for equipment model {ModelId} (uploaded via equipment {EquipmentId})", 
                    file.FileName, equipment.EquipmentModelId, equipmentId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving document for equipment {EquipmentId}", equipmentId);
                return false;
            }
        }

        public async Task ProcessDocumentAsync(int documentId)
        {
            try
            {
                var document = await _context.ManufacturerDocuments
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                {
                    _logger.LogWarning("Document with ID {DocumentId} not found", documentId);
                    return;
                }

                var fullPath = Path.Combine(_environment.WebRootPath, document.FilePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogError("Document file not found: {FilePath}", fullPath);
                    return;
                }

                // Extract text from document
                var extractedText = await ExtractTextFromDocumentAsync(fullPath, document.ContentType);
                
                // Extract maintenance recommendations for the equipment model
                var recommendations = await ExtractMaintenanceRecommendationsAsync(
                    extractedText, document.EquipmentModelId, document.DocumentId);

                // Save recommendations to database
                if (recommendations.Any())
                {
                    _context.MaintenanceRecommendations.AddRange(recommendations);
                }

                // Update document status
                document.IsProcessed = true;
                document.ProcessedDate = DateTime.UtcNow;
                document.ExtractedText = extractedText;
                document.ProcessingNotes = $"Extracted {recommendations.Count} maintenance recommendations";

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully processed document {DocumentId}, extracted {Count} recommendations", 
                    documentId, recommendations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {DocumentId}", documentId);
                
                // Update document with error status
                var document = await _context.ManufacturerDocuments.FindAsync(documentId);
                if (document != null)
                {
                    document.IsProcessed = true;
                    document.ProcessedDate = DateTime.UtcNow;
                    document.ProcessingNotes = $"Processing failed: {ex.Message}";
                    await _context.SaveChangesAsync();
                }
            }
        }
    }
}
