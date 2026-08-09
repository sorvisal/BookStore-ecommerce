using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        [MaxLength(100)]
        public string FirstName { get; set; }
        [MaxLength(100)]
        public string LastName { get; set; }
        [MaxLength(20)]
        public string Gender { get; set; }
        public DateTime? DateOrBirth { get; set; }
        [MaxLength(20)]
        public string Phone { get; set; }
        [MaxLength(150)]
        public string Email { get; set; }
        [MaxLength(255)]
        public string PasswordHash { get; set; }
        [MaxLength(255)]
        public string ProfileImage { get; set; }
        [MaxLength(255)]
        public string Address { get; set; }
        public bool Status { get; set; } = true;
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public ShoppingCart ShoppingCart { get; set; }
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}