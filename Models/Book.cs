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

        [Required, StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [StringLength(50)]
        public string? ISBN { get; set; }

        public string? Description { get; set; }

        [StringLength(50)]
        public string? Language { get; set; }

        public int? PublishYear { get; set; }
        [DataType(DataType.Date)]
        public DateTime? PublishedDate { get; set; }

        public int? Pages { get; set; }

        [Required, Range(0.01, double.MaxValue), Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Range(0.01, double.MaxValue), Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountPrice { get; set; }

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; } = 0;
        [StringLength(255)]
        public string? ImageUrl { get; set; }
        [StringLength(255)]
        public string? CoverImage { get; set; }

        public bool Status { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }

        [ForeignKey(nameof(PublisherId))]
        public Publisher? Publisher { get; set; }

        public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}