using E_Commerce.Models;

namespace E_Commerce.ViewModels
{
    public class OrderListViewModel
    {
        public List<Order> Orders { get; set; } = new();
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }
}