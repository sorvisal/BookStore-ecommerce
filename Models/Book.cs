using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Book
    {
        [Key]
        public int BookId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public int PublisherId { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; }

        [MaxLength(50)]
        public string ISBN { get; set; }

        public string Description { get; set; }

        [MaxLength(50)]
        public string Language { get; set; }

        public int? PublishYear { get; set; }

        public int? Pages { get; set; }

        [Required, Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountPrice { get; set; }

        public int StockQuantity { get; set; } = 0;

        [MaxLength(255)]
        public string CoverImage { get; set; }

        public bool Status { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category Category { get; set; }

        [ForeignKey(nameof(PublisherId))]
        public Publisher Publisher { get; set; }

        public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}