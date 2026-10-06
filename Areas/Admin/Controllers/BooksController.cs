using E_Commerce.Areas.Admin.ViewModels;
using E_Commerce.Data;
using E_Commerce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BooksController : Controller
    {
        private const int PageSize = 10;
        private const long MaxCoverBytes = 2 * 1024 * 1024; // 2 MB
        private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedMime = { "image/jpeg", "image/png", "image/webp" };

        private readonly BookStoreDbContext _db;
        private readonly IWebHostEnvironment _env;

        public BooksController(BookStoreDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index(string? search, int? categoryId, string? stock, int page = 1)
        {
            var q = _db.Books.AsNoTracking()
                .Include(b => b.Category)
                .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(b => b.Title.Contains(s)
                    || (b.ISBN != null && b.ISBN.Contains(s))
                    || b.BookAuthors.Any(ba => ba.Author != null && ba.Author.FullName.Contains(s)));
            }
            if (categoryId.HasValue) q = q.Where(b => b.CategoryId == categoryId.Value);
            q = stock switch
            {
                "in" => q.Where(b => b.StockQuantity >= 10),
                "low" => q.Where(b => b.StockQuantity > 0 && b.StockQuantity < 10),
                "out" => q.Where(b => b.StockQuantity <= 0),
                _ => q
            };

            var total = await q.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var vm = new BookListViewModel
            {
                Books = await q
                    .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.BookId)
                    .Skip((page - 1) * PageSize).Take(PageSize)
                    .ToListAsync(),
                Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync(),
                Search = search,
                CategoryId = categoryId,
                Stock = stock,
                Page = page,
                TotalPages = totalPages
            };
            return View(vm);
        }

        public async Task<IActionResult> Create()
        {
            var vm = new BookFormViewModel { Status = true };
            await FillListsAsync(vm);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookFormViewModel model)
        {
            await ValidateBookAsync(model);

            var book = new Book();
            if (ModelState.IsValid)
            {
                var imageError = await TrySaveCoverAsync(model, book, null);
                if (imageError != null)
                    ModelState.AddModelError(nameof(model.CoverFile), imageError);
            }

            if (!ModelState.IsValid)
            {
                await FillListsAsync(model);
                return View(model);
            }

            book.Title = model.Title.Trim();
            book.ISBN = string.IsNullOrWhiteSpace(model.ISBN) ? null : model.ISBN.Trim();
            book.Description = model.Description;
            book.Language = model.Language;
            book.PublishYear = model.PublishYear;
            book.PublishedDate = model.PublishedDate;
            book.Pages = model.Pages;
            book.Price = model.Price;
            book.DiscountPrice = model.DiscountPrice;
            book.StockQuantity = model.StockQuantity;
            book.Status = model.Status;
            book.CategoryId = model.CategoryId!.Value;
            book.PublisherId = model.PublisherId!.Value;
            book.CreatedAt = DateTime.Now;
            foreach (var authorId in model.SelectedAuthorIds.Distinct())
                book.BookAuthors.Add(new BookAuthor { AuthorId = authorId });

            _db.Books.Add(book);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Book created.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var book = await _db.Books.AsNoTracking()
                .Include(b => b.BookAuthors)
                .FirstOrDefaultAsync(b => b.BookId == id);
            if (book == null) return NotFound();

            var vm = new BookFormViewModel
            {
                BookId = book.BookId,
                Title = book.Title,
                ISBN = book.ISBN,
                Description = book.Description,
                Language = book.Language,
                PublishYear = book.PublishYear,
                PublishedDate = book.PublishedDate,
                Pages = book.Pages,
                Price = book.Price,
                DiscountPrice = book.DiscountPrice,
                StockQuantity = book.StockQuantity,
                Status = book.Status,
                CategoryId = book.CategoryId,
                PublisherId = book.PublisherId,
                SelectedAuthorIds = book.BookAuthors.Select(ba => ba.AuthorId).ToList(),
                ImageUrl = book.ImageUrl
            };
            await FillListsAsync(vm);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BookFormViewModel model)
        {
            if (id != model.BookId) return NotFound();
            await ValidateBookAsync(model);

            var book = await _db.Books
                .Include(b => b.BookAuthors)
                .FirstOrDefaultAsync(b => b.BookId == id);
            if (book == null) return NotFound();

            if (ModelState.IsValid)
            {
                var imageError = await TrySaveCoverAsync(model, book, book.ImageUrl);
                if (imageError != null)
                    ModelState.AddModelError(nameof(model.CoverFile), imageError);
            }

            if (!ModelState.IsValid)
            {
                await FillListsAsync(model);
                return View(model);
            }

            book.Title = model.Title.Trim();
            book.ISBN = string.IsNullOrWhiteSpace(model.ISBN) ? null : model.ISBN.Trim();
            book.Description = model.Description;
            book.Language = model.Language;
            book.PublishYear = model.PublishYear;
            book.PublishedDate = model.PublishedDate;
            book.Pages = model.Pages;
            book.Price = model.Price;
            book.DiscountPrice = model.DiscountPrice;
            book.StockQuantity = model.StockQuantity;
            book.Status = model.Status;
            book.CategoryId = model.CategoryId!.Value;
            book.PublisherId = model.PublisherId!.Value;
            book.UpdatedAt = DateTime.Now;

            // Replace the author rows with the selected ones
            _db.BookAuthors.RemoveRange(book.BookAuthors);
            foreach (var authorId in model.SelectedAuthorIds.Distinct())
                _db.BookAuthors.Add(new BookAuthor { BookId = book.BookId, AuthorId = authorId });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "This book was changed by someone else, please reload.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            TempData["Success"] = "Book updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var book = await _db.Books
                .Include(b => b.BookAuthors)
                .FirstOrDefaultAsync(b => b.BookId == id);
            if (book == null) return NotFound();

            if (await _db.OrderDetails.AnyAsync(od => od.BookId == id))
            {
                TempData["Error"] = "This book has orders and cannot be deleted. Set it to inactive instead.";
                return RedirectToAction(nameof(Index));
            }

            _db.BookAuthors.RemoveRange(book.BookAuthors);
            _db.CartItems.RemoveRange(await _db.CartItems.Where(ci => ci.BookId == id).ToListAsync());
            _db.Reviews.RemoveRange(await _db.Reviews.Where(r => r.BookId == id).ToListAsync());
            _db.Books.Remove(book);
            await _db.SaveChangesAsync();

            DeleteCoverFile(book.ImageUrl);
            TempData["Success"] = "Book deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var book = await _db.Books.FindAsync(id);
            if (book == null) return NotFound();
            book.Status = !book.Status;
            book.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = book.Status ? "Book activated." : "Book deactivated.";
            return RedirectToAction(nameof(Index));
        }

        // ---- Helpers ----

        private async Task FillListsAsync(BookFormViewModel vm)
        {
            vm.Categories = await _db.Categories.AsNoTracking().Where(c => c.Status).OrderBy(c => c.CategoryName).ToListAsync();
            vm.Publishers = await _db.Publishers.AsNoTracking().OrderBy(p => p.PublisherName).ToListAsync();
            vm.Authors = await _db.Authors.AsNoTracking().OrderBy(a => a.FullName).ToListAsync();
        }

        private async Task ValidateBookAsync(BookFormViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.ISBN))
            {
                var isbn = model.ISBN.Trim();
                var taken = await _db.Books.AnyAsync(b => b.ISBN == isbn && b.BookId != (model.BookId ?? 0));
                if (taken)
                    ModelState.AddModelError(nameof(model.ISBN), "This ISBN is already used by another book.");
            }

            if (model.DiscountPrice.HasValue && model.DiscountPrice.Value >= model.Price)
                ModelState.AddModelError(nameof(model.DiscountPrice), "The discount price must be lower than the price.");

            if (model.CategoryId == null || !await _db.Categories.AnyAsync(c => c.CategoryId == model.CategoryId.Value))
                ModelState.AddModelError(nameof(model.CategoryId), "Please choose a valid category.");

            if (model.PublisherId == null || !await _db.Publishers.AnyAsync(p => p.PublisherId == model.PublisherId.Value))
                ModelState.AddModelError(nameof(model.PublisherId), "Please choose a valid publisher.");
        }

        // Saves a new cover (if one was uploaded) and sets ImageUrl + CoverImage.
        // Returns an error message, or null on success / when nothing was uploaded.
        private async Task<string?> TrySaveCoverAsync(BookFormViewModel model, Book book, string? oldImageUrl)
        {
            var file = model.CoverFile;
            if (file == null || file.Length == 0)
            {
                if (oldImageUrl != null)
                {
                    book.ImageUrl = oldImageUrl;
                    book.CoverImage = oldImageUrl;
                }
                return null;
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext) || !AllowedMime.Contains(file.ContentType.ToLowerInvariant()))
                return "Only JPG, PNG or WEBP images are allowed.";
            if (file.Length > MaxCoverBytes)
                return "The cover image must be 2 MB or smaller.";

            var folder = Path.Combine(_env.WebRootPath, "images", "books");
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid().ToString("N") + ext;
            var path = Path.Combine(folder, name);
            await using (var stream = System.IO.File.Create(path))
                await file.CopyToAsync(stream);

            var url = "/images/books/" + name;
            book.ImageUrl = url;
            book.CoverImage = url;

            DeleteCoverFile(oldImageUrl);
            return null;
        }

        // Deletes a cover file only when it lives under /images/books
        // (never placeholder.svg or anything outside that folder).
        private void DeleteCoverFile(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("/images/books/", StringComparison.OrdinalIgnoreCase)) return;
            var fileName = Path.GetFileName(url);
            if (string.IsNullOrWhiteSpace(fileName)) return;
            var path = Path.Combine(_env.WebRootPath, "images", "books", fileName);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }
}
