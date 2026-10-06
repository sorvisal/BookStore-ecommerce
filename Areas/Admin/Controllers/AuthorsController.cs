using E_Commerce.Data;
using E_Commerce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AuthorsController : Controller
    {
        private readonly BookStoreDbContext _db;
        public AuthorsController(BookStoreDbContext db) => _db = db;

        public async Task<IActionResult> Index()
            => View(await _db.Authors.AsNoTracking().OrderBy(a => a.FullName).ToListAsync());

        public IActionResult Create() => View("Form", new Author());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Author model)
        {
            if (!ModelState.IsValid) return View("Form", model);
            _db.Authors.Add(new Author
            {
                FullName = model.FullName.Trim(),
                Biography = model.Biography,
                Country = model.Country,
                Photo = model.Photo,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Author created.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var author = await _db.Authors.FindAsync(id);
            if (author == null) return NotFound();
            return View("Form", author);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Author model)
        {
            if (id != model.AuthorId) return NotFound();
            if (!ModelState.IsValid) return View("Form", model);
            var author = await _db.Authors.FindAsync(id);
            if (author == null) return NotFound();
            author.FullName = model.FullName.Trim();
            author.Biography = model.Biography;
            author.Country = model.Country;
            author.Photo = model.Photo;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Author updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var author = await _db.Authors.FindAsync(id);
            if (author == null) return NotFound();
            if (await _db.BookAuthors.AnyAsync(ba => ba.AuthorId == id))
            {
                TempData["Error"] = "This author has books and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }
            _db.Authors.Remove(author);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Author deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
