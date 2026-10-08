using E_Commerce.Models;

namespace E_Commerce.ViewModels
{
    public class HomeViewModel
    {
        public List<Book> FeaturedBooks { get; set; } = new();
        public List<Book> NewArrivals { get; set; } = new();
        public List<CategoryWithCount> Categories { get; set; } = new();
    }

    public class CategoryWithCount
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int BookCount { get; set; }
    }
}
