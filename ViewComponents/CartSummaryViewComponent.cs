using E_Commerce.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_Commerce.ViewComponents
{
    public class CartSummaryViewComponent : ViewComponent
    {
        private readonly BookStoreDbContext _context;

        public CartSummaryViewComponent(BookStoreDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = GetCurrentUserId();
            int count = 0;
            if (userId.HasValue)
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.CartItems)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UserId == userId.Value);
                if (cart != null)
                {
                    count = cart.CartItems.Sum(i => i.Quantity);
                }
            }
            return View(count);
        }

        private int? GetCurrentUserId()
        {
            var claim = UserClaimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier) ?? UserClaimsPrincipal?.FindFirst("UserId");
            if (claim != null && int.TryParse(claim.Value, out int id))
            {
                return id;
            }
            return null;
        }
    }
}
