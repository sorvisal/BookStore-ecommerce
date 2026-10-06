using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [StringLength(30)]
        public string? OrderNumber { get; set; }

        [Required]
        public int UserId { get; set; }

        public int? ShippingMethodId { get; set; }

        public int? PaymentMethodId { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [StringLength(150)]
        public string? ReceiverName { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Province { get; set; }

        [StringLength(100)]
        public string? District { get; set; }

        [StringLength(100)]
        public string? Commune { get; set; }

        [StringLength(100)]
        public string? Village { get; set; }

        [StringLength(255)]
        public string? Street { get; set; }
        [StringLength(500)]
        public string? ShippingAddress { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0, double.MaxValue)]
        public decimal? Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0, double.MaxValue)]
        public decimal? Discount { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0, double.MaxValue)]
        public decimal? ShippingFee { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0, double.MaxValue)]
        public decimal? Tax { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0, double.MaxValue)]
        public decimal? GrandTotal { get; set; }
        [Column(TypeName = "decimal(18,2)"), Range(0, double.MaxValue)]
        public decimal? TotalAmount { get; set; }

        [StringLength(30)]
        public string OrderStatus { get; set; } = "Pending";
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        [StringLength(30)]
        public string PaymentStatus { get; set; } = "Unpaid";

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(ShippingMethodId))]
        public ShippingMethod? ShippingMethod { get; set; }

        [ForeignKey(nameof(PaymentMethodId))]
        public PaymentMethod? PaymentMethod { get; set; }

        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public Payment? Payment { get; set; }
    }
}