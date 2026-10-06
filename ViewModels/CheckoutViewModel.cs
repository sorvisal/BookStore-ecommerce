using System.ComponentModel.DataAnnotations;
using E_Commerce.Models;

namespace E_Commerce.ViewModels
{
    public class CheckoutViewModel
    {
        // ---- Form fields (posted) ----
        [Required, StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, Phone]
        public string Phone { get; set; } = string.Empty;

        [Required, StringLength(300)]
        [Display(Name = "Shipping address")]
        public string ShippingAddress { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Note { get; set; }

        [Required(ErrorMessage = "Please choose a shipping method.")]
        [Display(Name = "Shipping method")]
        public int? ShippingMethodId { get; set; }

        [Required(ErrorMessage = "Please choose a payment method.")]
        [Display(Name = "Payment method")]
        public int? PaymentMethodId { get; set; }

        // ---- Display data (not posted) ----
        public List<CheckoutLineViewModel> Items { get; set; } = new();
        public List<ShippingMethod> ShippingMethods { get; set; } = new();
        public List<PaymentMethod> PaymentMethods { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Total { get; set; }
        public decimal FreeShippingThreshold { get; set; } = 30m;
    }

    public class CheckoutLineViewModel
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }
}
