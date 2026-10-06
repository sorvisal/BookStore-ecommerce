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
    [Authorize]
    public class CheckoutController : Controller
    {
        private const decimal FreeShippingThreshold = 30m;
        private readonly BookStoreDbContext _db;
        public CheckoutController(BookStoreDbContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<List<CartItem>> LoadLinesAsync(int uid)
        {
            var cart = await _db.ShoppingCarts
                .Include(c => c.CartItems)
                    .ThenInclude(i => i.Book)
                .FirstOrDefaultAsync(c => c.UserId == uid);
            return cart?.CartItems.Where(i => i.Book != null).ToList() ?? new List<CartItem>();
        }

        private async Task FillDisplayDataAsync(CheckoutViewModel vm, List<CartItem> lines)
        {
            vm.Items = lines.Select(i => new CheckoutLineViewModel
            {
                BookId = i.BookId,
                Title = i.Book!.Title,
                ImageUrl = !string.IsNullOrWhiteSpace(i.Book!.ImageUrl) ? i.Book!.ImageUrl : i.Book!.CoverImage,
                Quantity = i.Quantity,
                UnitPrice = Pricing.CurrentPrice(i.Book!),
                Subtotal = Pricing.CurrentPrice(i.Book!) * i.Quantity
            }).ToList();
            vm.ShippingMethods = await _db.ShippingMethods.AsNoTracking()
                .Where(s => s.Status).OrderBy(s => s.Price).ToListAsync();
            vm.PaymentMethods = await _db.PaymentMethods.AsNoTracking()
                .OrderBy(p => p.PaymentMethodId).ToListAsync();
            vm.Subtotal = vm.Items.Sum(i => i.Subtotal);
            var price = vm.ShippingMethods.FirstOrDefault(m => m.ShippingMethodId == vm.ShippingMethodId)?.Price
                        ?? vm.ShippingMethods.FirstOrDefault()?.Price ?? 0m;
            vm.ShippingFee = vm.Subtotal >= vm.FreeShippingThreshold ? 0m : price;
            vm.Total = vm.Subtotal + vm.ShippingFee;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var uid = CurrentUserId;
            var lines = await LoadLinesAsync(uid);
            if (lines.Count == 0)
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }
            if (lines.Any(i => !i.Book!.Status || i.Book!.StockQuantity <= 0 || i.Quantity > i.Book!.StockQuantity))
            {
                TempData["Error"] = "Some items in your cart are out of stock. Please review your cart.";
                return RedirectToAction("Index", "Cart");
            }

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == uid);
            var vm = new CheckoutViewModel
            {
                FullName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "",
                Phone = user?.Phone ?? "",
                ShippingAddress = user?.Address ?? ""
            };
            // Preselect the first shipping and payment method, then compute the display data
            var shippingMethods = await _db.ShippingMethods.AsNoTracking()
                .Where(s => s.Status).OrderBy(s => s.Price).ToListAsync();
            var paymentMethods = await _db.PaymentMethods.AsNoTracking()
                .OrderBy(p => p.PaymentMethodId).ToListAsync();
            vm.ShippingMethodId = shippingMethods.FirstOrDefault()?.ShippingMethodId;
            vm.PaymentMethodId = paymentMethods.FirstOrDefault()?.PaymentMethodId;
            await FillDisplayDataAsync(vm, lines);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CheckoutViewModel model)
        {
            var uid = CurrentUserId;
            var lines = await LoadLinesAsync(uid);

            // Re-validate on the server: never trust posted prices
            if (lines.Count == 0)
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }
            if (lines.Any(i => !i.Book!.Status || i.Book!.StockQuantity <= 0 || i.Quantity > i.Book!.StockQuantity))
            {
                TempData["Error"] = "Some items are out of stock or no longer available in the requested quantity.";
                return RedirectToAction("Index", "Cart");
            }

            var shipping = await _db.ShippingMethods
                .FirstOrDefaultAsync(s => s.ShippingMethodId == model.ShippingMethodId && s.Status);
            if (shipping == null)
                ModelState.AddModelError(nameof(model.ShippingMethodId), "Please choose a valid shipping method.");

            var payment = await _db.PaymentMethods
                .FirstOrDefaultAsync(p => p.PaymentMethodId == model.PaymentMethodId);
            if (payment == null)
                ModelState.AddModelError(nameof(model.PaymentMethodId), "Please choose a valid payment method.");

            if (!ModelState.IsValid)
            {
                await FillDisplayDataAsync(model, lines);
                return View(model);
            }

            // Recalculate everything from the database
            var subtotal = Math.Round(lines.Sum(i => Pricing.CurrentPrice(i.Book!) * i.Quantity), 2);
            var discount = 0m; // consistent with the seeded orders
            var shippingFee = subtotal >= FreeShippingThreshold ? 0m : (shipping!.Price ?? 0m);
            var tax = 0m;
            var grandTotal = Math.Round(subtotal - discount + shippingFee + tax, 2);

            // Cash on Delivery stays "Pending"; ABA Pay / Credit Card are simulated online payments
            var isCod = string.Equals(payment!.Type, "COD", StringComparison.OrdinalIgnoreCase)
                        || payment.MethodName.Contains("Cash", StringComparison.OrdinalIgnoreCase);

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.Now;
                var order = new Order
                {
                    OrderNumber = AppDbInitializer.GenerateOrderNumber(_db),
                    UserId = uid,
                    ShippingMethodId = shipping!.ShippingMethodId,
                    PaymentMethodId = payment!.PaymentMethodId,
                    OrderDate = now,
                    ReceiverName = model.FullName,
                    Phone = model.Phone,
                    ShippingAddress = model.ShippingAddress,
                    Notes = model.Note ?? "",
                    Subtotal = subtotal,
                    Discount = discount,
                    ShippingFee = shippingFee,
                    Tax = tax,
                    GrandTotal = grandTotal,
                    TotalAmount = grandTotal,
                    OrderStatus = "Pending",
                    Status = "Pending",
                    PaymentStatus = isCod ? "Unpaid" : "Paid",
                    CreatedAt = now
                };
                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                foreach (var line in lines)
                {
                    var book = line.Book!;
                    var price = Pricing.CurrentPrice(book);
                    _db.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.OrderId,
                        BookId = book.BookId,
                        Quantity = line.Quantity,
                        UnitPrice = price,
                        Discount = 0m,
                        Subtotal = Math.Round(price * line.Quantity, 2)
                    });
                    book.StockQuantity = Math.Max(0, book.StockQuantity - line.Quantity);
                }

                _db.Payments.Add(new Payment
                {
                    OrderId = order.OrderId,
                    PaymentMethodId = payment.PaymentMethodId,
                    Amount = grandTotal,
                    TransactionReference = isCod ? "" : $"TXN-{now:yyyyMMdd}-{order.OrderId:D4}",
                    QRCode = "",
                    PaidDate = isCod ? (DateTime?)null : now,
                    Status = isCod ? "Pending" : "Paid",
                    Remark = isCod ? "" : "Simulated online payment - no real payment gateway is connected."
                });

                _db.CartItems.RemoveRange(lines);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return RedirectToAction(nameof(Success), new { id = order.OrderId });
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Something went wrong while placing your order. Please try again.";
                return RedirectToAction("Index", "Cart");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Success(int id)
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
    }
}
