namespace E_Commerce.Services
{
    /// <summary>
    /// Central helper for deciding whether a payment/order is paid. Uses exact,
    /// case-insensitive equality — never a substring match — so that "Unpaid"
    /// can never be mistaken for "Paid".
    /// </summary>
    public static class PaymentStatusHelper
    {
        public const string Paid = "Paid";

        /// <summary>True only when <paramref name="status"/> equals "Paid" (exact, case-insensitive).</summary>
        public static bool IsPaidStatus(string? status) =>
            string.Equals(status?.Trim(), Paid, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True only when <b>both</b> the order-level and payment-level statuses equal
        /// "Paid" exactly. An order is treated as settled only when they agree.
        /// </summary>
        public static bool IsOrderPaid(string? orderPaymentStatus, string? paymentStatus) =>
            IsPaidStatus(orderPaymentStatus) && IsPaidStatus(paymentStatus);
    }
}
