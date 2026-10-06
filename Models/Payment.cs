using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        public int? OrderId { get; set; }

        public int? PaymentMethodId { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0, double.MaxValue)]
        public decimal? Amount { get; set; }

        [StringLength(100)]
        public string? TransactionReference { get; set; }

        [StringLength(255)]
        public string? QRCode { get; set; }

        public DateTime? PaidDate { get; set; }

        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        public int? VerifiedBy { get; set; }

        [StringLength(255)]
        public string? Remark { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }

        [ForeignKey(nameof(PaymentMethodId))]
        public PaymentMethod? PaymentMethod { get; set; }
    }
}
