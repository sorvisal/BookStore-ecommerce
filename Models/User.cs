using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        [Required, StringLength(100)]
        public string FirstName { get; set; } = string.Empty;
        [Required, StringLength(100)]
        public string LastName { get; set; } = string.Empty;
        [StringLength(20)]
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        [StringLength(20)]
        public string? Phone { get; set; }
        [Required, StringLength(150)]
        public string Email { get; set; } = string.Empty;
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        [StringLength(255)]
        public string? ProfileImage { get; set; }
        [StringLength(255)]
        public string? Address { get; set; }
        [StringLength(20)]
        public string Role { get; set; } = "Customer";
        public bool Status { get; set; } = true;
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public ShoppingCart? ShoppingCart { get; set; }
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}