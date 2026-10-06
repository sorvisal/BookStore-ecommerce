using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Controllers
{
    public class CartController : Controller
    {
        private readonly BookStoreDbContext _db;
        public CartController(BookStoreDbContext db) => _db = db;

        private int? CurrentUserId
        {
            get
            {
                var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId");
                return int.TryParse(claim, out int id) ? id : (int?)null;
            }
        }

        // Referer header as a LOCAL url (path + query), null when missing or external.
        private string? GetLocalReferer()
        {
            var referer = Request.Headers.Referer.ToString();
            if (string.IsNullOrEmpty(referer)) return null;
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                if (!string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)) return null;
                var local = uri.PathAndQuery;
                return Url.IsLocalUrl(local) ? local : null;
            }
            return Url.IsLocalUrl(referer) ? referer : null;
        }

        private IActionResult RedirectBack()
        {
            var back = GetLocalReferer();
            return back != null ? LocalRedirect(back) : RedirectToAction(nameof(Index));
        }

        private async Task<ShoppingCart> GetOrCreateCartAsync(int userId)
        {
            var cart = await _db.ShoppingCarts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cart == null)
            {
                cart = new ShoppingCart { UserId = userId };
                _db.ShoppingCarts.Add(cart);
                await _db.SaveChangesAsync();
            }
            return cart;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var uid = CurrentUserId!.Value;
            var cart = await _db.ShoppingCarts.AsNoTracking()
                .Include(c => c.CartItems)
                    .ThenInclude(i => i.Book)
                        .ThenInclude(b => b!.Category)
                .FirstOrDefaultAsync(c => c.UserId == uid);

            if (cart == null)
            {
                cart = new ShoppingCart { UserId = uid };
                _db.ShoppingCarts.Add(cart);
                await _db.SaveChangesAsync();
            }

            var vm = new CartViewModel();
            foreach (var item in cart.CartItems)
            {
                var book = item.Book;
                if (book == null) continue;

                var price = Pricing.CurrentPrice(book);
                var qty = item.Quantity;
                if (!book.Status || book.StockQuantity < qty)
                {
                    vm.HasStockIssue = true;
                    qty = Math.Max(0, Math.Min(qty, book.StockQuantity));
                }

                vm.Items.Add(new CartLineViewModel
                {
                    CartItemId = item.CartItemId,
                    BookId = book.BookId,
                    Title = book.Title,
                    ImageUrl = !string.IsNullOrWhiteSpace(book.ImageUrl) ? book.ImageUrl : book.CoverImage,
                    CategoryName = book.Category?.CategoryName ?? "",
                    UnitPrice = price,
                    OldPrice = price < book.Price ? book.Price : (decimal?)null,
                    Quantity = qty,
                    MaxStock = book.StockQuantity,
                    Subtotal = price * qty
                });
            }
            vm.Subtotal = vm.Items.Sum(i => i.Subtotal);
            vm.ItemCount = vm.Items.Sum(i => i.Quantity);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
        public async Task<IActionResult> Add(int bookId, int quantity = 1, string? returnUrl = null)
        {
            var uid = CurrentUserId;
            if (uid == null)
            {
                TempData["Error"] = "Please log in to add books to your cart.";
                return RedirectToAction("Login", "Account", new { returnUrl = GetLocalReferer() });
            }

            var book = await _db.Books.FindAsync(bookId);
            if (book == null) return NotFound();

            if (!book.Status || book.StockQuantity <= 0)
            {
                TempData["Error"] = "Sorry, this book is out of stock.";
                return RedirectBack();
            }

            if (quantity < 1) quantity = 1;

            var cart = await GetOrCreateCartAsync(uid.Value);
            var price = Pricing.CurrentPrice(book);
            var existing = cart.CartItems.FirstOrDefault(i => i.BookId == bookId);
            if (existing != null)
            {
                existing.Quantity = Math.Min(existing.Quantity + quantity, book.StockQuantity);
                existing.UnitPrice = price;
                existing.Subtotal = price * existing.Quantity;
            }
            else
            {
                var qty = Math.Min(quantity, book.StockQuantity);
                cart.CartItems.Add(new CartItem
                {
                    CartId = cart.CartId,
                    BookId = book.BookId,
                    Quantity = qty,
                    UnitPrice = price,
                    Subtotal = price * qty
                });
            }
            await _db.SaveChangesAsync();

            TempData["Success"] = "Added to cart.";
            return RedirectBack();
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int cartItemId, int quantity)
        {
            var uid = CurrentUserId!.Value;
            var item = await _db.CartItems
                .Include(i => i.ShoppingCart)
                .Include(i => i.Book)
                .FirstOrDefaultAsync(i => i.CartItemId == cartItemId && i.ShoppingCart!.UserId == uid);
            if (item == null) return NotFound();

            var stock = item.Book?.StockQuantity ?? 0;
            if (quantity < 1)
            {
                _db.CartItems.Remove(item);
                TempData["Success"] = "Item removed from cart.";
            }
            else if (stock <= 0)
            {
                _db.CartItems.Remove(item);
                TempData["Error"] = "This book is out of stock and was removed from your cart.";
            }
            else
            {
                if (quantity > stock)
                {
                    quantity = stock;
                    TempData["Error"] = $"Only {stock} left in stock. Quantity adjusted.";
                }
                var price = item.Book != null ? Pricing.CurrentPrice(item.Book) : (item.UnitPrice ?? 0m);
                item.Quantity = quantity;
                item.UnitPrice = price;
                item.Subtotal = price * quantity;
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int cartItemId)
        {
            var uid = CurrentUserId!.Value;
            var item = await _db.CartItems
                .Include(i => i.ShoppingCart)
                .FirstOrDefaultAsync(i => i.CartItemId == cartItemId && i.ShoppingCart!.UserId == uid);
            if (item == null) return NotFound();

            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Item removed from cart.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            var uid = CurrentUserId!.Value;
            var items = await _db.CartItems
                .Where(i => i.ShoppingCart!.UserId == uid)
                .ToListAsync();
            if (items.Count > 0)
            {
                _db.CartItems.RemoveRange(items);
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Cart cleared.";
            return RedirectToAction(nameof(Index));
        }
    }
}
