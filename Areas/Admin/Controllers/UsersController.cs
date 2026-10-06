using System.Security.Claims;
using E_Commerce.Areas.Admin.ViewModels;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private const int PageSize = 10;
        private static readonly string[] Roles = { "Admin", "Customer" };

        private readonly BookStoreDbContext _db;
        private readonly IPasswordHasher<User> _hasher;
        private readonly IWebHostEnvironment _env;

        public UsersController(BookStoreDbContext db, IPasswordHasher<User> hasher, IWebHostEnvironment env)
        {
            _db = db;
            _hasher = hasher;
            _env = env;
        }

        private int CurrentAdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // True when this user is the ONLY active admin left
        private async Task<bool> IsLastActiveAdminAsync(int userId)
            => !await _db.Users.AnyAsync(u => u.Role == "Admin" && u.Status && u.UserId != userId);

        // ---------------------------------------------------------------- Index
        public async Task<IActionResult> Index(string? search, string? role, string? status, int page = 1)
        {
            var q = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(u => (u.FirstName + " " + u.LastName).Contains(s)
                    || u.Email.Contains(s)
                    || (u.Phone != null && u.Phone.Contains(s)));
            }
            if (!string.IsNullOrWhiteSpace(role) && Roles.Contains(role))
                q = q.Where(u => u.Role == role);
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "active") q = q.Where(u => u.Status);
                else if (status == "inactive") q = q.Where(u => !u.Status);
            }

            var total = await q.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var users = await q.OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

            var ids = users.Select(u => u.UserId).ToList();
            var agg = await _db.Orders.AsNoTracking()
                .Where(o => ids.Contains(o.UserId) && o.Status != "Cancelled")
                .GroupBy(o => o.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), Spent = g.Sum(o => o.GrandTotal ?? 0m) })
                .ToListAsync();

            var vm = new AdminUserListViewModel
            {
                Items = users.Select(u =>
                {
                    var a = agg.FirstOrDefault(x => x.UserId == u.UserId);
                    var fullName = $"{u.FirstName} {u.LastName}".Trim();
                    return new AdminUserRowViewModel
                    {
                        UserId = u.UserId,
                        FullName = fullName,
                        Email = u.Email,
                        Phone = u.Phone,
                        Role = u.Role,
                        Status = u.Status,
                        Avatar = new AvatarViewModel
                        {
                            ImageUrl = AvatarHelper.ResolveUrl(u.ProfileImage),
                            Initials = AvatarHelper.Initials(fullName),
                            CssClass = "avatar-40"
                        },
                        OrderCount = a?.Count ?? 0,
                        TotalSpent = a?.Spent ?? 0m,
                        CreatedAt = u.CreatedAt
                    };
                }).ToList(),
                Search = search,
                Role = role,
                Status = status,
                Page = page,
                TotalPages = totalPages,
                TotalUsers = await _db.Users.CountAsync(),
                TotalAdmins = await _db.Users.CountAsync(u => u.Role == "Admin"),
                TotalCustomers = await _db.Users.CountAsync(u => u.Role == "Customer"),
                TotalInactive = await _db.Users.CountAsync(u => !u.Status)
            };
            return View(vm);
        }

        // ---------------------------------------------------------------- Create
        public IActionResult Create() => View(new UserFormViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            await ValidateUserFormAsync(model, requirePassword: true);
            if (!ModelState.IsValid) return View(model);

            var user = new User
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = model.Email.Trim(),
                Phone = model.Phone,
                Gender = model.Gender,
                DateOfBirth = model.DateOfBirth,
                Address = model.Address,
                Role = model.Role,
                Status = model.Status,
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = _hasher.HashPassword(user, model.Password!);

            var photoError = await SavePhotoAsync(model, user, null);
            if (photoError != null)
            {
                ModelState.AddModelError(nameof(model.Photo), photoError);
                return View(model);
            }

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Registration creates an empty cart; do the same here
            _db.ShoppingCarts.Add(new ShoppingCart { UserId = user.UserId, CreatedAt = DateTime.Now });
            await _db.SaveChangesAsync();

            TempData["Success"] = "User created.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------------------------------------------------------- Edit
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View(ToForm(user));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserFormViewModel model)
        {
            if (id != model.UserId) return NotFound();
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            await ValidateUserFormAsync(model, requirePassword: false);

            var adminId = CurrentAdminId;
            var demote = user.Role == "Admin" && model.Role != "Admin";
            var deactivate = user.Status && !model.Status;

            if (id == adminId && (demote || deactivate))
                ModelState.AddModelError(string.Empty, "You cannot demote or deactivate your own account.");
            else if (user.Role == "Admin" && (demote || deactivate) && await IsLastActiveAdminAsync(id))
                ModelState.AddModelError(string.Empty, "The last active admin cannot be demoted or deactivated.");

            if (!ModelState.IsValid)
            {
                model.Avatar = ToForm(user).Avatar;
                model.ProfileImage = user.ProfileImage;
                return View(model);
            }

            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();
            user.Email = model.Email.Trim();
            user.Phone = model.Phone;
            user.Gender = model.Gender;
            user.DateOfBirth = model.DateOfBirth;
            user.Address = model.Address;
            user.Role = model.Role;
            user.Status = model.Status;
            user.UpdatedAt = DateTime.Now;

            if (!string.IsNullOrEmpty(model.Password))
                user.PasswordHash = _hasher.HashPassword(user, model.Password);

            var photoError = await SavePhotoAsync(model, user, user.ProfileImage);
            if (photoError != null)
            {
                ModelState.AddModelError(nameof(model.Photo), photoError);
                model.Avatar = ToForm(user).Avatar;
                model.ProfileImage = user.ProfileImage;
                return View(model);
            }

            await _db.SaveChangesAsync();

            // If an admin edits their OWN profile, refresh their sign-in claims
            if (id == adminId) await RefreshSignInAsync(user);

            TempData["Success"] = "User updated.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------------------------------------------------------- ToggleStatus
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var adminId = CurrentAdminId;
            if (id == adminId)
            {
                TempData["Error"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Status && user.Role == "Admin" && await IsLastActiveAdminAsync(id))
            {
                TempData["Error"] = "The last active admin cannot be deactivated.";
                return RedirectToAction(nameof(Index));
            }

            user.Status = !user.Status;
            user.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["Success"] = user.Status
                ? $"{user.FirstName}'s account has been activated."
                : $"{user.FirstName}'s account has been deactivated.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------------------------------------------------------- ResetPassword
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                TempData["Error"] = "The new password must be at least 6 characters.";
                return RedirectToAction(nameof(Index));
            }

            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            user.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Password reset for {user.FirstName} {user.LastName}.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------------------------------------------------------- Delete
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var adminId = CurrentAdminId;
            if (id == adminId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Role == "Admin" && user.Status && await IsLastActiveAdminAsync(id))
            {
                TempData["Error"] = "The last active admin cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (await _db.Orders.AnyAsync(o => o.UserId == id))
            {
                TempData["Error"] = "This user has orders and cannot be deleted. Set the account to inactive instead.";
                return RedirectToAction(nameof(Index));
            }

            var cart = await _db.ShoppingCarts.Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == id);
            if (cart != null)
            {
                _db.CartItems.RemoveRange(cart.CartItems);
                _db.ShoppingCarts.Remove(cart);
            }
            _db.Reviews.RemoveRange(await _db.Reviews.Where(r => r.UserId == id).ToListAsync());
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            AvatarHelper.DeleteFile(_env, user.ProfileImage);
            TempData["Success"] = "User deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------------------------------------------------------- Helpers
        private async Task ValidateUserFormAsync(UserFormViewModel model, bool requirePassword)
        {
            var email = (model.Email ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(email))
                ModelState.AddModelError(nameof(model.Email), "Email is required.");
            else if (await _db.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower()
                        && u.UserId != (model.UserId ?? 0)))
                ModelState.AddModelError(nameof(model.Email), "This email is already used by another account.");

            if (!Roles.Contains(model.Role))
                ModelState.AddModelError(nameof(model.Role), "Please choose a valid role.");

            if (requirePassword && string.IsNullOrEmpty(model.Password))
                ModelState.AddModelError(nameof(model.Password), "Password is required.");
        }

        private async Task<string?> SavePhotoAsync(UserFormViewModel model, User user, string? oldUrl)
        {
            if (model.RemovePhoto && model.Photo == null)
            {
                AvatarHelper.DeleteFile(_env, oldUrl);
                user.ProfileImage = null;
                return null;
            }
            if (model.Photo == null || model.Photo.Length == 0) return null;

            var (url, error) = await AvatarHelper.SaveAsync(_env, model.Photo, oldUrl);
            if (error == null) user.ProfileImage = url;
            return error;
        }

        private UserFormViewModel ToForm(User user)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            return new UserFormViewModel
            {
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                Address = user.Address,
                Role = user.Role,
                Status = user.Status,
                ProfileImage = user.ProfileImage,
                Avatar = new AvatarViewModel
                {
                    ImageUrl = AvatarHelper.ResolveUrl(user.ProfileImage),
                    Initials = AvatarHelper.Initials(fullName),
                    CssClass = "avatar-120"
                }
            };
        }

        private async Task RefreshSignInAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? "Customer" : user.Role),
                new Claim("ProfileImage", user.ProfileImage ?? string.Empty)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = auth?.Properties?.IsPersistent ?? false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });
        }
    }
}
