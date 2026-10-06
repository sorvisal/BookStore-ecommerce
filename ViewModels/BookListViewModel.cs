using E_Commerce.Models;

namespace E_Commerce.ViewModels
{
    public class BookListViewModel
    {
        public List<Book> Books { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public string? SortBy { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }
}
