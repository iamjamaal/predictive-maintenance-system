using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FEENALOoFINALE.Services
{
    public interface IPdfTimetableExtractionService
    {
        Task<TimetableExtractionResult> ExtractTimetableDataAsync(string pdfFilePath);
        Task<bool> ValidateTimetableFormatAsync(string pdfFilePath);
        Task<List<TimetableFormatOption>> DetectPossibleFormatsAsync(string pdfFilePath);
    }

    public class PdfTimetableExtractionService : IPdfTimetableExtractionService
    {
        private readonly ILogger<PdfTimetableExtractionService> _logger;

        public PdfTimetableExtractionService(ILogger<PdfTimetableExtractionService> logger)
        {
            _logger = logger;
        }

        public async Task<TimetableExtractionResult> ExtractTimetableDataAsync(string pdfFilePath)
        {
            var result = new TimetableExtractionResult();
            
            try
            {
                using var document = PdfDocument.Open(pdfFilePath);
                
                // Analyze document structure
                var documentAnalysis = AnalyzeDocumentStructure(document);
                result.DetectedFormat = documentAnalysis.DetectedFormat;
                result.FormatConfidence = documentAnalysis.Confidence;

                // Apply appropriate extraction strategy based on detected format
                switch (documentAnalysis.DetectedFormat)
                {
                    case TimetableFormat.GridBased:
                        result = await ExtractGridBasedTimetableAsync(document, result);
                        break;
                    case TimetableFormat.ListBased:
                        result = await ExtractListBasedTimetableAsync(document, result);
                        break;
                    case TimetableFormat.MultiColumn:
                        result = await ExtractMultiColumnTimetableAsync(document, result);
                        break;
                    case TimetableFormat.MultiYear:
                        result = await ExtractMultiYearTimetableAsync(document, result);
                        break;
                    case TimetableFormat.DayByDay:
                        result = await ExtractDayByDayTimetableAsync(document, result);
                        break;
                    default:
                        result = await ExtractGenericTimetableAsync(document, result);
                        break;
                }

                result.Success = result.ExtractedSchedules.Any() || result.ExtractedRooms.Any();
                
                if (!result.Success)
                {
                    result.ErrorMessage = "No timetable data could be extracted from the PDF";
                    
                    // Fallback: Try all extraction methods
                    _logger.LogWarning("Primary extraction failed, trying fallback methods");
                    result = await TryFallbackExtractionMethods(document, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting timetable data from PDF: {FilePath}", pdfFilePath);
                result.Success = false;
                result.ErrorMessage = $"PDF extraction error: {ex.Message}";
            }

            return result;
        }

        public async Task<bool> ValidateTimetableFormatAsync(string pdfFilePath)
        {
            try
            {
                using var document = PdfDocument.Open(pdfFilePath);
                var analysis = AnalyzeDocumentStructure(document);
                return analysis.Confidence > 0.6; // 60% confidence threshold
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<TimetableFormatOption>> DetectPossibleFormatsAsync(string pdfFilePath)
        {
            var options = new List<TimetableFormatOption>();
            
            try
            {
                using var document = PdfDocument.Open(pdfFilePath);
                var text = ExtractAllText(document);
                
                // Check for different format indicators
                options.Add(new TimetableFormatOption
                {
                    Format = TimetableFormat.GridBased,
                    Confidence = CalculateGridConfidence(text),
                    Description = "Table/Grid format with time slots and rooms"
                });

                options.Add(new TimetableFormatOption
                {
                    Format = TimetableFormat.ListBased,
                    Confidence = CalculateListConfidence(text),
                    Description = "List format with schedule entries"
                });

                options.Add(new TimetableFormatOption
                {
                    Format = TimetableFormat.MultiYear,
                    Confidence = CalculateMultiYearConfidence(text),
                    Description = "Multiple year levels in separate sections"
                });

                options.Add(new TimetableFormatOption
                {
                    Format = TimetableFormat.DayByDay,
                    Confidence = CalculateDayByDayConfidence(text),
                    Description = "Day-by-day schedule format"
                });

                return options.OrderByDescending(o => o.Confidence).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting timetable formats");
                return options;
            }
        }

        private DocumentAnalysis AnalyzeDocumentStructure(PdfDocument document)
        {
            var analysis = new DocumentAnalysis();
            var allText = ExtractAllText(document);
            
            // Analyze text patterns to determine format
            var gridScore = CalculateGridConfidence(allText);
            var listScore = CalculateListConfidence(allText);
            var multiYearScore = CalculateMultiYearConfidence(allText);
            var dayByDayScore = CalculateDayByDayConfidence(allText);
            var multiColumnScore = CalculateMultiColumnConfidence(allText);

            var scores = new Dictionary<TimetableFormat, double>
            {
                { TimetableFormat.GridBased, gridScore },
                { TimetableFormat.ListBased, listScore },
                { TimetableFormat.MultiYear, multiYearScore },
                { TimetableFormat.DayByDay, dayByDayScore },
                { TimetableFormat.MultiColumn, multiColumnScore }
            };

            var bestMatch = scores.OrderByDescending(s => s.Value).First();
            analysis.DetectedFormat = bestMatch.Key;
            analysis.Confidence = bestMatch.Value;

            return analysis;
        }

        private double CalculateGridConfidence(string text)
        {
            double score = 0;
            
            // Look for time patterns (8:00, 9:30, etc.)
            var timePatterns = Regex.Matches(text, @"\b([0-9]{1,2}):([0-9]{2})\b").Count;
            score += Math.Min(timePatterns * 0.1, 0.3);

            // Look for room patterns (Room 101, Lab A, etc.)
            var roomPatterns = Regex.Matches(text, @"\b(Room|Lab|Hall|Lecture|Theater)\s*[A-Z0-9]+\b", RegexOptions.IgnoreCase).Count;
            score += Math.Min(roomPatterns * 0.05, 0.2);

            // Look for day names
            var dayPatterns = Regex.Matches(text, @"\b(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|Mon|Tue|Wed|Thu|Fri|Sat|Sun)\b", RegexOptions.IgnoreCase).Count;
            score += Math.Min(dayPatterns * 0.1, 0.3);

            // Look for grid-like structures
            var lines = text.Split('\n');
            var tabSeparatedLines = lines.Count(line => line.Split('\t').Length > 3);
            score += Math.Min(tabSeparatedLines * 0.01, 0.2);

            return Math.Min(score, 1.0);
        }

        private double CalculateListConfidence(string text)
        {
            double score = 0;
            
            // Look for course codes (CS101, ENG201, etc.)
            var coursePatterns = Regex.Matches(text, @"\b[A-Z]{2,4}[0-9]{3,4}\b").Count;
            score += Math.Min(coursePatterns * 0.05, 0.4);

            // Look for bullet points or numbering
            var bulletPatterns = Regex.Matches(text, @"^\s*[\-\*\•]\s*", RegexOptions.Multiline).Count;
            score += Math.Min(bulletPatterns * 0.02, 0.2);

            // Look for structured course entries
            var lines = text.Split('\n');
            var structuredLines = lines.Count(line => 
                Regex.IsMatch(line, @"\b[0-9]{1,2}:[0-9]{2}\b") && 
                line.Length > 10 && 
                line.Length < 200);
            score += Math.Min(structuredLines * 0.03, 0.4);

            return Math.Min(score, 1.0);
        }

        private double CalculateMultiYearConfidence(string text)
        {
            double score = 0;
            
            // Look for year level indicators
            var yearPatterns = Regex.Matches(text, @"\b(Year\s*[1-4]|Level\s*[1-4]|[1-4](st|nd|rd|th)\s*Year)\b", RegexOptions.IgnoreCase).Count;
            score += Math.Min(yearPatterns * 0.2, 0.6);

            // Look for section headers
            var sectionPatterns = Regex.Matches(text, @"^[A-Z\s]+[1-4]", RegexOptions.Multiline).Count;
            score += Math.Min(sectionPatterns * 0.1, 0.4);

            return Math.Min(score, 1.0);
        }

        private double CalculateDayByDayConfidence(string text)
        {
            double score = 0;
            
            // Look for multiple day headers
            var dayHeaders = Regex.Matches(text, @"^(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)", RegexOptions.Multiline | RegexOptions.IgnoreCase).Count;
            score += Math.Min(dayHeaders * 0.15, 0.6);

            // Look for date patterns
            var datePatterns = Regex.Matches(text, @"\b\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}\b").Count;
            score += Math.Min(datePatterns * 0.1, 0.4);

            return Math.Min(score, 1.0);
        }

        private double CalculateMultiColumnConfidence(string text)
        {
            double score = 0;
            
            var lines = text.Split('\n');
            var multiColumnLines = lines.Count(line => line.Split(new char[] { '\t', '|' }).Length > 4);
            score += Math.Min(multiColumnLines * 0.02, 0.5);

            // Look for column headers
            var columnHeaders = Regex.Matches(text, @"\b(Time|Room|Subject|Course|Instructor|Day)\b", RegexOptions.IgnoreCase).Count;
            score += Math.Min(columnHeaders * 0.1, 0.5);

            return Math.Min(score, 1.0);
        }

        private async Task<TimetableExtractionResult> ExtractGridBasedTimetableAsync(PdfDocument document, TimetableExtractionResult result)
        {
            // Implementation for grid-based extraction
            var text = ExtractAllText(document);
            var lines = text.Split('\n');
            
            foreach (var line in lines)
            {
                var timeMatch = Regex.Match(line, @"\b([0-9]{1,2}):([0-9]{2})\b");
                var roomMatch = Regex.Match(line, @"\b(Room|Lab|Hall)\s*([A-Z0-9]+)\b", RegexOptions.IgnoreCase);
                
                if (timeMatch.Success && roomMatch.Success)
                {
                    var roomName = $"{roomMatch.Groups[1].Value} {roomMatch.Groups[2].Value}";
                    if (!result.ExtractedRooms.Contains(roomName))
                    {
                        result.ExtractedRooms.Add(roomName);
                    }
                    
                    result.ExtractedSchedules.Add(new ExtractedScheduleEntry
                    {
                        Time = timeMatch.Value,
                        Room = roomName,
                        RawText = line.Trim()
                    });
                }
            }
            
            return result;
        }

        private async Task<TimetableExtractionResult> ExtractListBasedTimetableAsync(PdfDocument document, TimetableExtractionResult result)
        {
            // Implementation for list-based extraction
            var text = ExtractAllText(document);
            var lines = text.Split('\n');
            
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                // Look for course patterns with time and room
                var pattern = @"([A-Z]{2,4}[0-9]{3,4}).*?(\d{1,2}:\d{2}).*?(Room|Lab|Hall)\s*([A-Z0-9]+)";
                var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                
                if (match.Success)
                {
                    var roomName = $"{match.Groups[3].Value} {match.Groups[4].Value}";
                    if (!result.ExtractedRooms.Contains(roomName))
                    {
                        result.ExtractedRooms.Add(roomName);
                    }
                    
                    result.ExtractedSchedules.Add(new ExtractedScheduleEntry
                    {
                        CourseCode = match.Groups[1].Value,
                        Time = match.Groups[2].Value,
                        Room = roomName,
                        RawText = line.Trim()
                    });
                }
            }
            
            return result;
        }

        private async Task<TimetableExtractionResult> ExtractMultiColumnTimetableAsync(PdfDocument document, TimetableExtractionResult result)
        {
            // Implementation for multi-column extraction
            var text = ExtractAllText(document);
            var lines = text.Split('\n');
            
            foreach (var line in lines)
            {
                var columns = line.Split('\t');
                if (columns.Length >= 3)
                {
                    // Assume format: Time | Subject | Room
                    var timePattern = @"\b\d{1,2}:\d{2}\b";
                    var roomPattern = @"\b(Room|Lab|Hall)\s*[A-Z0-9]+\b";
                    
                    var timeColumn = columns.FirstOrDefault(c => Regex.IsMatch(c, timePattern));
                    var roomColumn = columns.FirstOrDefault(c => Regex.IsMatch(c, roomPattern, RegexOptions.IgnoreCase));
                    
                    if (timeColumn != null && roomColumn != null)
                    {
                        var roomMatch = Regex.Match(roomColumn, roomPattern, RegexOptions.IgnoreCase);
                        if (roomMatch.Success)
                        {
                            var roomName = roomMatch.Value;
                            if (!result.ExtractedRooms.Contains(roomName))
                            {
                                result.ExtractedRooms.Add(roomName);
                            }
                            
                            result.ExtractedSchedules.Add(new ExtractedScheduleEntry
                            {
                                Time = timeColumn.Trim(),
                                Room = roomName,
                                RawText = line.Trim()
                            });
                        }
                    }
                }
            }
            
            return result;
        }

        private async Task<TimetableExtractionResult> ExtractMultiYearTimetableAsync(PdfDocument document, TimetableExtractionResult result)
        {
            // Implementation for multi-year format
            var text = ExtractAllText(document);
            var lines = text.Split('\n');
            
            string currentYear = "";
            
            foreach (var line in lines)
            {
                // Check for year headers
                var yearMatch = Regex.Match(line, @"\b(Year\s*[1-4]|Level\s*[1-4]|[1-4](st|nd|rd|th)\s*Year)\b", RegexOptions.IgnoreCase);
                if (yearMatch.Success)
                {
                    currentYear = yearMatch.Value;
                    continue;
                }
                
                // Extract schedule entries within year sections
                var timeMatch = Regex.Match(line, @"\b([0-9]{1,2}):([0-9]{2})\b");
                var roomMatch = Regex.Match(line, @"\b(Room|Lab|Hall)\s*([A-Z0-9]+)\b", RegexOptions.IgnoreCase);
                
                if (timeMatch.Success && roomMatch.Success)
                {
                    var roomName = $"{roomMatch.Groups[1].Value} {roomMatch.Groups[2].Value}";
                    if (!result.ExtractedRooms.Contains(roomName))
                    {
                        result.ExtractedRooms.Add(roomName);
                    }
                    
                    result.ExtractedSchedules.Add(new ExtractedScheduleEntry
                    {
                        Time = timeMatch.Value,
                        Room = roomName,
                        YearLevel = currentYear,
                        RawText = line.Trim()
                    });
                }
            }
            
            return result;
        }

        private async Task<TimetableExtractionResult> ExtractDayByDayTimetableAsync(PdfDocument document, TimetableExtractionResult result)
        {
            // Implementation for day-by-day format
            var text = ExtractAllText(document);
            var lines = text.Split('\n');
            
            string currentDay = "";
            
            foreach (var line in lines)
            {
                // Check for day headers
                var dayMatch = Regex.Match(line, @"^(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)", RegexOptions.IgnoreCase);
                if (dayMatch.Success)
                {
                    currentDay = dayMatch.Value;
                    continue;
                }
                
                // Extract schedule entries within day sections
                var timeMatch = Regex.Match(line, @"\b([0-9]{1,2}):([0-9]{2})\b");
                var roomMatch = Regex.Match(line, @"\b(Room|Lab|Hall)\s*([A-Z0-9]+)\b", RegexOptions.IgnoreCase);
                
                if (timeMatch.Success && roomMatch.Success)
                {
                    var roomName = $"{roomMatch.Groups[1].Value} {roomMatch.Groups[2].Value}";
                    if (!result.ExtractedRooms.Contains(roomName))
                    {
                        result.ExtractedRooms.Add(roomName);
                    }
                    
                    result.ExtractedSchedules.Add(new ExtractedScheduleEntry
                    {
                        Time = timeMatch.Value,
                        Room = roomName,
                        DayOfWeek = currentDay,
                        RawText = line.Trim()
                    });
                }
            }
            
            return result;
        }

        private async Task<TimetableExtractionResult> ExtractGenericTimetableAsync(PdfDocument document, TimetableExtractionResult result)
        {
            // Generic fallback extraction
            var text = ExtractAllText(document);
            
            // Try to find any room references
            var roomMatches = Regex.Matches(text, @"\b(Room|Lab|Hall|Lecture|Theater)\s*([A-Z0-9]+)\b", RegexOptions.IgnoreCase);
            foreach (Match match in roomMatches)
            {
                var roomName = $"{match.Groups[1].Value} {match.Groups[2].Value}";
                if (!result.ExtractedRooms.Contains(roomName))
                {
                    result.ExtractedRooms.Add(roomName);
                }
            }
            
            // Try to find time patterns
            var timeMatches = Regex.Matches(text, @"\b([0-9]{1,2}):([0-9]{2})\b");
            foreach (Match timeMatch in timeMatches)
            {
                result.ExtractedSchedules.Add(new ExtractedScheduleEntry
                {
                    Time = timeMatch.Value,
                    RawText = $"Time slot found: {timeMatch.Value}"
                });
            }
            
            return result;
        }

        private async Task<TimetableExtractionResult> TryFallbackExtractionMethods(PdfDocument document, TimetableExtractionResult result)
        {
            // Try all extraction methods as fallback
            var methods = new[]
            {
                () => ExtractGridBasedTimetableAsync(document, new TimetableExtractionResult()),
                () => ExtractListBasedTimetableAsync(document, new TimetableExtractionResult()),
                () => ExtractMultiColumnTimetableAsync(document, new TimetableExtractionResult()),
                () => ExtractMultiYearTimetableAsync(document, new TimetableExtractionResult()),
                () => ExtractDayByDayTimetableAsync(document, new TimetableExtractionResult()),
                () => ExtractGenericTimetableAsync(document, new TimetableExtractionResult())
            };

            foreach (var method in methods)
            {
                try
                {
                    var methodResult = await method();
                    if (methodResult.ExtractedRooms.Any() || methodResult.ExtractedSchedules.Any())
                    {
                        // Merge results
                        result.ExtractedRooms.AddRange(methodResult.ExtractedRooms.Except(result.ExtractedRooms));
                        result.ExtractedSchedules.AddRange(methodResult.ExtractedSchedules);
                        result.Success = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fallback extraction method failed");
                }
            }

            return result;
        }

        private string ExtractAllText(PdfDocument document)
        {
            var text = "";
            for (int i = 0; i < document.NumberOfPages; i++)
            {
                var page = document.GetPage(i + 1);
                text += page.Text + "\n";
            }
            return text;
        }
    }

    // Supporting classes
    public class TimetableExtractionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimetableFormat DetectedFormat { get; set; }
        public double FormatConfidence { get; set; }
        public List<string> ExtractedRooms { get; set; } = new List<string>();
        public List<ExtractedScheduleEntry> ExtractedSchedules { get; set; } = new List<ExtractedScheduleEntry>();
        public List<string> WarningMessages { get; set; } = new List<string>();
        public Dictionary<string, object> ExtractedMetadata { get; set; } = new Dictionary<string, object>();
    }

    public class ExtractedScheduleEntry
    {
        public string? CourseCode { get; set; }
        public string? Time { get; set; }
        public string? Room { get; set; }
        public string? DayOfWeek { get; set; }
        public string? YearLevel { get; set; }
        public string? Instructor { get; set; }
        public string RawText { get; set; } = string.Empty;
    }

    public class TimetableFormatOption
    {
        public TimetableFormat Format { get; set; }
        public double Confidence { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class DocumentAnalysis
    {
        public TimetableFormat DetectedFormat { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public enum TimetableFormat
    {
        Unknown,
        GridBased,
        ListBased,
        MultiColumn,
        MultiYear,
        DayByDay
    }
}
