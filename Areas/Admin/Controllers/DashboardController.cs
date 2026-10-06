using E_Commerce.Areas.Admin.ViewModels;
using E_Commerce.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private static readonly string[] Statuses = { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" };

        private readonly BookStoreDbContext _db;
        public DashboardController(BookStoreDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var thisMonthStart = new DateTime(now.Year, now.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);
            var sixMonthsStart = thisMonthStart.AddMonths(-5);

            var nonCancelled = _db.Orders.AsNoTracking().Where(o => o.Status != "Cancelled");

            var vm = new DashboardViewModel
            {
                TotalIncome = await nonCancelled.SumAsync(o => o.GrandTotal ?? 0m),
                IncomeThisMonth = await nonCancelled.Where(o => o.OrderDate >= thisMonthStart)
                    .SumAsync(o => o.GrandTotal ?? 0m),
                IncomeLastMonth = await nonCancelled.Where(o => o.OrderDate >= lastMonthStart && o.OrderDate < thisMonthStart)
                    .SumAsync(o => o.GrandTotal ?? 0m),
                TotalOrders = await _db.Orders.AsNoTracking().CountAsync(),
                PendingOrders = await _db.Orders.AsNoTracking().CountAsync(o => o.Status == "Pending"),
                TotalCustomers = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "Customer"),
                TotalBooks = await _db.Books.AsNoTracking().CountAsync(),
                LowStockBooks = await _db.Books.AsNoTracking().CountAsync(b => b.StockQuantity < 10)
            };

            vm.IncomeChangePct = vm.IncomeLastMonth > 0
                ? (double)Math.Round((vm.IncomeThisMonth - vm.IncomeLastMonth) / vm.IncomeLastMonth * 100m, 1)
                : (vm.IncomeThisMonth > 0 ? 100.0 : 0.0);

            // Monthly income for the last 6 months (months without orders = 0)
            var monthlyRaw = await nonCancelled
                .Where(o => o.OrderDate >= sixMonthsStart)
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(o => o.GrandTotal ?? 0m) })
                .ToListAsync();
            for (var i = 0; i < 6; i++)
            {
                var m = sixMonthsStart.AddMonths(i);
                var amount = monthlyRaw.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Amount ?? 0m;
                vm.MonthlyIncome.Add(new MonthlyIncomeItem { Label = m.ToString("MMM yyyy"), Amount = amount });
            }

            // Orders by status (always include all 5 statuses)
            var statusRaw = await _db.Orders.AsNoTracking()
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            foreach (var s in Statuses)
            {
                vm.OrdersByStatus.Add(new StatusCountItem
                {
                    Status = s,
                    Count = statusRaw.FirstOrDefault(x => x.Status == s)?.Count ?? 0
                });
            }

            // Top 5 selling books on non-cancelled orders
            var top = await _db.OrderDetails.AsNoTracking()
                .Where(d => d.Order != null && d.Order.Status != "Cancelled")
                .GroupBy(d => d.BookId)
                .Select(g => new { BookId = g.Key, Qty = g.Sum(d => d.Quantity), Revenue = g.Sum(d => d.Subtotal ?? 0m) })
                .OrderByDescending(x => x.Qty)
                .Take(5)
                .ToListAsync();
            var topIds = top.Select(x => x.BookId).ToList();
            var topBooks = await _db.Books.AsNoTracking()
                .Where(b => topIds.Contains(b.BookId))
                .Select(b => new { b.BookId, b.Title, b.ImageUrl, b.CoverImage })
                .ToListAsync();
            vm.TopBooks = top.Select(x =>
            {
                var b = topBooks.First(t => t.BookId == x.BookId);
                return new TopBookItem
                {
                    BookId = x.BookId,
                    Title = b.Title,
                    Cover = !string.IsNullOrWhiteSpace(b.ImageUrl) ? b.ImageUrl : b.CoverImage,
                    Quantity = x.Qty,
                    Revenue = x.Revenue
                };
            }).ToList();

            // 8 most recent orders
            vm.RecentOrders = await _db.Orders.AsNoTracking()
                .OrderByDescending(o => o.OrderDate)
                .Take(8)
                .Select(o => new RecentOrderItem
                {
                    OrderId = o.OrderId,
                    OrderNumber = o.OrderNumber,
                    Customer = o.User != null ? o.User.FirstName + " " + o.User.LastName : (o.ReceiverName ?? "-"),
                    Date = o.OrderDate,
                    Total = o.GrandTotal ?? 0m,
                    Status = o.Status
                })
                .ToListAsync();

            // 5 books with the lowest stock
            vm.LowStock = await _db.Books.AsNoTracking()
                .OrderBy(b => b.StockQuantity).ThenBy(b => b.Title)
                .Take(5)
                .Select(b => new LowStockBookItem
                {
                    BookId = b.BookId,
                    Title = b.Title,
                    Cover = b.ImageUrl ?? b.CoverImage,
                    Stock = b.StockQuantity
                })
                .ToListAsync();

            return View(vm);
        }
    }
}
