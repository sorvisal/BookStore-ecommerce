using E_Commerce.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MessagesController : Controller
    {
        private readonly BookStoreDbContext _db;
        public MessagesController(BookStoreDbContext db) => _db = db;

        public async Task<IActionResult> Index()
            => View(await _db.Contacts.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var msg = await _db.Contacts.FindAsync(id);
            if (msg == null) return NotFound();
            _db.Contacts.Remove(msg);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Message deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
