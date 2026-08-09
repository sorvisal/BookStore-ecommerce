using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class ShippingMethod
    {
        [Key]
        public int ShippingMethodId { get; set; }

        [MaxLength(100)]
        public string MethodName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Price { get; set; }

        public int? EstimatedDays { get; set; }

        public bool Status { get; set; } = true;

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}