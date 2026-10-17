using E_Commerce.Models;

namespace E_Commerce.Services
{
    /// <summary>
    /// The single source of truth for order money. Every total stored on an
    /// order (and shown to the customer) must come from here so the amount the
    /// customer sees always matches the amount we charge.
    /// </summary>
    public static class OrderTotals
    {
        public const decimal FreeShippingThreshold = 30m;

        public static decimal Round(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Subtotal is the sum of the current price of each book times its
        /// quantity. Shipping is free from the threshold. Discount and tax are
        /// currently always zero (the existing rule, consistent with the seeded
        /// orders). GrandTotal = Subtotal - Discount + ShippingFee + Tax.
        /// </summary>
        public static OrderTotalsResult Compute(IEnumerable<CartItem> lines, decimal shippingPrice)
        {
            ArgumentNullException.ThrowIfNull(lines);

            decimal subtotal = Round(lines.Sum(i => Pricing.CurrentPrice(i.Book!) * i.Quantity));
            return FromSubtotal(subtotal, shippingPrice);
        }

        public static OrderTotalsResult FromSubtotal(decimal subtotalInput, decimal shippingPrice)
        {
            decimal subtotal = Round(subtotalInput);
            decimal discount = 0m;
            decimal shippingFee = subtotal >= FreeShippingThreshold ? 0m : Round(shippingPrice);
            decimal tax = 0m;
            decimal grandTotal = Round(subtotal - discount + shippingFee + tax);

            return new OrderTotalsResult(subtotal, discount, shippingFee, tax, grandTotal);
        }
    }

    public readonly record struct OrderTotalsResult(
        decimal Subtotal,
        decimal Discount,
        decimal ShippingFee,
        decimal Tax,
        decimal GrandTotal);
}
