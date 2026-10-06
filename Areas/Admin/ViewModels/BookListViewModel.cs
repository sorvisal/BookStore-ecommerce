using E_Commerce.Models;

namespace E_Commerce.Areas.Admin.ViewModels
{
    public class BookListViewModel
    {
        public List<Book> Books { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public string? Stock { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
    }
}
