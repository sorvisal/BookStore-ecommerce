using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CustomersController : Controller
    {
        private const int PageSize = 10;
        private readonly BookStoreDbContext _db;
        public CustomersController(BookStoreDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            var q = _db.Users.AsNoTracking().Where(u => u.Role == "Customer");
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(u => (u.FirstName + " " + u.LastName).Contains(s) || u.Email.Contains(s));
            }

            var total = await q.CountAsync();
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            page = Math.Clamp(page, 1, pages);

            var users = await q.OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

            var ids = users.Select(u => u.UserId).ToList();
            var agg = await _db.Orders.AsNoTracking()
                .Where(o => ids.Contains(o.UserId) && o.Status != "Cancelled")
                .GroupBy(o => o.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), Spent = g.Sum(o => o.GrandTotal ?? 0m) })
                .ToListAsync();

            var rows = users.Select(u =>
            {
                var a = agg.FirstOrDefault(x => x.UserId == u.UserId);
                return new AdminCustomerRow
                {
                    UserId = u.UserId,
                    FullName = $"{u.FirstName} {u.LastName}".Trim(),
                    Email = u.Email,
                    Phone = u.Phone,
                    CreatedAt = u.CreatedAt,
                    Status = u.Status,
                    OrderCount = a?.Count ?? 0,
                    TotalSpent = a?.Spent ?? 0m
                };
            }).ToList();

            ViewData["Search"] = search;
            ViewData["Page"] = page;
            ViewData["TotalPages"] = pages;
            return View(rows);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (id == adminId)
            {
                TempData["Error"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Status = !user.Status;
            user.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["Success"] = user.Status
                ? $"{user.FirstName}'s account has been activated."
                : $"{user.FirstName}'s account has been deactivated.";
            return RedirectToAction(nameof(Index));
        }
    }
}
