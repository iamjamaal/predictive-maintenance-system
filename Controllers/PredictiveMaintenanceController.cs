using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.ViewModels;
using FEENALOoFINALE.Models.ViewModels;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace FEENALOoFINALE.Controllers
{
    [Authorize]
    public class PredictiveMaintenanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PredictiveMaintenanceController> _logger;

        public PredictiveMaintenanceController(ApplicationDbContext context, ILogger<PredictiveMaintenanceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Main predictive maintenance dashboard
        public async Task<IActionResult> Index()
        {
            var model = await GetPredictiveMaintenanceDataAsync();
            
            // Add current semester information for timetable integration
            var currentSemester = await _context.Semesters
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();
            //   var currentSemester = await _timetableController.GetCurrentSemesterAsync();
            ViewBag.CurrentSemester = currentSemester;
            ViewBag.HasActiveSemester = currentSemester != null;
            
            return View(model);
        }

        // Equipment risk assessment
        public async Task<IActionResult> RiskAssessment()
        {
            var equipmentWithRisk = await CalculateEquipmentRiskScoresAsync();
            return View(equipmentWithRisk);
        }

        // Maintenance planning and scheduling
        public async Task<IActionResult> MaintenancePlanning()
        {
            var planningData = await GetMaintenancePlanningDataAsync();
            return View(planningData);
        }

        // API endpoint for predictive analytics data
        [HttpGet]
        public async Task<IActionResult> GetPredictiveAnalytics()
        {
            try
            {
                var analytics = await GetPredictiveMaintenanceDataAsync();
                return Json(new { success = true, data = analytics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving predictive analytics");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // API endpoint for equipment risk scores
        [HttpGet]
        public async Task<IActionResult> GetEquipmentRiskScores()
        {
            try
            {
                var riskScores = await CalculateEquipmentRiskScoresAsync();
                return Json(new { success = true, data = riskScores });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating risk scores");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // API endpoint for maintenance predictions
        [HttpGet]
        public async Task<IActionResult> GetMaintenancePredictions(int? equipmentId = null)
        {
            try
            {
                var predictions = equipmentId.HasValue 
                    ? await PredictMaintenanceForEquipmentAsync(equipmentId.Value)
                    : await PredictMaintenanceForAllEquipmentAsync();

                return Json(new { success = true, data = predictions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating maintenance predictions");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Generate automatic maintenance scheduling based on predictions
        [HttpPost]
        public async Task<IActionResult> GenerateAutomaticSchedule()
        {
            try
            {
                var tasksCreated = await CreatePredictiveMaintenanceTasksAsync();
                return Json(new 
                { 
                    success = true, 
                    message = $"Generated {tasksCreated} predictive maintenance tasks",
                    tasksCreated = tasksCreated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating automatic schedule");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Redirect to dedicated timetable management system
        public IActionResult TimetableManagement()
        {
            TempData["InfoMessage"] = "Timetable management has been moved to a dedicated system with enhanced features including semester progress tracking, automatic completion detection, and comprehensive analytics.";
            return RedirectToAction("Index", "Timetable");
        }

        // Legacy upload endpoint - redirects to new system
        [HttpPost]
        public IActionResult UploadTimetable(IFormFile timetableFile, int semesterWeeks)
        {
            TempData["InfoMessage"] = "Timetable upload has been moved to the dedicated Timetable Management system. The new system provides enhanced features including real-time progress tracking, automatic semester completion detection, and comprehensive equipment usage analytics.";
            return RedirectToAction("Upload", "Timetable");
        }

        // Get semester-based equipment usage data for predictive analytics
        [HttpGet]
        public async Task<IActionResult> GetSemesterEquipmentUsage(int? semesterId = null)
        {
            try
            {
                var semester = semesterId.HasValue
                    ? await _context.Semesters.FindAsync(semesterId.Value)
                    : await _context.Semesters
                        .Where(s => s.IsActive)
                        .OrderByDescending(s => s.StartDate)
                        .FirstOrDefaultAsync();

                if (semester == null)
                {
                    return Json(new { success = false, error = "No semester found" });
                }

                var equipmentUsage = await _context.SemesterEquipmentUsages
                    .Include(seu => seu.Equipment)
                        .ThenInclude(e => e.EquipmentType)
                    .Include(seu => seu.Equipment)
                        .ThenInclude(e => e.EquipmentModel)
                    .Where(seu => seu.SemesterId == semester.SemesterId)
                    .Select(seu => new
                    {
                        EquipmentId = seu.EquipmentId,
                        EquipmentName = seu.Equipment!.EquipmentModel!.ModelName,
                        EquipmentType = seu.Equipment!.EquipmentType!.EquipmentTypeName,
                        WeeklyHours = seu.WeeklyUsageHours,
                        TotalHours = seu.TotalSemesterHours,
                        RoomName = seu.RoomName,
                        LastUpdated = seu.LastUpdated,
                        // Calculate derived properties
                        IsHighUsage = seu.WeeklyUsageHours > 40, // Consider high usage if > 40 hours/week
                        UtilizationPercentage = Math.Round((seu.WeeklyUsageHours / 168.0) * 100, 1) // 168 hours in a week
                    })
                    .ToListAsync();

                return Json(new { success = true, data = equipmentUsage, semester = new {
                    semester.SemesterId,
                    semester.SemesterName,
                    semester.Status,
                    semester.ProgressPercentage
                }});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving semester equipment usage");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Parses text extracted from a timetable PDF to determine weekly usage hours for equipment.
        /// </summary>
        /// <param name="text">The raw text from the PDF.</param>
        /// <param name="semesterWeeks">The number of weeks in the semester (currently unused, assuming a weekly timetable).</param>
        /// <returns>A dictionary mapping EquipmentId to its average weekly usage hours.</returns>
        private Dictionary<string, double> ParseTimetableText(string text, int semesterWeeks)
        {
            var roomWeeklyUsage = new Dictionary<string, double>();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // This Regex is an example and assumes a specific format like: "PB001 Mon 09:00-11:00".
            // It will likely need to be adjusted based on the actual format of your timetable PDF.
            var regex = new Regex(@"(?<room>\w+\d+)\s+(?<day>\w+)\s+(?<start>\d{2}:\d{2})-(?<end>\d{2}:\d{2})", RegexOptions.IgnoreCase);

            foreach (var line in lines)
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    var roomName = match.Groups["room"].Value.ToUpper();
                    var startTime = TimeSpan.Parse(match.Groups["start"].Value);
                    var endTime = TimeSpan.Parse(match.Groups["end"].Value);
                    var duration = (endTime - startTime).TotalHours;

                    if (duration > 0)
                    {
                        roomWeeklyUsage[roomName] = roomWeeklyUsage.GetValueOrDefault(roomName, 0) + duration;
                    }
                }
            }

            // Map room usage to all equipment within that room.
            var equipmentUsage = new Dictionary<string, double>();
            var allEquipmentInRooms = _context.Equipment
                .Where(e => e.Room != null && roomWeeklyUsage.Keys.Contains(e.Room.RoomName.ToUpper()))
                .Include(e => e.Room)
                .ToList();

            foreach (var equipment in allEquipmentInRooms)
            {
                if (equipment.Room != null && roomWeeklyUsage.TryGetValue(equipment.Room.RoomName.ToUpper(), out var weeklyHours))
                {
                    equipmentUsage[equipment.EquipmentId.ToString()] = weeklyHours;
                }
            }

            return equipmentUsage;
        }

        private async Task<PredictiveMaintenanceViewModel> GetPredictiveMaintenanceDataAsync()
        {
            var totalEquipment = await _context.Equipment.CountAsync(e => e.Status == EquipmentStatus.Active);
            
            var highRiskEquipment = await CalculateHighRiskEquipmentCountAsync();
            var predictedFailures = await GetPredictedFailuresNext30DaysAsync();
            var maintenanceRecommendations = await GenerateMaintenanceRecommendationsAsync();
            var costSavingsProjection = await CalculatePredictiveCostSavingsAsync();

            return new PredictiveMaintenanceViewModel
            {
                TotalActiveEquipment = totalEquipment,
                HighRiskEquipmentCount = highRiskEquipment,
                PredictedFailuresNext30Days = predictedFailures.Count,
                MaintenanceRecommendations = maintenanceRecommendations,
                ProjectedCostSavings = costSavingsProjection,
                PredictedFailures = predictedFailures,
                LastUpdated = DateTime.Now
            };
        }

        private async Task<List<EquipmentRiskScore>> CalculateEquipmentRiskScoresAsync()
        {
            var equipment = await _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.MaintenanceLogs)
                .Include(e => e.Building)
                .Include(e => e.Room)
                .Where(e => e.Status == EquipmentStatus.Active)
                .ToListAsync();

            var riskScores = new List<EquipmentRiskScore>();

            foreach (var item in equipment)
            {
                var riskScore = await CalculateIndividualRiskScoreAsync(item);
                riskScores.Add(riskScore);
            }

            return riskScores.OrderByDescending(rs => rs.RiskScore).ToList();
        }

        private async Task<EquipmentRiskScore> CalculateIndividualRiskScoreAsync(Equipment equipment)
        {
            var riskFactors = new List<string>();
            var riskScore = 0.0;

            // Factor 1: Equipment Age
            var ageRisk = CalculateAgeRisk(equipment, riskFactors);
            riskScore += ageRisk * 0.3; // 30% weight

            // Factor 2: Maintenance History
            var maintenanceRisk = CalculateMaintenanceRisk(equipment, riskFactors);
            riskScore += maintenanceRisk * 0.4; // 40% weight

            // Factor 3: Equipment Type Risk
            var typeRisk = CalculateEquipmentTypeRisk(equipment, riskFactors);
            riskScore += typeRisk * 0.2; // 20% weight

            // Factor 4: Environmental Factors
            var environmentRisk = CalculateEnvironmentalRisk(equipment, riskFactors);
            riskScore += environmentRisk * 0.1; // 10% weight

            // Determine risk level
            var riskLevel = riskScore switch
            {
                >= 0.7 => "High",
                >= 0.4 => "Medium",
                _ => "Low"
            };

            // Generate recommendations
            var recommendations = GenerateRiskBasedRecommendations(riskScore, equipment, riskFactors);

            return new EquipmentRiskScore
            {
                EquipmentId = equipment.EquipmentId,
                EquipmentName = equipment.EquipmentModel?.ModelName ?? "Unknown",
                EquipmentType = equipment.EquipmentType?.EquipmentTypeName ?? "Unknown",
                Location = $"{equipment.Building?.BuildingName ?? "Unknown"} - {equipment.Room?.RoomName ?? "Unknown"}",
                RiskScore = Math.Round(riskScore, 2),
                RiskLevel = riskLevel,
                RiskFactors = riskFactors,
                Recommendations = recommendations,
                NextMaintenanceDue = await CalculateNextMaintenanceDueAsync(equipment),
                EstimatedFailureDate = CalculateEstimatedFailureDate(riskScore),
                PredictedCost = CalculatePredictedMaintenanceCost(riskScore, equipment)
            };
        }

        private double CalculateAgeRisk(Equipment equipment, List<string> riskFactors)
        {
            if (!equipment.InstallationDate.HasValue)
            {
                riskFactors.Add("Unknown installation date");
                return 0.3; // Moderate risk for unknown age
            }

            var age = DateTime.Now - equipment.InstallationDate.Value;
            var ageInYears = age.TotalDays / 365.25;

            var equipmentType = equipment.EquipmentType?.EquipmentTypeName?.ToLower() ?? "";
            var expectedLifespan = equipmentType switch
            {
                var type when type.Contains("projector") => 5.0,
                var type when type.Contains("computer") => 6.0,
                var type when type.Contains("air condition") => 10.0,
                var type when type.Contains("printer") => 4.0,
                _ => 7.0
            };

            var ageRisk = Math.Min(1.0, ageInYears / expectedLifespan);

            if (ageRisk > 0.8)
                riskFactors.Add($"Equipment is {ageInYears:F1} years old (near end of life)");
            else if (ageRisk > 0.5)
                riskFactors.Add($"Equipment is {ageInYears:F1} years old (mature)");

            return ageRisk;
        }

        private double CalculateMaintenanceRisk(Equipment equipment, List<string> riskFactors)
        {
            var maintenanceLogs = equipment.MaintenanceLogs ?? new List<MaintenanceLog>();
            
            if (!maintenanceLogs.Any())
            {
                riskFactors.Add("No maintenance history available");
                return 0.5;
            }

            var lastMaintenance = maintenanceLogs.OrderByDescending(ml => ml.LogDate).First();
            var daysSinceLastMaintenance = (DateTime.Now - lastMaintenance.LogDate).TotalDays;

            // Risk increases with time since last maintenance
            var timeRisk = Math.Min(1.0, daysSinceLastMaintenance / 180.0); // 6 months baseline

            if (daysSinceLastMaintenance > 365)
                riskFactors.Add($"No maintenance for {daysSinceLastMaintenance:F0} days (critical)");
            else if (daysSinceLastMaintenance > 180)
                riskFactors.Add($"No maintenance for {daysSinceLastMaintenance:F0} days");

            // Corrective vs Preventive maintenance ratio
            var totalMaintenance = maintenanceLogs.Count;
            var correctiveMaintenance = maintenanceLogs.Count(ml => ml.MaintenanceType == MaintenanceType.Corrective);
            var correctiveRatio = totalMaintenance > 0 ? (double)correctiveMaintenance / totalMaintenance : 0;

            if (correctiveRatio > 0.5)
                riskFactors.Add($"High corrective maintenance ratio ({correctiveRatio:P0})");

            return (timeRisk * 0.7) + (correctiveRatio * 0.3);
        }

        private double CalculateEquipmentTypeRisk(Equipment equipment, List<string> riskFactors)
        {
            var equipmentType = equipment.EquipmentType?.EquipmentTypeName?.ToLower() ?? "";

            var typeRisk = equipmentType switch
            {
                var type when type.Contains("projector") => 0.6, // High maintenance equipment
                var type when type.Contains("printer") => 0.5,   // Mechanical wear
                var type when type.Contains("computer") => 0.3,  // Generally reliable
                var type when type.Contains("air condition") => 0.4, // Moderate risk
                _ => 0.4 // Default
            };

            if (typeRisk >= 0.5)
                riskFactors.Add($"{equipment.EquipmentType?.EquipmentTypeName} is high-maintenance equipment type");

            return typeRisk;
        }

        private double CalculateEnvironmentalRisk(Equipment equipment, List<string> riskFactors)
        {
            // Simulate environmental risk based on location
            var building = equipment.Building?.BuildingName?.ToLower() ?? "";
            var room = equipment.Room?.RoomName?.ToLower() ?? "";

            var environmentRisk = 0.2; // Base environmental risk

            // Higher risk for certain locations
            if (building.Contains("basement") || room.Contains("basement"))
            {
                environmentRisk += 0.2;
                riskFactors.Add("Located in basement (humidity risk)");
            }

            if (room.Contains("lab") || room.Contains("kitchen"))
            {
                environmentRisk += 0.15;
                riskFactors.Add("Located in high-use/chemical environment");
            }

            return Math.Min(1.0, environmentRisk);
        }

        private List<string> GenerateRiskBasedRecommendations(double riskScore, Equipment equipment, List<string> riskFactors)
        {
            var recommendations = new List<string>();

            if (riskScore >= 0.7)
            {
                recommendations.Add("🔴 CRITICAL: Schedule immediate preventive maintenance");
                recommendations.Add("📋 Consider equipment replacement planning");
                recommendations.Add("📊 Increase monitoring frequency to weekly");
            }
            else if (riskScore >= 0.4)
            {
                recommendations.Add("🟡 Schedule maintenance within next 30 days");
                recommendations.Add("📈 Monitor performance metrics closely");
                recommendations.Add("🔍 Review usage patterns and environment");
            }
            else
            {
                recommendations.Add("🟢 Continue routine maintenance schedule");
                recommendations.Add("👀 Monitor for any unusual changes");
            }

            // Type-specific recommendations
            var equipmentType = equipment.EquipmentType?.EquipmentTypeName?.ToLower() ?? "";
            if (equipmentType.Contains("projector"))
            {
                recommendations.Add("💡 Check lamp hours and replace if > 2000 hours");
                recommendations.Add("🧹 Clean air filters and vents");
            }
            else if (equipmentType.Contains("computer"))
            {
                recommendations.Add("💾 Check disk space and defragment if needed");
                recommendations.Add("🔄 Update software and security patches");
            }
            else if (equipmentType.Contains("air condition"))
            {
                recommendations.Add("❄️ Check refrigerant levels and air filters");
                recommendations.Add("🌡️ Verify temperature control calibration");
            }

            return recommendations;
        }

        private Task<DateTime?> CalculateNextMaintenanceDueAsync(Equipment equipment)
        {
            var lastMaintenance = equipment.MaintenanceLogs?
                .OrderByDescending(ml => ml.LogDate)
                .FirstOrDefault();

            var baseDate = lastMaintenance?.LogDate ?? equipment.InstallationDate ?? DateTime.Now.AddMonths(-6);

            // Determine interval based on equipment type
            var equipmentType = equipment.EquipmentType?.EquipmentTypeName?.ToLower() ?? "";
            var intervalDays = equipmentType switch
            {
                var type when type.Contains("projector") => 90,      // 3 months
                var type when type.Contains("computer") => 180,      // 6 months
                var type when type.Contains("air condition") => 90, // 3 months
                var type when type.Contains("printer") => 60,       // 2 months
                _ => 120 // 4 months default
            };

            return Task.FromResult<DateTime?>(baseDate.AddDays(intervalDays));
        }

        private DateTime CalculateEstimatedFailureDate(double riskScore)
        {
            var daysToFailure = riskScore switch
            {
                >= 0.8 => 30 + new Random().Next(0, 30),   // 1-2 months
                >= 0.6 => 90 + new Random().Next(0, 90),   // 3-6 months
                >= 0.4 => 180 + new Random().Next(0, 180), // 6-12 months
                _ => 365 + new Random().Next(0, 365)        // 1-2 years
            };

            return DateTime.Now.AddDays(daysToFailure);
        }

        private decimal CalculatePredictedMaintenanceCost(double riskScore, Equipment equipment)
        {
            var typeName = equipment.EquipmentType?.EquipmentTypeName?.ToLower() ?? "";
            var baseCost = typeName switch
            {
                var type when type.Contains("projector") => 150m,
                var type when type.Contains("computer") => 100m,
                var type when type.Contains("air condition") => 300m,
                var type when type.Contains("printer") => 80m,
                _ => 120m
            };

            // Higher risk = higher cost
            var riskMultiplier = 1 + (riskScore * 2); // 1x to 3x multiplier
            return Math.Round(baseCost * (decimal)riskMultiplier, 2);
        }

        private async Task<int> CalculateHighRiskEquipmentCountAsync()
        {
            var equipment = await _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.MaintenanceLogs)
                .Where(e => e.Status == EquipmentStatus.Active)
                .ToListAsync();

            var highRiskCount = 0;

            foreach (var item in equipment)
            {
                var riskScore = await CalculateIndividualRiskScoreAsync(item);
                if (riskScore.RiskScore >= 0.7) highRiskCount++;
            }

            return highRiskCount;
        }

        private async Task<List<PredictedFailure>> GetPredictedFailuresNext30DaysAsync()
        {
            var equipment = await _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.MaintenanceLogs)
                .Where(e => e.Status == EquipmentStatus.Active)
                .ToListAsync();

            var predictedFailures = new List<PredictedFailure>();

            foreach (var item in equipment)
            {
                var riskScore = await CalculateIndividualRiskScoreAsync(item);
                
                if (riskScore.RiskScore >= 0.7) // High risk equipment
                {
                    predictedFailures.Add(new PredictedFailure
                    {
                        EquipmentId = item.EquipmentId,
                        EquipmentName = item.EquipmentModel?.ModelName ?? "Unknown",
                        EquipmentType = item.EquipmentType?.EquipmentTypeName ?? "Unknown",
                        EstimatedFailureDate = riskScore.EstimatedFailureDate,
                        FailureProbability = riskScore.RiskScore,
                        EstimatedCost = riskScore.PredictedCost
                    });
                }
            }

            return predictedFailures.Where(pf => pf.EstimatedFailureDate <= DateTime.Now.AddDays(30)).ToList();
        }

        private async Task<List<FEENALOoFINALE.ViewModels.MaintenanceRecommendation>> GenerateMaintenanceRecommendationsAsync()
        {
            var riskScores = await CalculateEquipmentRiskScoresAsync();
            var recommendations = new List<FEENALOoFINALE.ViewModels.MaintenanceRecommendation>();

            foreach (var risk in riskScores.Where(rs => rs.RiskScore >= 0.4).Take(10))
            {
                var priority = risk.RiskScore >= 0.7 ? "High" : "Medium";
                var urgency = risk.RiskScore >= 0.7 ? "Immediate" : "Within 30 days";

                recommendations.Add(new FEENALOoFINALE.ViewModels.MaintenanceRecommendation
                {
                    EquipmentId = risk.EquipmentId,
                    EquipmentName = risk.EquipmentName,
                    RecommendationType = priority == "High" ? "Preventive Maintenance" : "Routine Inspection",
                    Priority = priority,
                    Urgency = urgency,
                    EstimatedCost = risk.PredictedCost,
                    Recommendations = risk.Recommendations
                });
            }

            return recommendations;
        }

        private async Task<decimal> CalculatePredictiveCostSavingsAsync()
        {
            var recommendations = await GenerateMaintenanceRecommendationsAsync();
            
            // Estimate cost savings by preventing failures
            var preventiveCost = recommendations.Sum(r => r.EstimatedCost);
            var estimatedFailureCost = preventiveCost * 3; // Failure typically costs 3x preventive maintenance
            
            return Math.Round((decimal)(estimatedFailureCost - preventiveCost), 2);
        }

        private async Task<List<MaintenancePrediction>> PredictMaintenanceForEquipmentAsync(int equipmentId)
        {
            var equipment = await _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.MaintenanceLogs)
                .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId);

            if (equipment == null) return new List<MaintenancePrediction>();

            var riskScore = await CalculateIndividualRiskScoreAsync(equipment);
            
            return new List<MaintenancePrediction>
            {
                new MaintenancePrediction
                {
                    EquipmentId = equipment.EquipmentId,
                    EquipmentName = equipment.EquipmentModel?.ModelName ?? "Unknown",
                    PredictedMaintenanceDate = riskScore.NextMaintenanceDue ?? DateTime.Now.AddDays(90),
                    MaintenanceType = riskScore.RiskScore >= 0.7 ? "Preventive" : "Routine",
                    Confidence = Math.Round((1 - riskScore.RiskScore) * 100, 1),
                    EstimatedCost = riskScore.PredictedCost
                }
            };
        }

        private async Task<List<MaintenancePrediction>> PredictMaintenanceForAllEquipmentAsync()
        {
            var equipment = await _context.Equipment
                .Include(e => e.EquipmentType)
                .Include(e => e.EquipmentModel)
                .Include(e => e.MaintenanceLogs)
                .Where(e => e.Status == EquipmentStatus.Active)
                .ToListAsync();

            var predictions = new List<MaintenancePrediction>();

            foreach (var item in equipment)
            {
                var itemPredictions = await PredictMaintenanceForEquipmentAsync(item.EquipmentId);
                predictions.AddRange(itemPredictions);
            }

            return predictions.OrderBy(p => p.PredictedMaintenanceDate).ToList();
        }

        private async Task<int> CreatePredictiveMaintenanceTasksAsync()
        {
            var recommendations = await GenerateMaintenanceRecommendationsAsync();
            var tasksCreated = 0;

            foreach (var recommendation in recommendations.Where(r => r.Priority == "High"))
            {
                // Check if task already exists
                var existingTask = await _context.MaintenanceTasks
                    .Where(mt => mt.EquipmentId == recommendation.EquipmentId && 
                                mt.Status == MaintenanceStatus.Pending)
                    .FirstOrDefaultAsync();

                if (existingTask == null)
                {
                    var task = new MaintenanceTask
                    {
                        EquipmentId = recommendation.EquipmentId,
                        Description = $"Predictive Maintenance: {recommendation.RecommendationType}",
                        ScheduledDate = DateTime.Now.AddDays(recommendation.Urgency == "Immediate" ? 1 : 7),
                        Status = MaintenanceStatus.Pending,
                        Priority = TaskPriority.Critical
                    };

                    _context.MaintenanceTasks.Add(task);
                    tasksCreated++;
                }
            }

            await _context.SaveChangesAsync();
            return tasksCreated;
        }

        private async Task<MaintenancePlanningData> GetMaintenancePlanningDataAsync()
        {
            var predictions = await PredictMaintenanceForAllEquipmentAsync();
            var recommendations = await GenerateMaintenanceRecommendationsAsync();

            return new MaintenancePlanningData
            {
                UpcomingMaintenance = predictions.Where(p => p.PredictedMaintenanceDate <= DateTime.Now.AddDays(30)).ToList(),
                MaintenanceRecommendations = recommendations,
                MonthlyPredictions = GenerateMonthlyPredictions(predictions),
                ResourceRequirements = CalculateResourceRequirements(predictions),
                BudgetProjections = CalculateBudgetProjections(predictions)
            };
        }

        private Dictionary<string, int> GenerateMonthlyPredictions(List<MaintenancePrediction> predictions)
        {
            return predictions
                .Where(p => p.PredictedMaintenanceDate <= DateTime.Now.AddMonths(12))
                .GroupBy(p => p.PredictedMaintenanceDate.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        private Dictionary<string, object> CalculateResourceRequirements(List<MaintenancePrediction> predictions)
        {
            var next30Days = predictions.Where(p => p.PredictedMaintenanceDate <= DateTime.Now.AddDays(30));
            
            return new Dictionary<string, object>
            {
                ["TechnicianHours"] = next30Days.Count() * 2, // Estimate 2 hours per task
                ["EstimatedCost"] = next30Days.Sum(p => p.EstimatedCost),
                ["TaskCount"] = next30Days.Count()
            };
        }

        private Dictionary<string, decimal> CalculateBudgetProjections(List<MaintenancePrediction> predictions)
        {
            var result = new Dictionary<string, decimal>();
            
            for (int month = 1; month <= 12; month++)
            {
                var monthKey = DateTime.Now.AddMonths(month - 1).ToString("yyyy-MM");
                var monthPredictions = predictions.Where(p => 
                    p.PredictedMaintenanceDate.Year == DateTime.Now.AddMonths(month - 1).Year &&
                    p.PredictedMaintenanceDate.Month == DateTime.Now.AddMonths(month - 1).Month);
                
                result[monthKey] = monthPredictions.Sum(p => p.EstimatedCost);
            }
            
            return result;
        }

        private async Task<string> ExtractTextFromPdf(IFormFile file)
        {
            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Seek(0, SeekOrigin.Begin);

                using (var pdfDocument = new PdfDocument(new PdfReader(stream)))
                {
                    StringBuilder text = new StringBuilder();
                    for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                    {
                        var page = pdfDocument.GetPage(i);
                        var strategy = new SimpleTextExtractionStrategy();
                        text.Append(PdfTextExtractor.GetTextFromPage(page, strategy));
                    }
                    return text.ToString();
                }
            }
        }

        private async Task UpdateEquipmentUsageData(Dictionary<string, double> equipmentUsage)
        {
            // Example: Update the AverageWeeklyUsageHours property of each equipment
            foreach (var kvp in equipmentUsage)
            {
                int equipmentId = int.Parse(kvp.Key); // Assuming the key is the EquipmentId
                double usageHours = kvp.Value;

                var equipment = await _context.Equipment.FindAsync(equipmentId);
                if (equipment != null)
                {
                    // Update the equipment's usage data
                    equipment.AverageWeeklyUsageHours = usageHours;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
