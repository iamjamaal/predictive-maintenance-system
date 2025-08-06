using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Models.ViewModels;
using FEENALOoFINALE.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace FEENALOoFINALE.Controllers
{
    [Authorize]
    public class TimetableController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<TimetableController> _logger;
        private readonly IWebHostEnvironment _environment;

        public TimetableController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<TimetableController> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _environment = environment;
        }

        // GET: Timetable Management Dashboard
        public async Task<IActionResult> Index()
        {
            var viewModel = new TimetableManagementViewModel
            {
                PageTitle = "Timetable Management Dashboard",
                PageDescription = "Manage semester timetables and track equipment usage",
                Notifications = new List<NotificationMessage>(),
                QuickActions = new List<QuickActionItem>(),
                Statistics = new TimetableStatistics(),
                ProgressInfo = new SemesterProgressInfo(),
                RecentSemesters = new List<Semester>(),
                CurrentSemester = null
            };

            try
            {
                // Get current active semester
                viewModel.CurrentSemester = await _context.Semesters
                    .Include(s => s.UploadedBy)
                    .Include(s => s.SemesterEquipmentUsages)
                        .ThenInclude(seu => seu.Equipment)
                    .Where(s => s.IsActive)
                    .OrderByDescending(s => s.StartDate)
                    .FirstOrDefaultAsync();

                // Check for automatic semester completion
                if (viewModel.CurrentSemester != null)
                {
                    await CheckAndUpdateSemesterCompletion(viewModel.CurrentSemester);
                }

                // Get recent semesters
                viewModel.RecentSemesters = await _context.Semesters
                    .Include(s => s.UploadedBy)
                    .OrderByDescending(s => s.StartDate)
                    .Take(5)
                    .ToListAsync();

                // Calculate statistics
                viewModel.Statistics = await CalculateTimetableStatistics();

                // Generate progress info if there's a current semester
                if (viewModel.CurrentSemester != null)
                {
                    viewModel.ProgressInfo = GenerateProgressInfo(viewModel.CurrentSemester);
                }
                else
                {
                    // Set default progress info for when there's no current semester
                    viewModel.ProgressInfo = new SemesterProgressInfo
                    {
                        ProgressPercentage = 0,
                        WeeksElapsed = 0,
                        WeeksRemaining = 0,
                        DaysRemaining = 0,
                        CurrentWeek = 0,
                        Status = SemesterStatus.Inactive,
                        StatusDescription = "No active semester"
                    };
                }

                // Add quick actions
                viewModel.QuickActions = GenerateQuickActions(viewModel.CurrentSemester);

                // Add notifications
                viewModel.Notifications = await GenerateNotifications(viewModel.CurrentSemester);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading timetable management dashboard");
                viewModel.Notifications.Add(new NotificationMessage
                {
                    Type = NotificationType.Error,
                    Message = "Error loading dashboard data. Please try again."
                });
                return View(viewModel);
            }
        }

        // GET: Upload new semester timetable
        public IActionResult Upload()
        {
            var viewModel = new SemesterUploadViewModel
            {
                StartDate = DateTime.Today,
                NumberOfWeeks = 16
            };

            return View(viewModel);
        }

        // POST: Upload new semester timetable
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(SemesterUploadViewModel model)
        {
            _logger.LogInformation("Starting semester upload process");
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                // Add validation feedback
                TempData["ErrorMessage"] = "Please correct the errors below and try again.";
                return View(model);
            }

            try
            {
                _logger.LogInformation("Getting current user");
                var currentUser = await _userManager.GetUserAsync(User);
                _logger.LogInformation("Current user: {UserId}", currentUser?.Id ?? "null");
                
                // Check for overlapping semesters
                var modelEndDate = model.StartDate.AddDays(model.NumberOfWeeks * 7);
                var overlappingSemester = await _context.Semesters
                    .Where(s => s.IsActive &&
                               ((s.StartDate <= model.StartDate && s.StartDate.AddDays(s.NumberOfWeeks * 7) >= model.StartDate) ||
                                (s.StartDate <= modelEndDate && s.StartDate.AddDays(s.NumberOfWeeks * 7) >= modelEndDate)))
                    .FirstOrDefaultAsync();

                if (overlappingSemester != null && !model.ReplaceCurrentSemester)
                {
                    ModelState.AddModelError("", $"There is an overlapping active semester: '{overlappingSemester.SemesterName}' ({overlappingSemester.StartDate:MMM dd} - {overlappingSemester.StartDate.AddDays(overlappingSemester.NumberOfWeeks * 7):MMM dd, yyyy}). Please check 'Replace Current Active Semester' if you want to replace it.");
                    TempData["ErrorMessage"] = "Semester dates overlap with an existing active semester.";
                    return View(model);
                }

                // Check if replacing current semester
                if (model.ReplaceCurrentSemester)
                {
                    var currentSemester = await _context.Semesters
                        .Where(s => s.IsActive)
                        .FirstOrDefaultAsync();
                    
                    if (currentSemester != null)
                    {
                        currentSemester.IsActive = false;
                        currentSemester.LastModified = DateTime.UtcNow;
                        currentSemester.ProcessingMessage = $"Replaced by new semester '{model.SemesterName}' on {DateTime.Now:MMM dd, yyyy}";
                    }
                }

                _logger.LogInformation("Creating new semester: {SemesterName}, StartDate: {StartDate}, Weeks: {Weeks}", 
                    model.SemesterName, model.StartDate, model.NumberOfWeeks);
                
                // Create new semester
                var semester = new Semester
                {
                    SemesterName = model.SemesterName,
                    StartDate = model.StartDate,
                    NumberOfWeeks = model.NumberOfWeeks,
                    UploadedByUserId = currentUser?.Id,
                    IsActive = true,
                    ProcessingStatus = SemesterProcessingStatus.Pending,
                    ProcessingMessage = "Upload completed. Processing timetable file...",
                    CreatedDate = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };
                
                _logger.LogInformation("Semester object created successfully");

                // Handle file upload
                _logger.LogInformation("Processing file upload. File: {FileName}, Size: {FileSize}", 
                    model.TimetableFile?.FileName ?? "null", model.TimetableFile?.Length ?? 0);
                
                if (model.TimetableFile != null && model.TimetableFile.Length > 0)
                {
                    var uploadResult = await SaveTimetableFile(model.TimetableFile, semester);
                    if (!uploadResult.Success)
                    {
                        _logger.LogError("File upload failed: {ErrorMessage}", uploadResult.ErrorMessage);
                        ModelState.AddModelError("TimetableFile", uploadResult.ErrorMessage ?? "An unknown error occurred during file upload.");
                        TempData["ErrorMessage"] = uploadResult.ErrorMessage;
                        return View(model);
                    }

                    _logger.LogInformation("File uploaded successfully: {FilePath}", uploadResult.FilePath);
                    semester.TimetableFilePath = uploadResult.FilePath;
                    semester.OriginalFileName = uploadResult.OriginalFileName;
                    semester.FileSizeBytes = uploadResult.FileSizeBytes;
                }
                else
                {
                    _logger.LogWarning("No file provided for upload");
                    ModelState.AddModelError("TimetableFile", "Timetable file is required.");
                    TempData["ErrorMessage"] = "Please select a timetable file to upload.";
                    return View(model);
                }

                _logger.LogInformation("Adding semester to database");
                _context.Semesters.Add(semester);
                
                _logger.LogInformation("Saving changes to database");
                await _context.SaveChangesAsync();
                _logger.LogInformation("Semester saved successfully with ID: {SemesterId}", semester.SemesterId);

                // Process the timetable file in the background
                _logger.LogInformation("Starting background processing for semester {SemesterId}", semester.SemesterId);
                _ = Task.Run(async () => await ProcessTimetableFile(semester.SemesterId));

                // Enhanced success message
                var successMessage = $"Semester '{model.SemesterName}' uploaded successfully! " +
                                   $"Duration: {model.NumberOfWeeks} weeks ({model.StartDate:MMM dd} - {semester.EndDate:MMM dd, yyyy}). " +
                                   "Timetable processing will begin shortly and may take a few minutes.";

                TempData["SuccessMessage"] = successMessage;
                TempData["NewSemesterId"] = semester.SemesterId;
                
                return RedirectToAction(nameof(Details), new { id = semester.SemesterId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading semester timetable: {Message}", ex.Message);
                _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                
                // More specific error messages based on exception type
                string errorMessage = ex switch
                {
                    UnauthorizedAccessException => "Permission denied. Please check file permissions.",
                    DirectoryNotFoundException => "Upload directory not found. Please contact administrator.",
                    IOException => "File system error. Please try again.",
                    ArgumentException => "Invalid file format or data. Please check your input.",
                    _ => $"An unexpected error occurred: {ex.Message}"
                };
                
                ModelState.AddModelError("", errorMessage);
                TempData["ErrorMessage"] = errorMessage;
                return View(model);
            }
        }

        // GET: Semester Progress Dashboard
        public async Task<IActionResult> Progress(int? id)
        {
            try
            {
                Semester? semester;
                
                if (id.HasValue)
                {
                    semester = await _context.Semesters
                        .Include(s => s.SemesterEquipmentUsages)
                            .ThenInclude(seu => seu.Equipment)
                                .ThenInclude(e => e.EquipmentType)
                        .Include(s => s.SemesterEquipmentUsages)
                            .ThenInclude(seu => seu.Equipment)
                                .ThenInclude(e => e.Building)
                        .Include(s => s.SemesterEquipmentUsages)
                            .ThenInclude(seu => seu.Equipment)
                                .ThenInclude(e => e.Room)
                        .FirstOrDefaultAsync(s => s.SemesterId == id.Value);
                }
                else
                {
                    // Get current active semester
                    semester = await _context.Semesters
                        .Include(s => s.SemesterEquipmentUsages)
                            .ThenInclude(seu => seu.Equipment.EquipmentModel)
                        .Include(s => s.SemesterEquipmentUsages)
                            .ThenInclude(seu => seu.Equipment.EquipmentType)
                        .Include(s => s.SemesterEquipmentUsages)
                            .ThenInclude(seu => seu.Equipment.Building)
                        .Include(s => s.SemesterEquipmentUsages)
                            .ThenInclude(seu => seu.Equipment.Room)
                        .Where(s => s.IsActive)
                        .OrderByDescending(s => s.StartDate)
                        .AsSplitQuery()
                        .FirstOrDefaultAsync();
                }

                if (semester == null)
                {
                    TempData["ErrorMessage"] = "No active semester found. Please upload a timetable first.";
                    return RedirectToAction(nameof(Index));
                }

                var viewModel = new SemesterProgressDashboardViewModel
                {
                    CurrentSemester = semester,
                    ProgressInfo = GenerateProgressInfo(semester),
                    EquipmentUsage = await GenerateEquipmentUsageSummary(semester),
                    WeeklyProgress = GenerateWeeklyProgress(semester),
                    Statistics = await CalculateSemesterStatistics(semester),
                    MaintenanceRecommendations = await GenerateMaintenanceRecommendations(semester)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading semester progress dashboard");
                TempData["ErrorMessage"] = "Error loading progress data. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Semester Details
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var semester = await _context.Semesters
                    .Include(s => s.UploadedBy)
                    .Include(s => s.SemesterEquipmentUsages)
                        .ThenInclude(seu => seu.Equipment.EquipmentModel)
                    .Include(s => s.SemesterEquipmentUsages)
                        .ThenInclude(seu => seu.Equipment.EquipmentType)
                    .Include(s => s.SemesterEquipmentUsages)
                        .ThenInclude(seu => seu.Equipment.Building)
                    .Include(s => s.SemesterEquipmentUsages)
                        .ThenInclude(seu => seu.Equipment.Room)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(s => s.SemesterId == id);

                if (semester == null)
                {
                    return NotFound();
                }

                var viewModel = new SemesterDetailsViewModel
                {
                    Semester = semester,
                    ProgressInfo = GenerateProgressInfo(semester),
                    EquipmentUsage = await GenerateEquipmentUsageSummary(semester),
                    WeeklyProgress = GenerateWeeklyProgress(semester),
                    Statistics = await CalculateSemesterStatistics(semester),
                    ProcessingLogs = GetProcessingLogs(semester),
                    CanEdit = true,
                    CanDelete = true, // Allow deletion of all semesters, including active ones
                    CanReprocess = semester.ProcessingStatus == SemesterProcessingStatus.Failed
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading semester details for ID {SemesterId}", id);
                TempData["ErrorMessage"] = "Error loading semester details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Edit Semester
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var semester = await _context.Semesters
                    .Include(s => s.UploadedBy)
                    .FirstOrDefaultAsync(s => s.SemesterId == id);

                if (semester == null)
                {
                    return NotFound();
                }

                var viewModel = new SemesterEditViewModel
                {
                    SemesterId = semester.SemesterId,
                    SemesterName = semester.SemesterName,
                    StartDate = semester.StartDate,
                    NumberOfWeeks = semester.NumberOfWeeks,
                    IsActive = semester.IsActive,
                    CurrentFileName = semester.OriginalFileName,
                    UploadDate = semester.UploadDate,
                    UploadedByName = semester.UploadedBy?.FullName ?? "Unknown",
                    ProcessingStatus = semester.ProcessingStatus,
                    ProcessingMessage = semester.ProcessingMessage
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading semester for edit: {SemesterId}", id);
                TempData["ErrorMessage"] = "Error loading semester data. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Edit Semester
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SemesterEditViewModel model)
        {
            if (id != model.SemesterId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors below and try again.";
                return View(model);
            }

            try
            {
                var semester = await _context.Semesters.FindAsync(id);
                if (semester == null)
                {
                    return NotFound();
                }

                // Check for overlapping semesters (excluding current semester)
                var modelEndDate = model.StartDate.AddDays(model.NumberOfWeeks * 7);
                var overlappingSemester = await _context.Semesters
                    .Where(s => s.SemesterId != id && s.IsActive &&
                               ((s.StartDate <= model.StartDate && s.StartDate.AddDays(s.NumberOfWeeks * 7) >= model.StartDate) ||
                                (s.StartDate <= modelEndDate && s.StartDate.AddDays(s.NumberOfWeeks * 7) >= modelEndDate)))
                    .FirstOrDefaultAsync();

                if (overlappingSemester != null)
                {
                    ModelState.AddModelError("", $"There is an overlapping active semester: '{overlappingSemester.SemesterName}' ({overlappingSemester.StartDate:MMM dd} - {overlappingSemester.StartDate.AddDays(overlappingSemester.NumberOfWeeks * 7):MMM dd, yyyy}).");
                    TempData["ErrorMessage"] = "Semester dates overlap with another active semester.";
                    return View(model);
                }

                // Track changes for audit
                var changes = new List<string>();
                if (semester.SemesterName != model.SemesterName)
                    changes.Add($"Name: '{semester.SemesterName}' → '{model.SemesterName}'");
                if (semester.StartDate != model.StartDate)
                    changes.Add($"Start Date: {semester.StartDate:MMM dd, yyyy} → {model.StartDate:MMM dd, yyyy}");
                if (semester.NumberOfWeeks != model.NumberOfWeeks)
                    changes.Add($"Duration: {semester.NumberOfWeeks} weeks → {model.NumberOfWeeks} weeks");
                if (semester.IsActive != model.IsActive)
                    changes.Add($"Status: {(semester.IsActive ? "Active" : "Inactive")} → {(model.IsActive ? "Active" : "Inactive")}");

                // Update basic properties
                semester.SemesterName = model.SemesterName;
                semester.StartDate = model.StartDate;
                semester.NumberOfWeeks = model.NumberOfWeeks;
                semester.IsActive = model.IsActive;
                semester.LastModified = DateTime.UtcNow;

                // Handle new file upload
                if (model.NewTimetableFile != null && model.NewTimetableFile.Length > 0)
                {
                    changes.Add("Timetable file: Replaced with new file");
                    
                    // Delete old file if exists
                    if (!string.IsNullOrEmpty(semester.TimetableFilePath))
                    {
                        DeleteTimetableFile(semester.TimetableFilePath);
                    }

                    var uploadResult = await SaveTimetableFile(model.NewTimetableFile, semester);
                    if (!uploadResult.Success)
                    {
                        ModelState.AddModelError("NewTimetableFile", uploadResult.ErrorMessage ?? "An unknown error occurred during file upload.");
                        TempData["ErrorMessage"] = uploadResult.ErrorMessage;
                        return View(model);
                    }

                    semester.TimetableFilePath = uploadResult.FilePath;
                    semester.OriginalFileName = uploadResult.OriginalFileName;
                    semester.FileSizeBytes = uploadResult.FileSizeBytes;
                    semester.ProcessingStatus = SemesterProcessingStatus.Pending;
                    semester.ProcessingMessage = "File replaced. Reprocessing timetable...";

                    // Reprocess the file
                    _ = Task.Run(async () => await ProcessTimetableFile(semester.SemesterId));
                }

                // Update processing message if there were changes
                if (changes.Any())
                {
                    semester.ProcessingMessage = $"Updated on {DateTime.Now:MMM dd, yyyy}. Changes: {string.Join(", ", changes)}";
                }

                await _context.SaveChangesAsync();

                // Enhanced success message
                var successMessage = $"Semester '{model.SemesterName}' updated successfully!";
                if (changes.Any())
                {
                    successMessage += $" Changes made: {string.Join(", ", changes)}";
                }
                if (model.NewTimetableFile != null)
                {
                    successMessage += " New timetable file is being processed.";
                }

                TempData["SuccessMessage"] = successMessage;
                return RedirectToAction(nameof(Details), new { id = semester.SemesterId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating semester: {SemesterId}", id);
                ModelState.AddModelError("", "An error occurred while updating the semester. Please try again.");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again or contact support if the problem persists.";
                return View(model);
            }
        }

        // POST: Delete Semester
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int SemesterId)
        {
            try
            {
                _logger.LogInformation("Attempting to delete semester with ID: {SemesterId}", SemesterId);
                
                var semester = await _context.Semesters
                    .Include(s => s.SemesterEquipmentUsages)
                    .FirstOrDefaultAsync(s => s.SemesterId == SemesterId);

                if (semester == null)
                {
                    _logger.LogWarning("Semester with ID {SemesterId} not found for deletion", SemesterId);
                    TempData["ErrorMessage"] = "Semester not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Delete related data first
                if (semester.SemesterEquipmentUsages != null && semester.SemesterEquipmentUsages.Any())
                {
                    _context.SemesterEquipmentUsages.RemoveRange(semester.SemesterEquipmentUsages);
                }

                // Delete the semester
                _context.Semesters.Remove(semester);
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Successfully deleted semester: {SemesterName} (ID: {SemesterId})", 
                    semester.SemesterName, semester.SemesterId);
                
                TempData["SuccessMessage"] = $"Semester '{semester.SemesterName}' has been successfully deleted.";
                
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting semester with ID: {SemesterId}", SemesterId);
                TempData["ErrorMessage"] = "An error occurred while deleting the semester. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Force Complete Processing (for debugging)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceCompleteProcessing(int id)
        {
            try
            {
                var semester = await _context.Semesters.FindAsync(id);
                if (semester == null)
                {
                    TempData["ErrorMessage"] = "Semester not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Force complete the processing with minimal data
                semester.ProcessingStatus = SemesterProcessingStatus.Completed;
                semester.ProcessingMessage = "Processing completed manually (no equipment data parsed).";
                semester.TotalEquipmentHours = 0;
                semester.EquipmentUsageDataJson = "{}";
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Processing for semester '{semester.SemesterName}' has been manually completed.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forcing completion for semester: {SemesterId}", id);
                TempData["ErrorMessage"] = "An error occurred while completing the processing.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // GET: Semester List
        public async Task<IActionResult> List(
            int page = 1,
            int pageSize = 10,
            string searchTerm = "",
            string sortBy = "StartDate",
            string sortDirection = "desc",
            string[]? statusFilter = null,
            string[]? processingStatusFilter = null,
            int? yearFilter = null)
        {
            try
            {
                var query = _context.Semesters
                    .Include(s => s.UploadedBy)
                    .AsQueryable();

                // Apply search filter
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(s => s.SemesterName.Contains(searchTerm));
                }

                // Apply status filter
                if (statusFilter != null && statusFilter.Any())
                {
                    var statuses = statusFilter.Select(s => Enum.Parse<SemesterStatus>(s)).ToList();
                    query = query.Where(s => statuses.Contains(s.Status));
                }

                // Apply processing status filter
                if (processingStatusFilter != null && processingStatusFilter.Any())
                {
                    var processingStatuses = processingStatusFilter.Select(s => Enum.Parse<SemesterProcessingStatus>(s)).ToList();
                    query = query.Where(s => processingStatuses.Contains(s.ProcessingStatus));
                }

                // Apply year filter
                if (yearFilter.HasValue)
                {
                    query = query.Where(s => s.StartDate.Year == yearFilter.Value);
                }

                // Apply sorting
                query = sortBy.ToLower() switch
                {
                    "semestername" => sortDirection == "desc" 
                        ? query.OrderByDescending(s => s.SemesterName)
                        : query.OrderBy(s => s.SemesterName),
                    "startdate" => sortDirection == "desc" 
                        ? query.OrderByDescending(s => s.StartDate)
                        : query.OrderBy(s => s.StartDate),
                    "status" => sortDirection == "desc" 
                        ? query.OrderByDescending(s => s.Status)
                        : query.OrderBy(s => s.Status),
                    _ => query.OrderByDescending(s => s.StartDate)
                };

                var totalRecords = await query.CountAsync();
                var semesters = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new TimetableListViewModel
                {
                    Semesters = semesters.Select(s => new SemesterListItem
                    {
                        SemesterId = s.SemesterId,
                        SemesterName = s.SemesterName,
                        StartDate = s.StartDate,
                        EndDate = s.StartDate.AddDays(s.NumberOfWeeks * 7),
                        NumberOfWeeks = s.NumberOfWeeks,
                        Status = s.Status,
                        ProgressPercentage = s.ProgressPercentage,
                        IsActive = s.IsActive,
                        UploadedByName = s.UploadedBy?.FullName ?? "Unknown",
                        UploadDate = s.UploadDate,
                        ProcessingStatus = s.ProcessingStatus,
                        EquipmentCount = s.SemesterEquipmentUsages?.Count ?? 0,
                        TotalUsageHours = s.TotalEquipmentHours,
                        CanEdit = true,
                        CanDelete = true, // Allow deletion of all semesters, including active ones
                        CanActivate = s.Status != SemesterStatus.Active && s.ProcessingStatus == SemesterProcessingStatus.Completed
                    }).ToList(),
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    SearchTerm = searchTerm,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    FilterOptions = await BuildFilterOptions()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading semester list");
                TempData["ErrorMessage"] = "Error loading semester list. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Helper Methods
        private async Task<TimetableStatistics> CalculateTimetableStatistics()
        {
            var statistics = new TimetableStatistics();

            try
            {
                statistics.TotalSemesters = await _context.Semesters.CountAsync();
                statistics.ActiveSemesters = await _context.Semesters.CountAsync(s => s.IsActive);
                statistics.CompletedSemesters = await _context.Semesters.CountAsync(s => s.IsActive && DateTime.Now > s.StartDate.AddDays(s.NumberOfWeeks * 7));
                
                // Handle average calculation for empty collections
                if (statistics.TotalSemesters > 0)
                {
                    statistics.AverageWeeksPerSemester = await _context.Semesters.AverageAsync(s => (double)s.NumberOfWeeks);
                }
                else
                {
                    statistics.AverageWeeksPerSemester = 0;
                }

                statistics.TotalEquipmentTracked = await _context.SemesterEquipmentUsages.Select(seu => seu.EquipmentId).Distinct().CountAsync();
                statistics.TotalUsageHours = await _context.Semesters.SumAsync(s => s.TotalEquipmentHours);
                
                // Handle max calculation for empty collections - check if any semesters exist first
                if (statistics.TotalSemesters > 0)
                {
                    statistics.LastUploadDate = await _context.Semesters.MaxAsync(s => (DateTime?)s.UploadDate);
                }
                else
                {
                    statistics.LastUploadDate = null;
                }
                
                statistics.PendingProcessing = await _context.Semesters.CountAsync(s => s.ProcessingStatus == SemesterProcessingStatus.Pending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating timetable statistics");
                // Return default statistics on error
                statistics = new TimetableStatistics
                {
                    TotalSemesters = 0,
                    ActiveSemesters = 0,
                    CompletedSemesters = 0,
                    AverageWeeksPerSemester = 0,
                    TotalEquipmentTracked = 0,
                    TotalUsageHours = 0,
                    LastUploadDate = null,
                    PendingProcessing = 0
                };
            }

            return statistics;
        }

        private SemesterProgressInfo GenerateProgressInfo(Semester semester)
        {
            return new SemesterProgressInfo
            {
                ProgressPercentage = semester.ProgressPercentage,
                WeeksElapsed = semester.WeeksElapsed,
                WeeksRemaining = semester.NumberOfWeeks - semester.WeeksElapsed,
                DaysRemaining = semester.DaysRemaining,
                CurrentWeek = semester.CurrentWeek,
                Status = semester.Status,
                StatusDescription = GetStatusDescription(semester.Status)
            };
        }

        private string GetStatusDescription(SemesterStatus status)
        {
            return status switch
            {
                SemesterStatus.Upcoming => "Semester has not started yet",
                SemesterStatus.Active => "Semester is currently in progress",
                SemesterStatus.Completed => "Semester has been completed",
                SemesterStatus.Inactive => "Semester is inactive",
                _ => "Unknown status"
            };
        }

        // Enhanced quick actions generation
        private List<QuickActionItem> GenerateQuickActions(Semester? currentSemester)
        {
            var actions = new List<QuickActionItem>();

            actions.Add(new QuickActionItem
            {
                Title = "Upload New Semester",
                Description = "Upload a new semester timetable",
                Icon = "bi-upload",
                Controller = "Timetable",
                Action = "Upload",
                Color = "primary"
            });

            if (currentSemester != null)
            {
                actions.Add(new QuickActionItem
                {
                    Title = "View Progress",
                    Description = $"Track progress for {currentSemester.SemesterName}",
                    Icon = "bi-graph-up",
                    Controller = "Timetable",
                    Action = "Progress",
                    RouteValues = new Dictionary<string, string> { { "id", currentSemester.SemesterId.ToString() } },
                    Color = "success"
                });

                actions.Add(new QuickActionItem
                {
                    Title = "Edit Semester",
                    Description = $"Modify {currentSemester.SemesterName} details",
                    Icon = "bi-pencil",
                    Controller = "Timetable",
                    Action = "Edit",
                    RouteValues = new Dictionary<string, string> { { "id", currentSemester.SemesterId.ToString() } },
                    Color = "warning"
                });

                // Show delete option for all semesters (with appropriate warnings in the confirmation view)
                actions.Add(new QuickActionItem
                {
                    Title = currentSemester.IsActive ? "Delete Active Semester" : "Delete Semester",
                    Description = currentSemester.IsActive ? 
                        $"Remove active semester {currentSemester.SemesterName} (with warnings)" : 
                        $"Remove {currentSemester.SemesterName} and its data",
                    Icon = "bi-trash",
                    Controller = "Timetable",
                    Action = "ConfirmDelete",
                    RouteValues = new Dictionary<string, string> { { "id", currentSemester.SemesterId.ToString() } },
                    Color = "danger"
                });
            }

            actions.Add(new QuickActionItem
            {
                Title = "View All Semesters",
                Description = "Browse semester history and statistics",
                Icon = "bi-list",
                Controller = "Timetable",
                Action = "List",
                Color = "info"
            });

            return actions;
        }

        // Enhanced notification generation
        private async Task<List<NotificationMessage>> GenerateNotifications(Semester? currentSemester)
        {
            var notifications = new List<NotificationMessage>();

            try
            {
                if (currentSemester == null)
                {
                    notifications.Add(new NotificationMessage
                    {
                        Type = NotificationType.Warning,
                        Message = "No active semester found. Please upload a new semester timetable to enable equipment usage tracking."
                    });
                    return notifications;
                }

                // Check semester progress
                if (currentSemester.ProgressPercentage >= 90)
                {
                    notifications.Add(new NotificationMessage
                    {
                        Type = NotificationType.Warning,
                        Message = $"Semester '{currentSemester.SemesterName}' is {currentSemester.ProgressPercentage:F1}% complete. Consider preparing the next semester's timetable."
                    });
                }

                if (currentSemester.ProgressPercentage >= 95)
                {
                    notifications.Add(new NotificationMessage
                    {
                        Type = NotificationType.Warning,
                        Message = $"Semester '{currentSemester.SemesterName}' is nearly complete ({currentSemester.ProgressPercentage:F1}%). Please upload the next semester's timetable soon."
                    });
                }

                // Check processing status
                if (currentSemester.ProcessingStatus == SemesterProcessingStatus.Failed)
                {
                    notifications.Add(new NotificationMessage
                    {
                        Type = NotificationType.Error,
                        Message = $"Timetable processing failed for semester '{currentSemester.SemesterName}'. Please review and reprocess the file."
                    });
                }

                if (currentSemester.ProcessingStatus == SemesterProcessingStatus.Processing)
                {
                    notifications.Add(new NotificationMessage
                    {
                        Type = NotificationType.Info,
                        Message = $"Timetable for semester '{currentSemester.SemesterName}' is currently being processed. This may take a few minutes."
                    });
                }

                // Check for upcoming semesters
                var upcomingSemesters = await _context.Semesters
                    .Where(s => s.StartDate > DateTime.Now && s.StartDate <= DateTime.Now.AddDays(30))
                    .OrderBy(s => s.StartDate)
                    .ToListAsync();

                if (upcomingSemesters.Any())
                {
                    var nextSemester = upcomingSemesters.First();
                    var daysUntilStart = (nextSemester.StartDate - DateTime.Now).Days;
                    
                    notifications.Add(new NotificationMessage
                    {
                        Type = NotificationType.Info,
                        Message = $"Semester '{nextSemester.SemesterName}' starts in {daysUntilStart} days. Ensure all preparations are complete."
                    });
                }

                // Check for high usage equipment
                var highUsageEquipment = await _context.SemesterEquipmentUsages
                    .Include(seu => seu.Equipment)
                    .Where(seu => seu.WeeklyUsageHours > 20)
                    .ToListAsync();

                if (highUsageEquipment.Any())
                {
                    notifications.Add(new NotificationMessage
                    {
                        Type = NotificationType.Warning,
                        Message = $"{highUsageEquipment.Count} equipment items have high weekly usage (>20 hours). Consider scheduling preventive maintenance."
                    });
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating notifications");
                notifications.Add(new NotificationMessage
                {
                    Type = NotificationType.Error,
                    Message = "Error generating system notifications."
                });
            }

            return notifications;
        }

        private async Task<List<EquipmentUsageSummary>> GenerateEquipmentUsageSummary(Semester semester)
        {
            var equipmentUsage = new List<EquipmentUsageSummary>();

            if (semester.SemesterEquipmentUsages == null || !semester.SemesterEquipmentUsages.Any())
            {
                return equipmentUsage;
            }

            // Get all equipment IDs at once
            var equipmentIds = semester.SemesterEquipmentUsages
                .Where(seu => seu.Equipment != null)
                .Select(seu => seu.EquipmentId)
                .ToList();

            // Get all maintenance logs for these equipment items in a single query
            var maintenanceLogs = await _context.MaintenanceLogs
                .Where(ml => equipmentIds.Contains(ml.EquipmentId))
                .GroupBy(ml => ml.EquipmentId)
                .Select(g => new { 
                    EquipmentId = g.Key, 
                    LastMaintenanceDate = g.OrderByDescending(ml => ml.LogDate).FirstOrDefault()!.LogDate 
                })
                .ToListAsync();

            // Create a lookup dictionary for O(1) access
            var maintenanceDict = maintenanceLogs.ToDictionary(ml => ml.EquipmentId, ml => ml.LastMaintenanceDate);

            foreach (var usage in semester.SemesterEquipmentUsages)
            {
                var equipment = usage.Equipment;
                if (equipment == null) continue;

                // Get last maintenance date from the dictionary (O(1) lookup)
                maintenanceDict.TryGetValue(equipment.EquipmentId, out var lastMaintenanceDate);

                equipmentUsage.Add(new EquipmentUsageSummary
                {
                    EquipmentId = equipment.EquipmentId,
                    EquipmentName = equipment.EquipmentModel?.ModelName ?? "Unknown",
                    EquipmentType = equipment.EquipmentType?.EquipmentTypeName ?? "Unknown",
                    Location = $"{equipment.Building?.BuildingName} - {equipment.Room?.RoomName}",
                    WeeklyHours = usage.WeeklyUsageHours,
                    TotalSemesterHours = usage.TotalSemesterHours,
                    UtilizationPercentage = CalculateUtilizationPercentage(usage.WeeklyUsageHours),
                    RiskLevel = CalculateRiskLevel(usage.WeeklyUsageHours, lastMaintenanceDate),
                    LastMaintenanceDate = lastMaintenanceDate,
                    NextMaintenanceDue = CalculateNextMaintenanceDue(lastMaintenanceDate, usage.WeeklyUsageHours)
                });
            }

            return equipmentUsage.OrderByDescending(eu => eu.WeeklyHours).ToList();
        }

        private List<WeeklyProgressItem> GenerateWeeklyProgress(Semester semester)
        {
            var weeklyProgress = new List<WeeklyProgressItem>();

            for (int week = 1; week <= semester.NumberOfWeeks; week++)
            {
                var weekStartDate = semester.StartDate.AddDays((week - 1) * 7);
                var weekEndDate = weekStartDate.AddDays(6);
                var isCompleted = weekEndDate < DateTime.Now;
                var isCurrent = weekStartDate <= DateTime.Now && DateTime.Now <= weekEndDate;

                weeklyProgress.Add(new WeeklyProgressItem
                {
                    WeekNumber = week,
                    WeekStartDate = weekStartDate,
                    WeekEndDate = weekEndDate,
                    IsCompleted = isCompleted,
                    IsCurrent = isCurrent,
                    TotalUsageHours = semester.SemesterEquipmentUsages?.Sum(seu => seu.WeeklyUsageHours) ?? 0,
                    ActiveEquipmentCount = semester.SemesterEquipmentUsages?.Count ?? 0,
                    Status = isCompleted ? "Completed" : isCurrent ? "Current" : "Upcoming"
                });
            }

            return weeklyProgress;
        }

        private async Task<SemesterStatistics> CalculateSemesterStatistics(Semester semester)
        {
            var statistics = new SemesterStatistics();

            statistics.TotalUsageHours = semester.TotalEquipmentHours;
            statistics.AverageWeeklyUsage = semester.NumberOfWeeks > 0 ? semester.TotalEquipmentHours / semester.NumberOfWeeks : 0;
            statistics.TotalEquipmentUsed = semester.SemesterEquipmentUsages?.Count ?? 0;
            statistics.HighUsageEquipmentCount = semester.SemesterEquipmentUsages?.Count(seu => seu.WeeklyUsageHours > 40) ?? 0;
            
            // Calculate predicted maintenance cost based on usage
            statistics.PredictedMaintenanceCost = (double)(semester.TotalEquipmentHours * 2.5); // $2.5 per hour estimate
            
            // Count maintenance tasks generated for this semester's equipment
            var equipmentIds = semester.SemesterEquipmentUsages?.Select(seu => seu.EquipmentId).ToList() ?? new List<int>();
            statistics.MaintenanceTasksGenerated = await _context.MaintenanceTasks
                .Where(mt => equipmentIds.Contains(mt.EquipmentId))
                .CountAsync();

            // Calculate efficiency score (simplified)
            statistics.EfficiencyScore = Math.Min(100, (statistics.TotalUsageHours / (statistics.TotalEquipmentUsed * 40 * semester.NumberOfWeeks)) * 100);

            return statistics;
        }

        private async Task<List<FEENALOoFINALE.ViewModels.MaintenanceRecommendation>> GenerateMaintenanceRecommendations(Semester semester)
        {
            var recommendations = new List<FEENALOoFINALE.ViewModels.MaintenanceRecommendation>();
            var highUsageEquipment = semester.SemesterEquipmentUsages?
                .Where(seu => seu.WeeklyUsageHours > 40)
                .ToList() ?? new List<SemesterEquipmentUsage>();

            if (!highUsageEquipment.Any()) return recommendations;

            // Get all equipment IDs for high usage equipment
            var equipmentIds = highUsageEquipment.Select(seu => seu.EquipmentId).ToList();

            // Get all maintenance logs for these equipment items in a single query
            var maintenanceLogs = await _context.MaintenanceLogs
                .Where(ml => equipmentIds.Contains(ml.EquipmentId))
                .GroupBy(ml => ml.EquipmentId)
                .Select(g => new { 
                    EquipmentId = g.Key, 
                    LastMaintenanceDate = g.OrderByDescending(ml => ml.LogDate).FirstOrDefault()!.LogDate 
                })
                .ToListAsync();

            // Create a lookup dictionary for O(1) access
            var maintenanceDict = maintenanceLogs.ToDictionary(ml => ml.EquipmentId, ml => ml.LastMaintenanceDate);

            foreach (var usage in highUsageEquipment)
            {
                // Get last maintenance date from the dictionary (O(1) lookup)
                var hasLastMaintenance = maintenanceDict.TryGetValue(usage.EquipmentId, out var lastMaintenanceDate);
                
                var daysSinceLastMaintenance = hasLastMaintenance
                    ? (DateTime.Now - lastMaintenanceDate).TotalDays 
                    : 365;

                if (daysSinceLastMaintenance > 90) // More than 3 months
                {
                    recommendations.Add(new FEENALOoFINALE.ViewModels.MaintenanceRecommendation
                    {
                        EquipmentId = usage.EquipmentId,
                        EquipmentName = usage.Equipment?.EquipmentModel?.ModelName ?? "Unknown",
                        RecommendationType = "Preventive Maintenance",
                        Priority = daysSinceLastMaintenance > 180 ? "High" : "Medium",
                        Urgency = daysSinceLastMaintenance > 180 ? "Immediate" : "Within 30 days",
                        EstimatedCost = CalculateMaintenanceCost(usage.WeeklyUsageHours),
                        Recommendations = new List<string>
                        {
                            $"Equipment has high usage ({usage.WeeklyUsageHours:F1} hours/week)",
                            $"Last maintenance was {daysSinceLastMaintenance:F0} days ago",
                            "Schedule preventive maintenance to avoid equipment failure"
                        }
                    });
                }
            }

            return recommendations;
        }

        // File handling methods
        private async Task<FileUploadResult> SaveTimetableFile(IFormFile file, Semester semester)
        {
            try
            {
                // Validate file
                if (file == null)
                {
                    return new FileUploadResult { Success = false, ErrorMessage = "No file was provided." };
                }

                if (file.Length == 0)
                {
                    return new FileUploadResult { Success = false, ErrorMessage = "The uploaded file is empty." };
                }

                if (file.Length > 10 * 1024 * 1024) // 10MB limit
                {
                    return new FileUploadResult { Success = false, ErrorMessage = "File size cannot exceed 10MB." };
                }

                if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return new FileUploadResult { Success = false, ErrorMessage = "Only PDF files are allowed." };
                }

                // Create upload directory
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "timetables");
                
                try
                {
                    if (!Directory.Exists(uploadsPath))
                    {
                        Directory.CreateDirectory(uploadsPath);
                        _logger.LogInformation("Created uploads directory: {Path}", uploadsPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create uploads directory: {Path}", uploadsPath);
                    return new FileUploadResult { Success = false, ErrorMessage = "Failed to create upload directory. Please contact administrator." };
                }

                // Generate unique filename
                var fileName = $"{semester.SemesterId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // Save file
                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    
                    _logger.LogInformation("Successfully saved file: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save file: {FilePath}", filePath);
                    return new FileUploadResult { Success = false, ErrorMessage = "Failed to save file. Please try again." };
                }

                return new FileUploadResult
                {
                    Success = true,
                    FilePath = Path.Combine("uploads", "timetables", fileName),
                    OriginalFileName = file.FileName,
                    FileSizeBytes = file.Length
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving timetable file: {Message}", ex.Message);
                return new FileUploadResult { Success = false, ErrorMessage = $"Error saving file: {ex.Message}" };
            }
        }

        private void DeleteTimetableFile(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath);
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting timetable file: {FilePath}", filePath);
            }
        }

        private async Task ProcessTimetableFile(int semesterId)
        {
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(10)); // 10-minute timeout
            try
            {
                _logger.LogInformation("Starting ProcessTimetableFile for semester {SemesterId}", semesterId);
                
                var semester = await _context.Semesters.FindAsync(semesterId, cancellationTokenSource.Token);
                if (semester == null || string.IsNullOrEmpty(semester.TimetableFilePath))
                {
                    _logger.LogWarning("Semester not found or no file path for semester {SemesterId}", semesterId);
                    return;
                }

                _logger.LogInformation("Setting processing status for semester {SemesterId}", semesterId);
                semester.ProcessingStatus = SemesterProcessingStatus.Processing;
                semester.ProcessingMessage = "Processing timetable file...";
                semester.LastModified = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationTokenSource.Token);

                // Extract text from PDF
                _logger.LogInformation("Extracting text from PDF for semester {SemesterId}", semesterId);
                var filePath = Path.Combine(_environment.WebRootPath, semester.TimetableFilePath);
                
                if (!System.IO.File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Timetable file not found: {filePath}");
                }
                
                var timetableText = ExtractTextFromPdf(filePath);
                _logger.LogInformation("Extracted {TextLength} characters from PDF for semester {SemesterId}", 
                    timetableText?.Length ?? 0, semesterId);

                if (string.IsNullOrEmpty(timetableText))
                {
                    throw new InvalidOperationException("No text could be extracted from the PDF file");
                }

                // Parse timetable text
                _logger.LogInformation("Parsing timetable text for semester {SemesterId}", semesterId);
                var equipmentUsage = await ParseTimetableText(timetableText, semester.NumberOfWeeks);
                _logger.LogInformation("Parsed {EquipmentCount} equipment items for semester {SemesterId}", 
                    equipmentUsage.Count, semesterId);

                // Clear existing usage data
                _logger.LogInformation("Clearing existing usage data for semester {SemesterId}", semesterId);
                var existingUsage = await _context.SemesterEquipmentUsages
                    .Where(seu => seu.SemesterId == semesterId)
                    .ToListAsync();
                _context.SemesterEquipmentUsages.RemoveRange(existingUsage);

                // Save new usage data
                _logger.LogInformation("Saving new usage data for semester {SemesterId}", semesterId);
                var totalHours = 0.0;
                foreach (var usage in equipmentUsage)
                {
                    if (int.TryParse(usage.Key, out int equipmentId))
                    {
                        var semesterUsage = new SemesterEquipmentUsage
                        {
                            SemesterId = semesterId,
                            EquipmentId = equipmentId,
                            WeeklyUsageHours = usage.Value,
                            LastUpdated = DateTime.UtcNow
                        };

                        _context.SemesterEquipmentUsages.Add(semesterUsage);
                        totalHours += usage.Value * semester.NumberOfWeeks;
                    }
                }

                semester.TotalEquipmentHours = totalHours;
                semester.ProcessingStatus = SemesterProcessingStatus.Completed;
                semester.ProcessingMessage = $"Successfully processed {equipmentUsage.Count} equipment items.";
                semester.EquipmentUsageDataJson = JsonSerializer.Serialize(equipmentUsage);
                semester.LastModified = DateTime.UtcNow;

                _logger.LogInformation("Saving final results for semester {SemesterId}", semesterId);
                await _context.SaveChangesAsync(cancellationTokenSource.Token);
                _logger.LogInformation("Successfully completed processing for semester {SemesterId}", semesterId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timetable processing for semester {SemesterId} was cancelled due to timeout", semesterId);
                
                try
                {
                    var semester = await _context.Semesters.FindAsync(semesterId);
                    if (semester != null)
                    {
                        semester.ProcessingStatus = SemesterProcessingStatus.Failed;
                        semester.ProcessingMessage = "Processing failed: Operation timed out after 10 minutes.";
                        semester.LastModified = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to update semester {SemesterId} status after timeout", semesterId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing timetable file for semester {SemesterId}: {ErrorMessage}", 
                    semesterId, ex.Message);
                
                try
                {
                    var semester = await _context.Semesters.FindAsync(semesterId);
                    if (semester != null)
                    {
                        semester.ProcessingStatus = SemesterProcessingStatus.Failed;
                        semester.ProcessingMessage = $"Processing failed: {ex.Message}";
                        semester.LastModified = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Updated semester {SemesterId} status to Failed", semesterId);
                    }
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to update semester {SemesterId} status after processing error", semesterId);
                }
            }
            finally
            {
                cancellationTokenSource?.Dispose();
            }
        }

        private string ExtractTextFromPdf(string filePath)
        {
            using (var pdfDocument = new PdfDocument(new PdfReader(filePath)))
            {
                var text = new StringBuilder();
                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    var page = pdfDocument.GetPage(i);
                    var strategy = new SimpleTextExtractionStrategy();
                    text.Append(PdfTextExtractor.GetTextFromPage(page, strategy));
                }
                return text.ToString();
            }
        }

        private async Task<Dictionary<string, double>> ParseTimetableText(string text, int semesterWeeks)
        {
            var roomWeeklyUsage = new Dictionary<string, double>();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Regex pattern to match room schedules (e.g., "PB001 Mon 09:00-11:00")
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

            // Map room usage to equipment using async query
            var equipmentUsage = new Dictionary<string, double>();
            var allEquipmentInRooms = await _context.Equipment
                .Where(e => e.Room != null && roomWeeklyUsage.Keys.Contains(e.Room.RoomName.ToUpper()))
                .Include(e => e.Room)
                .ToListAsync();

            foreach (var equipment in allEquipmentInRooms)
            {
                if (equipment.Room != null && roomWeeklyUsage.TryGetValue(equipment.Room.RoomName.ToUpper(), out var weeklyHours))
                {
                    equipmentUsage[equipment.EquipmentId.ToString()] = weeklyHours;
                }
            }

            return equipmentUsage;
        }

        // Calculation helper methods
        private double CalculateUtilizationPercentage(double weeklyHours)
        {
            // Assume maximum 40 hours per week for 100% utilization
            return Math.Min(100, (weeklyHours / 40.0) * 100);
        }

        private string CalculateRiskLevel(double weeklyHours, DateTime? lastMaintenanceDate)
        {
            var daysSinceMaintenance = lastMaintenanceDate.HasValue
                ? (DateTime.Now - lastMaintenanceDate.Value).TotalDays
                : 365;

            if (weeklyHours > 40 && daysSinceMaintenance > 180)
                return "High";
            else if (weeklyHours > 20 && daysSinceMaintenance > 90)
                return "Medium";
            else
                return "Low";
        }

        private DateTime? CalculateNextMaintenanceDue(DateTime? lastMaintenanceDate, double weeklyHours)
        {
            if (!lastMaintenanceDate.HasValue)
                return DateTime.Now.AddDays(30); // Default to 30 days if no previous maintenance

            // Calculate interval based on usage intensity
            var intervalDays = weeklyHours switch
            {
                > 40 => 60,  // High usage: 2 months
                > 20 => 90,  // Medium usage: 3 months
                _ => 120     // Low usage: 4 months
            };

            return lastMaintenanceDate.Value.AddDays(intervalDays);
        }

        private decimal CalculateMaintenanceCost(double weeklyHours)
        {
            // Base cost calculation based on usage intensity
            return weeklyHours switch
            {
                > 40 => 300m,  // High usage equipment
                > 20 => 200m,  // Medium usage equipment
                _ => 150m      // Low usage equipment
            };
        }

        private List<string> GetProcessingLogs(Semester semester)
        {
            var logs = new List<string>();
            
            logs.Add($"Semester created: {semester.CreatedDate:yyyy-MM-dd HH:mm}");
            logs.Add($"Processing status: {semester.ProcessingStatus}");
            
            if (!string.IsNullOrEmpty(semester.ProcessingMessage))
            {
                logs.Add($"Message: {semester.ProcessingMessage}");
            }
            
            logs.Add($"Total equipment hours: {semester.TotalEquipmentHours:F2}");
            logs.Add($"Equipment tracked: {semester.SemesterEquipmentUsages?.Count ?? 0}");
            
            return logs;
        }

        private async Task<TimetableFilterOptions> BuildFilterOptions()
        {
            var filterOptions = new TimetableFilterOptions();

            // Status options
            filterOptions.StatusOptions = Enum.GetValues<SemesterStatus>()
                .Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() })
                .ToList();

            // Processing status options
            filterOptions.ProcessingStatusOptions = Enum.GetValues<SemesterProcessingStatus>()
                .Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() })
                .ToList();

            // Year options
            var years = await _context.Semesters
                .Select(s => s.StartDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            filterOptions.YearOptions = years
                .Select(y => new SelectListItem { Value = y.ToString(), Text = y.ToString() })
                .ToList();

            return filterOptions;
        }

        // Enhanced semester completion detection
        private async Task CheckAndUpdateSemesterCompletion(Semester semester)
        {
            try
            {
                var now = DateTime.Now;
                var endDate = semester.StartDate.AddDays(semester.NumberOfWeeks * 7);

                // Check if semester has ended
                if (now > endDate && semester.IsActive)
                {
                    semester.IsActive = false;
                    semester.LastModified = DateTime.UtcNow;
                    
                    // Add completion note
                    semester.ProcessingMessage = $"Semester automatically completed on {now:MMM dd, yyyy}. Total duration: {semester.NumberOfWeeks} weeks.";
                    
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Semester {SemesterId} automatically completed", semester.SemesterId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking semester completion for semester {SemesterId}", semester.SemesterId);
            }
        }

        // GET: Confirm Delete Semester
        public async Task<IActionResult> ConfirmDelete(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to load semester for deletion confirmation. ID: {SemesterId}", id);
                
                var semester = await _context.Semesters
                    .Include(s => s.UploadedBy)
                    .Include(s => s.SemesterEquipmentUsages)
                    .FirstOrDefaultAsync(s => s.SemesterId == id);

                if (semester == null)
                {
                    _logger.LogWarning("Semester with ID {SemesterId} not found for deletion", id);
                    TempData["ErrorMessage"] = "Semester not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Calculate delete implications
                var equipmentUsageCount = semester.SemesterEquipmentUsages?.Count ?? 0;
                var maintenanceTaskCount = 0;
                var hasMaintenanceData = false;

                if (semester.SemesterEquipmentUsages != null && semester.SemesterEquipmentUsages.Any())
                {
                    var equipmentIds = semester.SemesterEquipmentUsages.Select(seu => seu.EquipmentId).ToList();
                    
                    // Check maintenance tasks
                    maintenanceTaskCount = await _context.MaintenanceTasks
                        .Where(mt => equipmentIds.Contains(mt.EquipmentId))
                        .CountAsync();

                    // Check maintenance logs
                    hasMaintenanceData = await _context.MaintenanceLogs
                        .AnyAsync(ml => equipmentIds.Contains(ml.EquipmentId) &&
                                       ml.LogDate >= semester.StartDate && 
                                       ml.LogDate <= semester.StartDate.AddDays(semester.NumberOfWeeks * 7));
                }

                var viewModel = new SemesterDeleteViewModel
                {
                    SemesterId = semester.SemesterId,
                    SemesterName = semester.SemesterName,
                    StartDate = semester.StartDate,
                    EndDate = semester.StartDate.AddDays(semester.NumberOfWeeks * 7),
                    IsActive = semester.IsActive,
                    EquipmentUsageCount = equipmentUsageCount,
                    MaintenanceTaskCount = maintenanceTaskCount,
                    TotalUsageHours = semester.TotalEquipmentHours,
                    UploadedByName = semester.UploadedBy?.UserName,
                    UploadDate = semester.UploadDate
                };

                // Generate warnings and check if deletion is allowed
                var warnings = new List<string>();
                var canDelete = true;
                string? blockReason = null;

                // Allow deletion of active semesters, but warn the user
                if (semester.IsActive)
                {
                    warnings.Add("This semester is currently active. Deleting it will immediately deactivate all related processes.");
                }

                if (equipmentUsageCount > 0)
                {
                    warnings.Add($"This will delete {equipmentUsageCount} equipment usage records.");
                }

                if (maintenanceTaskCount > 0)
                {
                    warnings.Add($"This action may affect {maintenanceTaskCount} related maintenance tasks.");
                }

                if (hasMaintenanceData)
                {
                    warnings.Add("This semester has associated maintenance history that may be affected.");
                }

                if (semester.TotalEquipmentHours > 0)
                {
                    warnings.Add($"This will permanently remove {semester.TotalEquipmentHours:F1} hours of equipment usage data.");
                }

                if (warnings.Count == 0)
                {
                    warnings.Add("This semester has minimal data and can be safely deleted.");
                }

                viewModel.DeletionWarnings = warnings;
                viewModel.CanDelete = canDelete;
                viewModel.DeletionBlockReason = blockReason;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading semester deletion confirmation for ID: {SemesterId}", id);
                TempData["ErrorMessage"] = "Error loading semester details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // API endpoint to check processing status
        [HttpGet]
        public async Task<IActionResult> CheckProcessingStatus(int semesterId)
        {
            try
            {
                var semester = await _context.Semesters
                    .Select(s => new {
                        s.SemesterId,
                        s.SemesterName,
                        s.ProcessingStatus,
                        s.ProcessingMessage,
                        s.LastModified
                    })
                    .FirstOrDefaultAsync(s => s.SemesterId == semesterId);

                if (semester == null)
                {
                    return NotFound(new { error = "Semester not found" });
                }

                return Json(new
                {
                    semesterId = semester.SemesterId,
                    semesterName = semester.SemesterName,
                    status = semester.ProcessingStatus.ToString(),
                    message = semester.ProcessingMessage,
                    lastModified = semester.LastModified,
                    isProcessing = semester.ProcessingStatus == SemesterProcessingStatus.Processing,
                    isCompleted = semester.ProcessingStatus == SemesterProcessingStatus.Completed,
                    isFailed = semester.ProcessingStatus == SemesterProcessingStatus.Failed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking processing status for semester {SemesterId}", semesterId);
                return StatusCode(500, new { error = "An error occurred while checking processing status" });
            }
        }

    }

    // Helper classes
    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? OriginalFileName { get; set; }
        public long FileSizeBytes { get; set; }
        public string? ErrorMessage { get; set; }
    }
}