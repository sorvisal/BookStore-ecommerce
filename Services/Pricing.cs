using E_Commerce.Models;

namespace E_Commerce.Services
{
    public static class Pricing
    {
        // The price the customer pays right now: DiscountPrice when it exists
        // and is lower than Price, otherwise Price.
        public static decimal CurrentPrice(Book b) =>
            b.DiscountPrice.HasValue && b.DiscountPrice.Value < b.Price ? b.DiscountPrice.Value : b.Price;
    }
}
