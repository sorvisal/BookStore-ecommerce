using System.Diagnostics;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly BookStoreDbContext _context;

        public HomeController(ILogger<HomeController> logger, BookStoreDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            var model = new HomeViewModel();

            model.FeaturedBooks = _context.Books
                .AsNoTracking()
                .Where(b => b.Status && b.StockQuantity >= 0)
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .Where(b => b.DiscountPrice.HasValue)
                .OrderByDescending(b => b.CreatedAt)
                .Take(8)
                .ToList();

            model.NewArrivals = _context.Books
                .AsNoTracking()
                .Where(b => b.Status)
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .OrderByDescending(b => b.CreatedAt)
                .Take(8)
                .ToList();

            model.BestSellers = _context.Books
                .AsNoTracking()
                .Where(b => b.Status)
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .Select(b => new
                {
                    Book = b,
                    TotalQty = b.OrderDetails
                        .Where(od => od.Order != null)
                        .Where(od => od.Order.OrderStatus != "Cancelled" && od.Order.Status != "Cancelled")
                        .Sum(od => od.Quantity)
                })
                .OrderByDescending(x => x.TotalQty)
                .ThenByDescending(x => x.Book.CreatedAt)
                .Take(8)
                .Select(x => x.Book)
                .ToList();

            model.Categories = _context.Categories
                .AsNoTracking()
                .Where(c => c.Status)
                .Select(c => new CategoryWithCount
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    BookCount = c.Books.Count(b => b.Status)
                })
                .OrderBy(c => c.CategoryName)
                .ToList();

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
