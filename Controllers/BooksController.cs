using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_Commerce.Controllers
{
    public class BooksController : Controller
    {
        private readonly BookStoreDbContext _context;

        public BooksController(BookStoreDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string? search, int? categoryId, string? sortBy, int page = 1)
        {
            var query = _context.Books
                .AsNoTracking()
                .Where(b => b.Status)
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b => b.Title.Contains(term) ||
                    (b.BookAuthors.Any(ba => ba.Author != null && ba.Author.FullName.Contains(term))));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == categoryId.Value);
            }

            query = sortBy switch
            {
                "newest" or "new" => query.OrderByDescending(b => b.CreatedAt),
                "priceAsc" or "price_asc" => query.OrderBy(b => b.DiscountPrice.HasValue ? b.DiscountPrice.Value : b.Price),
                "priceDesc" or "price_desc" => query.OrderByDescending(b => b.DiscountPrice.HasValue ? b.DiscountPrice.Value : b.Price),
                "title" or "titleAsc" => query.OrderBy(b => b.Title),
                _ => query.OrderByDescending(b => b.CreatedAt)
            };

            var pageSize = 12;
            var totalCount = query.Count();
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            page = page < 1 ? 1 : (page > totalPages ? totalPages : page);

            var books = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new BookListViewModel
            {
                Books = books,
                Categories = _context.Categories.AsNoTracking().Where(c => c.Status).OrderBy(c => c.CategoryName).ToList(),
                Search = search,
                CategoryId = categoryId,
                SortBy = sortBy,
                Page = page,
                TotalPages = totalPages,
                PageSize = pageSize
            };

            return View(model);
        }

        public IActionResult Details(int id)
        {
            var book = _context.Books
                .AsNoTracking()
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .Include(b => b.Reviews).ThenInclude(r => r.User)
                .FirstOrDefault(b => b.BookId == id && b.Status);

            if (book == null)
            {
                return NotFound();
            }

            var authors = book.BookAuthors
                .Where(ba => ba.Author != null)
                .Select(ba => ba.Author!)
                .ToList();

            var reviewsWithUser = book.Reviews
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewWithUser
                {
                    ReviewId = r.ReviewId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UserName = r.User != null ? $"{r.User.FirstName} {r.User.LastName}" : "Unknown"
                })
                .ToList();

            var averageRating = reviewsWithUser.Any(r => r.Rating.HasValue)
                ? reviewsWithUser.Where(r => r.Rating.HasValue).Average(r => r.Rating!.Value)
                : 0;

            var relatedBooks = _context.Books
                .AsNoTracking()
                .Where(b => b.Status && b.CategoryId == book.CategoryId && b.BookId != book.BookId)
                .Include(b => b.Category)
                .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .OrderByDescending(b => b.CreatedAt)
                .Take(4)
                .ToList();

            var userId = GetCurrentUserId();
            var canReview = false;
            if (userId.HasValue)
            {
                canReview = !book.Reviews.Any(r => r.UserId == userId.Value);
            }

            var model = new BookDetailsViewModel
            {
                Book = book,
                Authors = authors,
                AverageRating = averageRating,
                Reviews = reviewsWithUser,
                RelatedBooks = relatedBooks,
                CanReview = canReview,
                NewReview = new NewReview { BookId = book.BookId, Rating = 5 }
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddReview(BookDetailsViewModel model)
        {
            var bookId = model.NewReview.BookId;
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var book = _context.Books.Include(b => b.Reviews).FirstOrDefault(b => b.BookId == bookId && b.Status);
            if (book == null)
            {
                return NotFound();
            }

            if (model.NewReview.Rating < 1 || model.NewReview.Rating > 5)
            {
                TempData["Error"] = "Please choose a rating from 1 to 5 stars.";
                return RedirectToAction("Details", new { id = bookId });
            }

            if (ModelState.IsValid)
            {
                var existing = book.Reviews.FirstOrDefault(r => r.UserId == userId.Value);
                if (existing != null)
                {
                    existing.Rating = model.NewReview.Rating;
                    existing.Comment = model.NewReview.Comment;
                    existing.CreatedAt = DateTime.Now;
                    _context.Reviews.Update(existing);
                }
                else
                {
                    var review = new Review
                    {
                        BookId = bookId,
                        UserId = userId.Value,
                        Rating = model.NewReview.Rating,
                        Comment = model.NewReview.Comment,
                        CreatedAt = DateTime.Now
                    };
                    _context.Reviews.Add(review);
                }
                _context.SaveChanges();
                return RedirectToAction("Details", new { id = bookId });
            }

            return RedirectToAction("Details", new { id = bookId });
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (claim != null && int.TryParse(claim.Value, out int id))
            {
                return id;
            }
            return null;
        }
    }
}
