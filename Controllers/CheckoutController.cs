using System.Globalization;
using System.Security.Claims;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.Services.Khqr;
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace E_Commerce.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        // Cached ABA QR payload per order, so refreshing the page shows the same QR.
        private const string AbaQrCachePrefix = "abaqr:";
        // Remembers when the QR for an order expires so the status endpoint can
        // report "expired" even after the QR payload itself has been evicted.
        private const string AbaExpiryCachePrefix = "abaexp:";
        // Server-side copy of the client_id|token verification pair. The payment
        // record column is capped at 255 chars, which the pair can exceed, so the
        // in-memory copy is the reliable source for a scheduled status check.
        private const string AbaVerifyCachePrefix = "abaverify:";
        // Separate throttles for normal polls and explicit "Check now" clicks, so a
        // forced check can bypass the poll cadence while still being rate limited.
        private const string AbaPollThrottlePrefix = "abapoll:";
        private const string AbaForceThrottlePrefix = "abaforce:";
        private static readonly TimeSpan AbaStatusThrottle = TimeSpan.FromSeconds(2);

        private readonly BookStoreDbContext _db;
        private readonly DynamicKhqrService _khqr;
        private readonly IMemoryCache _cache;
        private readonly AbaPaymentService _abaPayments;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            BookStoreDbContext db,
            DynamicKhqrService khqr,
            IMemoryCache cache,
            AbaPaymentService abaPayments,
            ILogger<CheckoutController> logger)
        {
            _db = db;
            _khqr = khqr;
            _cache = cache;
            _abaPayments = abaPayments;
            _logger = logger;
        }

        // Only Cash on Delivery and ABA Pay are offered at checkout. Card/Stripe methods are hidden.
        private static List<PaymentMethod> GetAvailablePaymentMethods(IEnumerable<PaymentMethod> methods) =>
            methods.Where(p =>
                !p.MethodName.Contains("Credit", StringComparison.OrdinalIgnoreCase) &&
                !p.MethodName.Contains("Card", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Type, "CARD", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Type, "STRIPE", StringComparison.OrdinalIgnoreCase) &&
                p.Status)
            .OrderBy(p => p.PaymentMethodId).ToList();

        private static bool IsAba(PaymentMethod? method) =>
            method != null &&
            (method.MethodName.Contains("ABA", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(method.Type, "ABA", StringComparison.OrdinalIgnoreCase));

        private static bool IsPaid(Order order) =>
            PaymentStatusHelper.IsOrderPaid(order.PaymentStatus, order.Payment?.Status);

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
            vm.PaymentMethods = GetAvailablePaymentMethods(await _db.PaymentMethods.AsNoTracking().ToListAsync());
            var price = vm.ShippingMethods.FirstOrDefault(m => m.ShippingMethodId == vm.ShippingMethodId)?.Price
                        ?? vm.ShippingMethods.FirstOrDefault()?.Price ?? 0m;
            var totals = OrderTotals.FromSubtotal(vm.Items.Sum(i => i.Subtotal), price);
            vm.Subtotal = totals.Subtotal;
            vm.ShippingFee = totals.ShippingFee;
            vm.Total = totals.GrandTotal;
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
            var paymentMethods = GetAvailablePaymentMethods(await _db.PaymentMethods.AsNoTracking().ToListAsync());
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

            var payment = GetAvailablePaymentMethods(await _db.PaymentMethods.ToListAsync())
                .FirstOrDefault(p => p.PaymentMethodId == model.PaymentMethodId);
            if (payment == null)
                ModelState.AddModelError(nameof(model.PaymentMethodId), "Please choose a valid payment method.");

            if (!ModelState.IsValid)
            {
                await FillDisplayDataAsync(model, lines);
                return View(model);
            }

            // Recalculate everything from the database
            var totals = OrderTotals.Compute(lines, shipping!.Price ?? 0m);
            var subtotal = totals.Subtotal;
            var discount = totals.Discount;
            var shippingFee = totals.ShippingFee;
            var tax = totals.Tax;
            var grandTotal = totals.GrandTotal;

            // No gateway charges the card instantly. Cash on Delivery stays unpaid
            // until delivery, and ABA Pay stays unpaid until the payment is
            // confirmed (automatically in live mode, or by an admin otherwise).
            var isAba = IsAba(payment);

            var createdOrderId = 0;
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
                    PaymentStatus = "Unpaid",
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
                        Subtotal = OrderTotals.Round(price * line.Quantity)
                    });
                    book.StockQuantity = Math.Max(0, book.StockQuantity - line.Quantity);
                }

                _db.Payments.Add(new Payment
                {
                    OrderId = order.OrderId,
                    PaymentMethodId = payment.PaymentMethodId,
                    Amount = grandTotal,
                    TransactionReference = "",
                    QRCode = "",
                    PaidDate = null,
                    Status = "Pending",
                    Remark = isAba ? "Awaiting ABA payment." : ""
                });

                _db.CartItems.RemoveRange(lines);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                createdOrderId = order.OrderId;
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Something went wrong while placing your order. Please try again.";
                return RedirectToAction("Index", "Cart");
            }

            return isAba
                ? RedirectToAction(nameof(AbaPay), new { id = createdOrderId })
                : RedirectToAction(nameof(Success), new { id = createdOrderId });
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

        /// <summary>
        /// Shows the KHQR for exactly this order's grand total. The QR is generated
        /// on the server and cached per order so a refresh shows the same code. When
        /// no ABA settings are configured, a static QR is shown in manual mode and
        /// the order waits for an admin to confirm the payment.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AbaPay(int id)
        {
            var uid = CurrentUserId;
            var order = await _db.Orders
                .Include(o => o.Payment).ThenInclude(p => p!.PaymentMethod)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == uid);
            if (order == null) return NotFound();

            // Already settled, or not an ABA order: nothing to pay here.
            if (IsPaid(order) || !IsAba(order.Payment?.PaymentMethod))
                return RedirectToAction(nameof(Success), new { id });

            var amount = OrderTotals.Round(order.GrandTotal ?? order.Payment?.Amount ?? 0m);
            var vm = new AbaPayViewModel
            {
                OrderId = order.OrderId,
                OrderNumber = order.OrderNumber ?? ("#" + order.OrderId),
                Amount = amount,
                Currency = "USD"
            };

            DynamicKhqrResult? result = await GetOrCreateQrAsync(order, amount);
            if (result is not null)
            {
                _logger.LogInformation(
                    "ABA QR for order {OrderId}: mode={Mode}, clientIdFound={ClientIdFound}, tokenFound={TokenFound}, canVerify={CanVerify}.",
                    order.OrderId,
                    string.IsNullOrEmpty(result.Mode) ? "-" : result.Mode,
                    !string.IsNullOrEmpty(result.ClientId),
                    !string.IsNullOrEmpty(result.Token),
                    result.CanVerifyPayment);
            }
            else
            {
                _logger.LogWarning(
                    "ABA QR for order {OrderId}: generation returned no result (falling back to manual mode).",
                    order.OrderId);
            }

            if (result is { IsConfigured: true })
            {
                vm.IsConfigured = true;
                vm.Mode = result.Mode;
                vm.CanVerifyPayment = result.CanVerifyPayment;
                vm.QrString = result.QrString;
                vm.DeepLink = result.DeepLink;
                vm.QrImageSource = string.IsNullOrEmpty(result.QrImageDataUri)
                    ? "/images/aba-qr.png"
                    : result.QrImageDataUri;
                vm.ExpiresAtUtc = result.ExpiresAtUtc;
            }
            else
            {
                // Manual mode: show the static QR and let an admin confirm.
                vm.IsConfigured = false;
                vm.CanVerifyPayment = false;
                vm.QrImageSource = "/images/aba-qr.png";
                vm.ErrorMessage = result?.ErrorMessage;
            }

            return View(vm);
        }

        /// <summary>
        /// Polled by the ABA Pay page. Returns the current payment state as
        /// <c>{ status, paidAtText, redirectUrl }</c> where status is one of
        /// "paid", "pending", "expired", or "cancelled".
        ///
        /// The database is always checked first. In live mode the ABA API is queried
        /// at most once per order every two seconds; an explicit <c>?force=1</c> (the
        /// "Check payment now" button) bypasses the poll throttle but is itself rate
        /// limited to once every two seconds. In manual mode ABA is never called and
        /// only the database decides. A payment approved just before the QR expires
        /// still counts: one final ABA check is made before returning "expired".
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AbaStatus(int id, int force = 0)
        {
            Response.Headers["Cache-Control"] = "no-store";

            var uid = CurrentUserId;
            var order = await _db.Orders.AsNoTracking()
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == uid);
            if (order == null) return NotFound();

            string? successUrl = Url.Action(nameof(Success), new { id });

            // (1) The database is the source of truth: a confirmed payment is final.
            if (IsPaid(order))
            {
                return Json(PaidPayload(order, successUrl));
            }

            // (2) Verifying automatically needs the server-side client_id/token pair,
            //     which is only stored when a live QR was produced.
            string? compact = await _db.Payments.AsNoTracking()
                .Where(p => p.OrderId == id)
                .Select(p => p.QRCode)
                .FirstOrDefaultAsync();
            if (string.IsNullOrEmpty(compact) || !compact.Contains('|'))
            {
                // Fall back to the in-memory copy when the payment record could not
                // hold the pair (the column is limited to 255 characters).
                if (_cache.TryGetValue(AbaVerifyCachePrefix + id, out string? cachedCompact))
                {
                    compact = cachedCompact;
                }
            }

            bool canVerify = !string.IsNullOrEmpty(compact) && compact.Contains('|');
            if (!canVerify)
            {
                _logger.LogInformation(
                    "ABA status for order {OrderId}: manual mode, decision=pending.",
                    id);
                return Json(new
                {
                    status = "pending",
                    paidAtText = (string?)null,
                    redirectUrl = successUrl
                });
            }

            bool forceCheck = force == 1;
            bool isExpired = HasQrExpired(id);

            // (3) Throttle ABA calls: at most one per order every two seconds. A forced
            //     check bypasses the poll throttle but is throttled on its own. A QR that
            //     has just expired is still checked once more before we report "expired".
            string throttleKey = (forceCheck ? AbaForceThrottlePrefix : AbaPollThrottlePrefix) + id;
            bool throttled = _cache.TryGetValue(throttleKey, out bool _);
            if (throttled && !(isExpired && !forceCheck))
            {
                string throttledStatus = isExpired ? "expired" : "pending";
                _logger.LogInformation(
                    "ABA status for order {OrderId}: throttled, decision={Decision}.",
                    id,
                    throttledStatus);
                return Json(new
                {
                    status = throttledStatus,
                    paidAtText = (string?)null,
                    redirectUrl = successUrl
                });
            }

            _cache.Set(throttleKey, true, AbaStatusThrottle);

            string[] parts = compact!.Split('|', 2);
            DynamicKhqrCheckResult check = await _khqr.CheckAsync(parts[0], parts[1]);

            // Strict: the ONLY way the ABA check can mark an order paid is an exact
            // "data.action == approved" response. Everything else stays pending (or
            // expired once the locally tracked QR lifetime has elapsed).
            string decision = check.Approved
                ? "paid"
                : isExpired ? "expired" : "pending";

            _logger.LogInformation(
                "ABA status for order {OrderId}: http={HttpStatus}, action={Action}, code={StatusCode}, message={Message}, decision={Decision}.",
                id,
                check.HttpStatus?.ToString(CultureInfo.InvariantCulture) ?? "-",
                check.Action ?? "-",
                check.StatusCode ?? "-",
                string.IsNullOrEmpty(check.Message) ? "-" : check.Message,
                decision);

            if (check.Approved)
            {
                await _abaPayments.MarkAbaPaidAsync(id, AbaPaidSource.Aba);
                _cache.Remove(AbaQrCachePrefix + id);
                _cache.Remove(AbaExpiryCachePrefix + id);
                _cache.Remove(AbaVerifyCachePrefix + id);

                var paidOrder = await _db.Orders.AsNoTracking()
                    .Include(o => o.Payment)
                    .FirstOrDefaultAsync(o => o.OrderId == id) ?? order;
                return Json(PaidPayload(paidOrder, successUrl));
            }

            return Json(new
            {
                status = decision,
                paidAtText = (string?)null,
                redirectUrl = successUrl
            });
        }

        private static object PaidPayload(Order order, string? redirectUrl)
        {
            string? paidAtText = order.Payment?.PaidDate is { } paid
                ? paid.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture)
                : null;

            return new
            {
                status = "paid",
                paidAtText,
                redirectUrl
            };
        }

        private bool HasQrExpired(int id) =>
            _cache.TryGetValue(AbaExpiryCachePrefix + id, out DateTimeOffset expires) &&
            expires <= DateTimeOffset.UtcNow;

        private async Task<DynamicKhqrResult?> GetOrCreateQrAsync(Order order, decimal amount)
        {
            string cacheKey = AbaQrCachePrefix + order.OrderId;
            if (_cache.TryGetValue(cacheKey, out DynamicKhqrResult? cached) && cached is not null)
                return cached;

            DynamicKhqrResult result;
            try
            {
                result = await _khqr.GenerateAsync(new DynamicKhqrRequest
                {
                    TranId = order.OrderNumber,
                    Amount = amount,
                    Currency = "USD",
                    CustomerName = order.ReceiverName,
                    ExpiresInMinutes = 10
                });
            }
            catch
            {
                return null;
            }

            if (!result.IsConfigured) return result;

            TimeSpan ttl = result.ExpiresAtUtc is { } expires && expires > DateTimeOffset.UtcNow
                ? expires - DateTimeOffset.UtcNow
                : TimeSpan.FromMinutes(10);
            _cache.Set(cacheKey, result, ttl);

            // Keep the expiry timestamp a little longer than the QR itself so the
            // status endpoint can still report "expired" after the QR is evicted.
            if (result.ExpiresAtUtc is { } expiry)
            {
                _cache.Set(AbaExpiryCachePrefix + order.OrderId, expiry, expiry.AddMinutes(5));
            }

            // Keep a compact copy of the verification pair so the status check can
            // confirm the payment later. Server-side only; never sent to the browser.
            if (result.CanVerifyPayment && !string.IsNullOrEmpty(result.ClientId) && !string.IsNullOrEmpty(result.Token))
            {
                string compact = $"{result.ClientId}|{result.Token}";

                // In-memory copy lasts for the QR lifetime plus a small buffer so a
                // check can still be made the instant the QR expires. This does not
                // depend on the 255-char payment record column.
                TimeSpan verifyTtl = result.ExpiresAtUtc is { } verifyExpiry && verifyExpiry > DateTimeOffset.UtcNow
                    ? verifyExpiry - DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5)
                    : TimeSpan.FromMinutes(15);
                _cache.Set(AbaVerifyCachePrefix + order.OrderId, compact, verifyTtl);

                // Best-effort copy in the payment record too, when it fits.
                var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == order.OrderId);
                if (payment != null && compact.Length <= 255 && payment.QRCode != compact)
                {
                    payment.QRCode = compact;
                    await _db.SaveChangesAsync();
                }
            }

            return result;
        }

    }
}
