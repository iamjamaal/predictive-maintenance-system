﻿using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace FEENALOoFINALE.Models
{
    public class User : IdentityUser
    {
        [Required]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string LastName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20, ErrorMessage = "Worker ID cannot exceed 20 characters")]
        [Display(Name = "Worker ID")]
        public string WorkerId { get; set; } = string.Empty;
        
        // Computed property for convenience
        public string FullName => $"{FirstName} {LastName}";

        [Phone]
        [Display(Name = "Phone Number")]
        public new string? PhoneNumber { get; set; }

        public DateTime LastLogin { get; set; }
        
        // Email verification properties
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpires { get; set; }
    
        // Navigation properties
        public virtual ICollection<Alert> AssignedAlerts { get; set; } = new List<Alert>();
        public virtual ICollection<MaintenanceTask> MaintenanceTasks { get; set; } = new List<MaintenanceTask>();
    }
}
