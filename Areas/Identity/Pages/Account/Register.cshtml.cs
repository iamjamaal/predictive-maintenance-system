// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using FEENALOoFINALE.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using FEENALOoFINALE.Services;

namespace FEENALOoFINALE.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly IUserStore<User> _userStore;
        private readonly IUserEmailStore<User> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailService _emailService;

        public RegisterModel(
            UserManager<User> userManager,
            IUserStore<User> userStore,
            SignInManager<User> signInManager,
            ILogger<RegisterModel> logger,
            IEmailService emailService)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
            [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, and dashes")]
            [Display(Name = "Username")]
            public string UserName { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            [StringLength(50)]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Required]
            [StringLength(50)]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }

            [Required]
            [StringLength(20)]
            [Display(Name = "Worker ID")]
            public string WorkerId { get; set; }

            [Phone]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                // Custom validation: Check if username already exists
                var existingUserByUsername = await _userManager.FindByNameAsync(Input.UserName);
                if (existingUserByUsername != null)
                {
                    ModelState.AddModelError("Input.UserName", "This username is already taken.");
                    return Page();
                }

                // Custom validation: Check if Worker ID already exists
                var existingUserByWorkerId = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.WorkerId == Input.WorkerId);
                if (existingUserByWorkerId != null)
                {
                    ModelState.AddModelError("Input.WorkerId", "This Worker ID is already registered.");
                    return Page();
                }

                var user = CreateUser();

                user.UserName = Input.UserName;
                user.FirstName = Input.FirstName;
                user.LastName = Input.LastName;
                user.WorkerId = Input.WorkerId;
                user.PhoneNumber = Input.PhoneNumber;
                user.IsEmailVerified = false; // Explicitly set for new users
                user.EmailVerificationToken = GenerateEmailVerificationToken();
                user.EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24);

                // The user manager will handle setting the normalized username from the UserName property
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password for email {Email}.", user.Email);

                    try
                    {
                        // Send verification email using the custom flow from UserController
                        var verificationUrl = Url.Action("VerifyEmail", "User", 
                            new { userId = user.Id, token = user.EmailVerificationToken }, 
                            Request.Scheme);

                        if (verificationUrl != null)
                        {
                            await _emailService.SendEmailVerificationAsync(user.Email, verificationUrl);
                            TempData["SuccessMessage"] = "Account created successfully! Please check your email to verify your account.";
                        }
                        else
                        {
                            _logger.LogWarning("Could not generate verification URL for user {UserId}.", user.Id);
                            TempData["WarningMessage"] = "Account created, but there was an issue generating the verification link. Please contact support.";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
                        TempData["WarningMessage"] = "Account created, but we couldn't send the verification email. Please contact support.";
                    }
                    
                    // Redirect to login page to enforce email verification
                    return RedirectToPage("./Login");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private string GenerateEmailVerificationToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private User CreateUser()
        {
            try
            {
                return Activator.CreateInstance<User>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(User)}'. " +
                    $"Ensure that '{nameof(User)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<User> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<User>)_userStore;
        }
    }
}