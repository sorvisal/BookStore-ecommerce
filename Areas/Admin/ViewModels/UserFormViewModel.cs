using System.ComponentModel.DataAnnotations;
using E_Commerce.ViewModels;

namespace E_Commerce.Areas.Admin.ViewModels
{
    public class UserFormViewModel
    {
        public int? UserId { get; set; } // null on create

        [Required, StringLength(100)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(20)]
        public string? Gender { get; set; }

        [Display(Name = "Date of birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        [Required]
        public string Role { get; set; } = "Customer";

        [Display(Name = "Active (can log in)")]
        public bool Status { get; set; } = true;

        // Required on create, optional on edit (empty = keep the old one)
        [MinLength(6, ErrorMessage = "The password must be at least 6 characters.")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string? ConfirmPassword { get; set; }

        public IFormFile? Photo { get; set; }

        public bool RemovePhoto { get; set; }

        // ---- Display-only ----
        public string? ProfileImage { get; set; }
        public AvatarViewModel Avatar { get; set; } = new();
    }
}
