using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class Publisher
    {
        [Key]
        public int PublisherId { get; set; }
        [Required, StringLength(200)]
        public string PublisherName { get; set; } = string.Empty;
        [StringLength(30)]
        public string? Phone { get; set; }
        [StringLength(150)]
        public string? Email { get; set; }
        [StringLength(200)]
        public string? Website { get; set; }
        [StringLength(255)]
        public string? Address { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public ICollection<Book> Books { get; set; } = new List<Book>();

    }
}
