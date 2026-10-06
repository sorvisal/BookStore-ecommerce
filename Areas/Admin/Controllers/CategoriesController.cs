using E_Commerce.Data;
using E_Commerce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly BookStoreDbContext _db;
        public CategoriesController(BookStoreDbContext db) => _db = db;

        public async Task<IActionResult> Index()
            => View(await _db.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync());

        public IActionResult Create() => View("Form", new Category());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category model)
        {
            if (!ModelState.IsValid) return View("Form", model);
            _db.Categories.Add(new Category
            {
                CategoryName = model.CategoryName.Trim(),
                Description = model.Description,
                Image = model.Image,
                Status = model.Status,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Category created.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound();
            return View("Form", cat);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category model)
        {
            if (id != model.CategoryId) return NotFound();
            if (!ModelState.IsValid) return View("Form", model);
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound();
            cat.CategoryName = model.CategoryName.Trim();
            cat.Description = model.Description;
            cat.Image = model.Image;
            cat.Status = model.Status;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Category updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound();
            if (await _db.Books.AnyAsync(b => b.CategoryId == id))
            {
                TempData["Error"] = "This category is used by books and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }
            _db.Categories.Remove(cat);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Category deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
