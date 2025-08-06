using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FEENALOoFINALE.Data;
using FEENALOoFINALE.Models;
using FEENALOoFINALE.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using FEENALOoFINALE.Services;
using System.Security.Cryptography;
using System.Text;

namespace FEENALOoFINALE.Controllers
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly IEmailService _emailService;
        private readonly ILogger<UserController> _logger;
        private readonly IConfiguration _configuration;

        public UserController(
            ApplicationDbContext context, 
            SignInManager<User> signInManager, 
            UserManager<User> userManager,
            IEmailService emailService,
            ILogger<UserController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _signInManager = signInManager;
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
        }

        // GET: User (Protected)
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(users);
        }

        // Enhanced User Management with Advanced Features
        [Authorize]
        public async Task<IActionResult> Enhanced(
            int page = 1,
            int pageSize = 20,
            string searchTerm = "",
            string[]? statusFilter = null,
            string sortBy = "FullName",
            string sortDirection = "asc")
        {
            var viewModel = new UserManagementViewModel
            {
                PageTitle = "User Management",
                CurrentPage = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDirection = sortDirection,
                CanCreateUsers = true,
                CanManageRoles = true,
                CanExport = true
            };

            // Build query
            var query = _context.Users.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(u => 
                    u.FullName.Contains(searchTerm) ||
                    (u.Email != null && u.Email.Contains(searchTerm)) ||
                    (u.UserName != null && u.UserName.Contains(searchTerm)) ||
                    u.WorkerId.Contains(searchTerm));
            }

            // Apply status filter
            if (statusFilter != null && statusFilter.Any())
            {
                if (statusFilter.Contains("Active"))
                    query = query.Where(u => u.EmailConfirmed && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow));
                if (statusFilter.Contains("Unverified"))
                    query = query.Where(u => !u.EmailConfirmed);
                if (statusFilter.Contains("Locked"))
                    query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
            }

            // Apply sorting
            query = sortBy.ToLower() switch
            {
                "fullname" => sortDirection == "desc" ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
                "email" => sortDirection == "desc" ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
                "workerid" => sortDirection == "desc" ? query.OrderByDescending(u => u.WorkerId) : query.OrderBy(u => u.WorkerId),
                "lastlogin" => sortDirection == "desc" ? query.OrderByDescending(u => u.LastLogin) : query.OrderBy(u => u.LastLogin),
                _ => query.OrderBy(u => u.FullName)
            };

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserItemViewModel
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "",
                    Email = u.Email ?? "",
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    PhoneNumber = u.PhoneNumber ?? "",
                    EmailConfirmed = u.EmailConfirmed,
                    IsActive = u.EmailConfirmed && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow),
                    LastLoginDate = u.LastLogin == DateTime.MinValue ? null : u.LastLogin,
                    CreatedDate = DateTime.UtcNow // Use current date as fallback since User model doesn't have CreatedDate
                })
                .ToListAsync();

            // Calculate statistics
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.EmailConfirmed && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow));
            var unverifiedUsers = await _context.Users.CountAsync(u => !u.EmailConfirmed);
            var lockedUsers = await _context.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);

            viewModel.Statistics = new UserStatistics
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                InactiveUsers = totalUsers - activeUsers,
                UnconfirmedUsers = unverifiedUsers,
                UsersLoggedInToday = 0, // Would need additional tracking
                NewUsersThisMonth = 0  // Would need CreatedDate field in User model
            };

            // Set up pagination
            viewModel.TotalRecords = totalCount;

            viewModel.Users = users;

            // Set up filter options
            viewModel.FilterOptions = new UserFilterOptions
            {
                Statuses = new List<UserStatusOption>
                {
                    new UserStatusOption { Status = "Active", Name = "Active", Count = activeUsers },
                    new UserStatusOption { Status = "Unverified", Name = "Unverified", Count = unverifiedUsers },
                    new UserStatusOption { Status = "Locked", Name = "Locked", Count = lockedUsers }
                }
            };

            return View(viewModel);
        }

        // GET: Profile for current logged-in user (Protected)
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            if (User?.Identity?.Name == null)
            {
                return RedirectToAction("Login", "User");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Logout (Protected)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "User");
        }

        // GET: Login (Public)
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        // POST: Login (Public)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Username and password are required.");
                return View();
            }

            // Find user by username or email
            var user = await _userManager.FindByNameAsync(username) ?? 
                      await _userManager.FindByEmailAsync(username);

            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            
            if (user != null && !string.IsNullOrEmpty(user.UserName))
            {
                _logger.LogInformation("User found: {Email}, IsEmailVerified: {IsVerified}", user.Email, user.IsEmailVerified);
                
                // Check if email is verified (skip in development if email service isn't configured)
                var emailConfigured = !string.IsNullOrEmpty(_configuration["EmailSettings:SenderEmail"]) &&
                                     !_configuration["EmailSettings:SenderEmail"]!.Contains("your-email") &&
                                     !_configuration["EmailSettings:SenderEmail"]!.Contains("demo") &&
                                     !string.IsNullOrEmpty(_configuration["EmailSettings:Password"]) &&
                                     _configuration["EmailSettings:Password"] != "demo-password";

                // Use SignInManager for proper authentication
                var result = await _signInManager.PasswordSignInAsync(user.UserName, password, false, false);
                
                if (result.Succeeded)
                {
                    // Check if email verification is required
                    if (!user.IsEmailVerified && (!isDevelopment || emailConfigured))
                    {
                        await _signInManager.SignOutAsync(); // Sign out immediately if not verified
                        ModelState.AddModelError("", "Please verify your email address before logging in. Check your email for verification link.");
                        ViewBag.ShowResendVerification = true;
                        ViewBag.Email = user.Email;
                        return View();
                    }
                    else if (!user.IsEmailVerified && isDevelopment && !emailConfigured)
                    {
                        // In development with no email configured, automatically verify the user
                        user.IsEmailVerified = true;
                        user.EmailVerificationToken = null;
                        user.EmailVerificationTokenExpires = null;
                        await _userManager.UpdateAsync(user);
                        _logger.LogInformation("Auto-verified user {Email} in development environment", user.Email);
                    }
                    
                    // Update last login time
                    user.LastLogin = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                    
                    // Redirect to dashboard or home page after successful login
                    return RedirectToAction("Index", "Dashboard");
                }
                else
                {
                    // Password is wrong, but if user exists and is unverified in development, show bypass option
                    if (!user.IsEmailVerified && isDevelopment)
                    {
                        ModelState.AddModelError("", "Invalid username or password. Your account may not be verified yet.");
                        ViewBag.ShowResendVerification = true;
                        ViewBag.Email = user.Email;
                        return View();
                    }
                }
            }

            ModelState.AddModelError("", "Invalid username or password.");
            return View();
        }

        // GET: User/Details/5 (Protected)
        [Authorize]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _context.Users.FirstOrDefaultAsync(m => m.Id == id);

            if (user == null)
                return NotFound();

            return View(user);
        }

        // GET: User/Create (Public)
        [AllowAnonymous]
        public IActionResult Create()
        {
            return View(new RegisterViewModel());
        }

        // POST: User/Create (Public)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Validate email format
                    if (!_emailService.IsValidEmail(model.Email))
                    {
                        ModelState.AddModelError("Email", "Please enter a valid email address.");
                        return View(model);
                    }

                    // Check if email already exists
                    var existingUserByEmail = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUserByEmail != null)
                    {
                        ModelState.AddModelError("Email", "An account with this email address already exists.");
                        return View(model);
                    }

                    // Check if username already exists
                    var existingUserByUsername = await _userManager.FindByNameAsync(model.UserName);
                    if (existingUserByUsername != null)
                    {
                        ModelState.AddModelError("UserName", "This username is already taken.");
                        return View(model);
                    }

                    // Check if Worker ID already exists
                    var existingUserByWorkerId = await _context.Users
                        .FirstOrDefaultAsync(u => u.WorkerId == model.WorkerId);
                    if (existingUserByWorkerId != null)
                    {
                        ModelState.AddModelError("WorkerId", "This Worker ID is already registered.");
                        return View(model);
                    }

                    // Create the user
                    var user = new User
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        WorkerId = model.WorkerId,
                        UserName = model.UserName,
                        Email = model.Email,
                        // ContactNumber = model.ContactNumber,
                        IsEmailVerified = false,
                        EmailVerificationToken = GenerateEmailVerificationToken(),
                        EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24),
                        LastLogin = DateTime.UtcNow
                    };

                    var result = await _userManager.CreateAsync(user, model.Password);

                    if (result.Succeeded)
                    {
                        try
                        {
                            // Send verification email
                            var verificationUrl = Url.Action("VerifyEmail", "User", 
                                new { userId = user.Id, token = user.EmailVerificationToken }, 
                                Request.Scheme);

                            await _emailService.SendEmailVerificationAsync(user.Email!, verificationUrl!);

                            // Check if we're in development environment
                            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                            
                            if (isDevelopment)
                            {
                                TempData["SuccessMessage"] = "Account created successfully! In development mode, email verification is simulated. You can use the bypass verification feature on the login page or check the console logs.";
                            }
                            else
                            {
                                TempData["SuccessMessage"] = "Account created successfully! Please check your email to verify your account before logging in.";
                            }
                            
                            return RedirectToAction("Login", "User");
                        }
                        catch (Exception ex)
                        {
                            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                            
                            if (isDevelopment)
                            {
                                _logger.LogInformation(ex, "Email service in development mode for {Email}", user.Email);
                                TempData["SuccessMessage"] = "Account created successfully! In development mode, you can use the bypass verification feature on the login page.";
                            }
                            else
                            {
                                _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
                                TempData["WarningMessage"] = "Account created successfully, but we couldn't send the verification email. Please contact support.";
                            }
                            
                            return RedirectToAction("Login", "User");
                        }
                    }

                    // Add Identity errors to ModelState
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating user account for {Email}", model.Email);
                    ModelState.AddModelError("", "An error occurred while creating your account. Please try again.");
                }
            }

            return View(model);
        }

        // GET: User/Edit/5 (Protected)
        [Authorize]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            return View(user);
        }

        // POST: User/Edit/5 (Protected)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,UserName,Email,FirstName,LastName,WorkerId,PhoneNumber,ContactNumber")] User user)
        {
            if (id != user.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the existing user from UserManager to ensure proper Identity handling
                    var existingUser = await _userManager.FindByIdAsync(id);
                    if (existingUser == null)
                        return NotFound();

                    // Update the basic properties
                    existingUser.FirstName = user.FirstName;
                    existingUser.LastName = user.LastName;
                    existingUser.WorkerId = user.WorkerId;
                    existingUser.PhoneNumber = user.PhoneNumber;
                    // existingUser.ContactNumber = user.ContactNumber;

                    // Update username if changed
                    if (existingUser.UserName != user.UserName)
                    {
                        var setUserNameResult = await _userManager.SetUserNameAsync(existingUser, user.UserName);
                        if (!setUserNameResult.Succeeded)
                        {
                            foreach (var error in setUserNameResult.Errors)
                            {
                                ModelState.AddModelError("", error.Description);
                            }
                            return View(user);
                        }
                    }

                    // Update email if changed
                    if (existingUser.Email != user.Email)
                    {
                        var setEmailResult = await _userManager.SetEmailAsync(existingUser, user.Email);
                        if (!setEmailResult.Succeeded)
                        {
                            foreach (var error in setEmailResult.Errors)
                            {
                                ModelState.AddModelError("", error.Description);
                            }
                            return View(user);
                        }
                    }

                    // Update the user through UserManager
                    var updateResult = await _userManager.UpdateAsync(existingUser);
                    if (updateResult.Succeeded)
                    {
                        return RedirectToAction(nameof(Profile));
                    }
                    else
                    {
                        foreach (var error in updateResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                        return NotFound();

                    throw;
                }
            }

            return View(user);
        }

        // GET: User/Delete/5 (Protected)
        [Authorize]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _context.Users.FirstOrDefaultAsync(m => m.Id == id);

            if (user == null)
                return NotFound();

            return View(user);
        }

        // POST: User/DeleteConfirmed/5 (Protected)
        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: User/Settings (Protected)
        [Authorize]
        public async Task<IActionResult> Settings()
        {
            if (User?.Identity?.Name == null)
            {
                return RedirectToAction("Login", "User");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: User/Settings (Protected)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(User model, string? currentPassword, string? newPassword, string? confirmPassword)
        {
            if (User?.Identity?.Name == null)
            {
                return RedirectToAction("Login", "User");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            if (user == null)
            {
                return NotFound();
            }

            try
            {
                // Update basic profile information
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;
                user.PhoneNumber = model.PhoneNumber;
                // user.ContactNumber = model.ContactNumber;
                user.WorkerId = model.WorkerId;

                // Handle password change if provided
                if (!string.IsNullOrEmpty(newPassword))
                {
                    if (string.IsNullOrEmpty(currentPassword))
                    {
                        ModelState.AddModelError("", "Current password is required to set a new password.");
                        return View(user);
                    }

                    if (newPassword != confirmPassword)
                    {
                        ModelState.AddModelError("", "New password and confirmation password do not match.");
                        return View(user);
                    }

                    // Verify current password
                    var passwordCheck = await _userManager.CheckPasswordAsync(user, currentPassword);
                    if (!passwordCheck)
                    {
                        ModelState.AddModelError("", "Current password is incorrect.");
                        return View(user);
                    }

                    // Change password
                    var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        return View(user);
                    }
                }

                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Account settings updated successfully!";
                return RedirectToAction(nameof(Settings));
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while updating your settings. Please try again.");
                return View(user);
            }
        }

        private bool UserExists(string id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        // Email verification methods
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Invalid verification link.";
                return RedirectToAction("Login");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            if (user.EmailVerificationToken != token)
            {
                TempData["ErrorMessage"] = "Invalid verification token.";
                return RedirectToAction("Login");
            }

            if (user.EmailVerificationTokenExpires < DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "Verification link has expired. Please request a new one.";
                return RedirectToAction("Login");
            }

            // Mark email as verified
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpires = null;
            user.EmailConfirmed = true; // Identity property

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Email verified successfully! You can now log in.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to verify email. Please try again.";
            }

            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public async Task<IActionResult> ResendVerification(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Email address is required.";
                return RedirectToAction("Login");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            if (user.IsEmailVerified)
            {
                TempData["InfoMessage"] = "Email is already verified.";
                return RedirectToAction("Login");
            }

            // Generate new verification token
            user.EmailVerificationToken = GenerateEmailVerificationToken();
            user.EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24);

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                try
                {
                    var verificationUrl = Url.Action("VerifyEmail", "User",
                        new { userId = user.Id, token = user.EmailVerificationToken },
                        Request.Scheme);

                    await _emailService.SendEmailVerificationAsync(user.Email!, verificationUrl!);
                    TempData["SuccessMessage"] = "Verification email sent successfully!";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
                    TempData["ErrorMessage"] = "Failed to send verification email. Please try again.";
                }
            }

            return RedirectToAction("Login");
        }

        private string GenerateEmailVerificationToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        // API endpoint to check if Worker ID exists (for client-side validation)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> CheckWorkerIdExists(string workerId)
        {
            if (string.IsNullOrEmpty(workerId))
            {
                return Json(new { exists = false });
            }

            var exists = await _context.Users.AnyAsync(u => u.WorkerId == workerId);
            return Json(new { exists });
        }

        // API endpoint to check if email exists and validate format (for client-side validation)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> CheckEmailExists(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return Json(new { exists = false, isValid = false, message = "Email is required" });
            }

            // First validate email format
            bool isValidFormat = _emailService.IsValidEmail(email);
            if (!isValidFormat)
            {
                return Json(new { exists = false, isValid = false, message = "Invalid email format" });
            }

            // Then check if email already exists
            var user = await _userManager.FindByEmailAsync(email);
            bool emailExists = user != null;

            if (emailExists)
            {
                return Json(new { exists = true, isValid = true, message = "This email is already registered" });
            }

            return Json(new { exists = false, isValid = true, message = "Email is available" });
        }

        // API endpoint to check if username exists (for client-side validation)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> CheckUsernameExists(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return Json(new { exists = false, isValid = false, message = "Username is required" });
            }

            if (username.Length < 3)
            {
                return Json(new { exists = false, isValid = false, message = "Username must be at least 3 characters long" });
            }

            if (username.Length > 50)
            {
                return Json(new { exists = false, isValid = false, message = "Username must not exceed 50 characters" });
            }

            // Check for valid characters (alphanumeric, underscore, dash)
            if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_-]+$"))
            {
                return Json(new { exists = false, isValid = false, message = "Username can only contain letters, numbers, underscores, and dashes" });
            }

            var user = await _userManager.FindByNameAsync(username);
            bool usernameExists = user != null;

            if (usernameExists)
            {
                return Json(new { exists = true, isValid = true, message = "This username is already taken" });
            }

            return Json(new { exists = false, isValid = true, message = "Username is available" });
        }

        // Development only: Bypass email verification
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> BypassEmailVerification(string email)
        {
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            if (!isDevelopment)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Email parameter is required.";
                return RedirectToAction("Login");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Verify the user
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpires = null;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = $"Email verification bypassed for {email}. You can now log in.";
            _logger.LogInformation("Email verification bypassed for {Email} in development environment", email);

            return RedirectToAction("Login");
        }

        // Temporary debug endpoint to check user status (Development only)
        public async Task<IActionResult> DebugUsers()
        {
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
            {
                return NotFound();
            }

            var users = await _context.Users.ToListAsync();
            var userInfo = users.Select(u => new {
                u.Id,
                u.UserName,
                u.Email,
                u.WorkerId,
                u.IsEmailVerified,
                u.EmailVerificationToken,
                u.EmailVerificationTokenExpires,
                HasPassword = !string.IsNullOrEmpty(u.PasswordHash),
                u.LastLogin
            }).ToList();

            return Json(userInfo);
        }

        // Development only - direct login bypass (after password verification)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DevLogin(string username, string password)
        {
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                TempData["ErrorMessage"] = "Username and password are required.";
                return RedirectToAction("Login");
            }

            try
            {
                // Find user by username or email
                var user = await _userManager.FindByNameAsync(username) ?? 
                          await _userManager.FindByEmailAsync(username);

                if (user != null)
                {
                    _logger.LogInformation("Dev Login - User found: {Email}, IsEmailVerified: {IsVerified}", user.Email, user.IsEmailVerified);
                    
                    // Check password
                    var passwordValid = await _userManager.CheckPasswordAsync(user, password);
                    _logger.LogInformation("Dev Login - Password valid: {PasswordValid}", passwordValid);
                    
                    if (passwordValid)
                    {
                        // Force verify the user in development
                        if (!user.IsEmailVerified)
                        {
                            user.IsEmailVerified = true;
                            user.EmailVerificationToken = null;
                            user.EmailVerificationTokenExpires = null;
                            await _userManager.UpdateAsync(user);
                            _logger.LogInformation("Dev Login - Auto-verified user {Email}", user.Email);
                        }
                        
                        // Sign in the user
                        await _signInManager.SignInAsync(user, false);
                        
                        // Update last login time
                        user.LastLogin = DateTime.UtcNow;
                        await _userManager.UpdateAsync(user);
                        
                        TempData["SuccessMessage"] = "Development login successful!";
                        return RedirectToAction("Index", "Dashboard");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Invalid password.";
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "User not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during development login");
                TempData["ErrorMessage"] = "Login error: " + ex.Message;
            }

            return RedirectToAction("Login");
        }

        // Bulk action for users
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> BulkUserAction(string actionId, [FromBody] BulkActionRequest request)
        {
            try
            {
                var userIds = request.SelectedIds.Select(id => id.ToString()).ToList();
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToListAsync();

                if (!users.Any())
                {
                    return Json(new { success = false, message = "No users found." });
                }

                switch (actionId.ToLower())
                {
                    case "verify":
                        foreach (var user in users)
                        {
                            user.EmailConfirmed = true;
                        }
                        break;

                    case "lock":
                        foreach (var user in users)
                        {
                            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100); // Effectively permanent
                        }
                        break;

                    case "unlock":
                        foreach (var user in users)
                        {
                            user.LockoutEnd = null;
                        }
                        break;

                    case "delete":
                        _context.Users.RemoveRange(users);
                        break;

                    default:
                        return Json(new { success = false, message = "Unknown action." });
                }

                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = $"Successfully processed {users.Count} user(s)." 
                });
            }
            catch (Exception)
            {
                return Json(new { 
                    success = false, 
                    message = "An error occurred while processing the request." 
                });
            }
        }

        // Export users
        [Authorize]
        public async Task<IActionResult> ExportUsers(string format = "csv", string searchTerm = "", string[]? statusFilter = null)
        {
            var query = _context.Users.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(u => 
                    u.FullName.Contains(searchTerm) ||
                    (u.Email != null && u.Email.Contains(searchTerm)) ||
                    (u.UserName != null && u.UserName.Contains(searchTerm)));
            }

            if (statusFilter != null && statusFilter.Any())
            {
                if (statusFilter.Contains("Active"))
                    query = query.Where(u => u.EmailConfirmed && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow));
                if (statusFilter.Contains("Unverified"))
                    query = query.Where(u => !u.EmailConfirmed);
                if (statusFilter.Contains("Locked"))
                    query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
            }

            var users = await query
                .Select(u => new
                {
                    u.FullName,
                    Email = u.Email ?? "",
                    UserName = u.UserName ?? "",
                    u.WorkerId,
                    PhoneNumber = u.PhoneNumber ?? "",
                    LastLoginDate = u.LastLogin.ToString("yyyy-MM-dd"),
                    Status = u.EmailConfirmed ? (u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow ? "Locked" : "Active") : "Unverified"
                })
                .ToListAsync();

            if (format.ToLower() == "csv")
            {
                var csv = new StringBuilder();
                csv.AppendLine("Full Name,Email,Username,Worker ID,Phone Number,Last Login Date,Status");

                foreach (var user in users)
                {
                    csv.AppendLine($"\"{user.FullName}\",\"{user.Email}\",\"{user.UserName}\",\"{user.WorkerId}\",\"{user.PhoneNumber}\",\"{user.LastLoginDate}\",\"{user.Status}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"users_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }

            return BadRequest("Unsupported export format.");
        }
    }
}
