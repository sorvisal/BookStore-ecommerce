using E_Commerce.Data;
using E_Commerce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PublishersController : Controller
    {
        private readonly BookStoreDbContext _db;
        public PublishersController(BookStoreDbContext db) => _db = db;

        public async Task<IActionResult> Index()
            => View(await _db.Publishers.AsNoTracking().OrderBy(p => p.PublisherName).ToListAsync());

        public IActionResult Create() => View("Form", new Publisher());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Publisher model)
        {
            if (!ModelState.IsValid) return View("Form", model);
            _db.Publishers.Add(new Publisher
            {
                PublisherName = model.PublisherName.Trim(),
                Phone = model.Phone,
                Email = model.Email,
                Website = model.Website,
                Address = model.Address,
                Description = model.Description,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Publisher created.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var pub = await _db.Publishers.FindAsync(id);
            if (pub == null) return NotFound();
            return View("Form", pub);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Publisher model)
        {
            if (id != model.PublisherId) return NotFound();
            if (!ModelState.IsValid) return View("Form", model);
            var pub = await _db.Publishers.FindAsync(id);
            if (pub == null) return NotFound();
            pub.PublisherName = model.PublisherName.Trim();
            pub.Phone = model.Phone;
            pub.Email = model.Email;
            pub.Website = model.Website;
            pub.Address = model.Address;
            pub.Description = model.Description;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Publisher updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var pub = await _db.Publishers.FindAsync(id);
            if (pub == null) return NotFound();
            if (await _db.Books.AnyAsync(b => b.PublisherId == id))
            {
                TempData["Error"] = "This publisher has books and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }
            _db.Publishers.Remove(pub);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Publisher deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
