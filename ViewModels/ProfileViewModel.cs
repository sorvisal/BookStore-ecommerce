using System.ComponentModel.DataAnnotations;

namespace E_Commerce.ViewModels
{
    public class ProfileViewModel
    {
        [Required, StringLength(100)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(20)]
        public string? Gender { get; set; }

        [Display(Name = "Date of birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        // ---- Display-only (never posted into the database directly) ----
        public string Email { get; set; } = string.Empty;
        public string? ProfileImage { get; set; }
        public DateTime? MemberSince { get; set; }

        public IFormFile? Photo { get; set; }
    }
}
