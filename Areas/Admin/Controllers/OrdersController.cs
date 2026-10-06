using E_Commerce.Data;
using E_Commerce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrdersController : Controller
    {
        private const int PageSize = 10;
        private static readonly string[] Statuses = { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" };

        // Allowed status transitions; Delivered and Cancelled are final
        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            ["Pending"] = new[] { "Processing", "Cancelled" },
            ["Processing"] = new[] { "Shipped", "Cancelled" },
            ["Shipped"] = new[] { "Delivered" }
        };

        private readonly BookStoreDbContext _db;
        public OrdersController(BookStoreDbContext db) => _db = db;

        private IQueryable<Order> ApplyFilters(string? search, DateTime? from, DateTime? to)
        {
            var q = _db.Orders.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(o => (o.OrderNumber != null && o.OrderNumber.Contains(s))
                    || (o.User != null && ((o.User.FirstName + " " + o.User.LastName).Contains(s)
                        || o.User.Email.Contains(s))));
            }
            if (from.HasValue) q = q.Where(o => o.OrderDate >= from.Value.Date);
            if (to.HasValue) q = q.Where(o => o.OrderDate < to.Value.Date.AddDays(1));
            return q;
        }

        public async Task<IActionResult> Index(string? status, string? search, DateTime? from, DateTime? to, int page = 1)
        {
            var baseQ = ApplyFilters(search, from, to);

            var counts = await baseQ
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            var q = baseQ;
            if (!string.IsNullOrWhiteSpace(status) && Statuses.Contains(status))
                q = q.Where(o => o.Status == status);

            // Summary of the filtered non-cancelled orders
            var activeQ = q.Where(o => o.Status != "Cancelled");
            ViewData["FilteredCount"] = await activeQ.CountAsync();
            ViewData["FilteredIncome"] = await activeQ.SumAsync(o => o.GrandTotal ?? 0m);

            var total = await q.CountAsync();
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            page = Math.Clamp(page, 1, pages);

            var orders = await q
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewData["Status"] = status;
            ViewData["Search"] = search;
            ViewData["From"] = from?.ToString("yyyy-MM-dd");
            ViewData["To"] = to?.ToString("yyyy-MM-dd");
            ViewData["Page"] = page;
            ViewData["TotalPages"] = pages;
            ViewData["Counts"] = counts;
            ViewData["Statuses"] = Statuses;
            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _db.Orders.AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Book)
                .Include(o => o.ShippingMethod)
                .Include(o => o.Payment).ThenInclude(p => p!.PaymentMethod)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();

            ViewData["NextStatuses"] = Allowed.TryGetValue(order.Status, out var next)
                ? next
                : Array.Empty<string>();
            return View(order);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var order = await _db.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Book)
                .Include(o => o.Payment).ThenInclude(p => p!.PaymentMethod)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();

            var allowed = Allowed.TryGetValue(order.Status, out var next)
                ? next
                : Array.Empty<string>();
            if (!allowed.Contains(status))
            {
                TempData["Error"] = $"Cannot change an order from {order.Status} to {status}.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (status == "Cancelled")
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                try
                {
                    foreach (var d in order.OrderDetails)
                        if (d.Book != null) d.Book.StockQuantity += d.Quantity;
                    if (order.Payment != null) order.Payment.Status = "Cancelled";
                    order.Status = "Cancelled";
                    order.OrderStatus = "Cancelled";
                    order.UpdatedAt = DateTime.Now;
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    TempData["Error"] = "Could not cancel the order. Please try again.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }
            else
            {
                order.Status = status;
                order.OrderStatus = status;
                order.UpdatedAt = DateTime.Now;

                if (status == "Delivered" && order.Payment?.PaymentMethod != null
                    && (string.Equals(order.Payment.PaymentMethod.Type, "COD", StringComparison.OrdinalIgnoreCase)
                        || order.Payment.PaymentMethod.MethodName.Contains("Cash", StringComparison.OrdinalIgnoreCase)))
                {
                    order.Payment.Status = "Paid";
                    order.Payment.PaidDate = DateTime.Now;
                    order.PaymentStatus = "Paid";
                }
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = $"Order status updated to {status}.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
