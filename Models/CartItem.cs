using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class CartItem
    {
        [Key]
        public int CartItemId { get; set; }

        [Required]
        public int CartId { get; set; }

        [Required]
        public int BookId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0, double.MaxValue)]
        public decimal? UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0, double.MaxValue)]
        public decimal? Subtotal { get; set; }

        [ForeignKey(nameof(CartId))]
        public ShoppingCart? ShoppingCart { get; set; }

        [ForeignKey(nameof(BookId))]
        public Book? Book { get; set; }
    }
}
