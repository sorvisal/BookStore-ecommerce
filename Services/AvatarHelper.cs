using E_Commerce.Models;

namespace E_Commerce.Services
{
    // Shared avatar utilities: resolve the stored ProfileImage value, build
    // initials, and save/delete uploaded photos under wwwroot/images/avatars.
    public static class AvatarHelper
    {
        public const string WebFolder = "/images/avatars/";
        private const long MaxPhotoBytes = 2 * 1024 * 1024; // 2 MB
        private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedMime = { "image/jpeg", "image/png", "image/webp" };

        // Only values under /images/avatars/ or absolute http(s) urls count as a
        // real image. Legacy values like "users/user1.jpg" mean "no image".
        public static string? ResolveUrl(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            if (stored.StartsWith(WebFolder, StringComparison.OrdinalIgnoreCase)) return stored;
            if (stored.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return stored;
            return null;
        }

        public static string Initials(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "?";
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Take(2).Select(p => char.ToUpper(p[0])));
        }

        // Saves a new avatar, deletes the replaced one and returns (newUrl, error).
        public static async Task<(string? Url, string? Error)> SaveAsync(
            IWebHostEnvironment env, IFormFile file, string? oldUrl)
        {
            if (file.Length == 0) return (null, "The file is empty.");
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext) || !AllowedMime.Contains(file.ContentType.ToLowerInvariant()))
                return (null, "Only JPG, PNG or WEBP images are allowed.");
            if (file.Length > MaxPhotoBytes)
                return (null, "The photo must be 2 MB or smaller.");

            var folder = Path.Combine(env.WebRootPath, "images", "avatars");
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid().ToString("N") + ext;
            var path = Path.Combine(folder, name);
            await using (var stream = System.IO.File.Create(path))
                await file.CopyToAsync(stream);

            DeleteFile(env, oldUrl);
            return (WebFolder + name, null);
        }

        // Deletes an avatar file only when it lives under /images/avatars/.
        public static void DeleteFile(IWebHostEnvironment env, string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith(WebFolder, StringComparison.OrdinalIgnoreCase)) return;
            var fileName = Path.GetFileName(url);
            if (string.IsNullOrWhiteSpace(fileName)) return;
            var path = Path.Combine(env.WebRootPath, "images", "avatars", fileName);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }
}
