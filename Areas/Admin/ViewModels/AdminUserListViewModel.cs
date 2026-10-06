using E_Commerce.ViewModels;

namespace E_Commerce.Areas.Admin.ViewModels
{
    public class AdminUserListViewModel
    {
        public List<AdminUserRowViewModel> Items { get; set; } = new();
        public string? Search { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalUsers { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalInactive { get; set; }
    }

    public class AdminUserRowViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = "Customer";
        public bool Status { get; set; }
        public AvatarViewModel Avatar { get; set; } = new();
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
