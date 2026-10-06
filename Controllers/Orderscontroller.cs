using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private const int PageSize = 8;
        private readonly BookStoreDbContext _db;
        public OrdersController(BookStoreDbContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> Index(string? status, int page = 1)
        {
            var uid = CurrentUserId;
            var q = _db.Orders.AsNoTracking().Where(o => o.UserId == uid);
            if (!string.IsNullOrEmpty(status)) q = q.Where(o => o.Status == status);

            var total = await q.CountAsync();
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            page = Math.Clamp(page, 1, pages);

            var orders = await q.OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * PageSize).Take(PageSize)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Book)
                .ToListAsync();

            return View(new OrderListViewModel { Orders = orders, Status = status, Page = page, TotalPages = pages });
        }

        public async Task<IActionResult> Details(int id)
        {
            var uid = CurrentUserId;
            var order = await _db.Orders.AsNoTracking()
                .Include(o => o.OrderDetails).ThenInclude(d => d.Book)
                .Include(o => o.ShippingMethod)
                .Include(o => o.Payment).ThenInclude(p => p!.PaymentMethod)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == uid);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var uid = CurrentUserId;
            var order = await _db.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Book)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == uid);
            if (order == null) return NotFound();

            if (order.Status != "Pending")
            {
                TempData["Error"] = "Only pending orders can be cancelled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            order.Status = "Cancelled";
            if (order.Payment != null) order.Payment.Status = "Cancelled";
            foreach (var d in order.OrderDetails)
                if (d.Book != null) d.Book.StockQuantity += d.Quantity;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["Success"] = "Order cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}