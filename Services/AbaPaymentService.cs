using E_Commerce.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Services
{
    /// <summary>Who triggered a manual/automatic ABA payment confirmation.</summary>
    public enum AbaPaidSource
    {
        /// <summary>ABA itself approved the payment (<c>data.action == "approved"</c>).</summary>
        Aba,

        /// <summary>An administrator manually marked the order as paid.</summary>
        Admin,
    }

    /// <summary>
    /// Shared, idempotent payment confirmation. There are exactly two allowed callers:
    /// the customer-facing status endpoint (only after a strict ABA approval) and the
    /// admin "Mark as paid" action. Confirming twice is harmless.
    /// </summary>
    public sealed class AbaPaymentService
    {
        internal const string AbaRemark = "Paid with ABA (KHQR) - confirmed by ABA.";
        internal const string AdminRemark = "Marked as paid by admin (manual ABA check).";

        private readonly BookStoreDbContext _db;
        private readonly ILogger<AbaPaymentService> _logger;

        public AbaPaymentService(BookStoreDbContext db, ILogger<AbaPaymentService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Marks the given order as paid. Returns <c>true</c> when the order exists
        /// (whether or not it was already paid) and <c>false</c> only when no such
        /// order can be found. Never logs secrets or payment references.
        /// </summary>
        public async Task<bool> MarkAbaPaidAsync(int orderId, AbaPaidSource source)
        {
            var order = await _db.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                _logger.LogWarning(
                    "Mark-as-paid for order {OrderId} ignored: order not found (source={Source}).",
                    orderId,
                    source);
                return false;
            }

            bool alreadyPaid =
                string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
                (order.Payment != null &&
                 string.Equals(order.Payment.Status, "Paid", StringComparison.OrdinalIgnoreCase));

            if (alreadyPaid)
            {
                _logger.LogInformation(
                    "Mark-as-paid for order {OrderId} ignored: already paid (source={Source}).",
                    orderId,
                    source);
                return true;
            }

            string reason = source == AbaPaidSource.Aba ? AbaRemark : AdminRemark;
            var now = DateTime.Now;
            order.PaymentStatus = "Paid";

            if (order.Payment != null)
            {
                order.Payment.Status = "Paid";
                order.Payment.PaidDate = now;
                order.Payment.Remark = reason;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Order {OrderId} marked as Paid by {Source}: {Reason}",
                orderId,
                source,
                reason);
            return true;
        }
    }
}
