using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [MaxLength(30)]
        public string OrderNumber { get; set; }

        [Required]
        public int UserId { get; set; }

        public int? ShippingMethodId { get; set; }

        public int? PaymentMethodId { get; set; }

        [MaxLength(150)]
        public string ReceiverName { get; set; }

        [MaxLength(30)]
        public string Phone { get; set; }

        [MaxLength(100)]
        public string Province { get; set; }

        [MaxLength(100)]
        public string District { get; set; }

        [MaxLength(100)]
        public string Commune { get; set; }

        [MaxLength(100)]
        public string Village { get; set; }

        [MaxLength(255)]
        public string Street { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ShippingFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Tax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? GrandTotal { get; set; }

        [MaxLength(30)]
        public string OrderStatus { get; set; }

        [MaxLength(30)]
        public string PaymentStatus { get; set; }

        public string Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; }

        [ForeignKey(nameof(ShippingMethodId))]
        public ShippingMethod ShippingMethod { get; set; }

        [ForeignKey(nameof(PaymentMethodId))]
        public PaymentMethod PaymentMethod { get; set; }

        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}