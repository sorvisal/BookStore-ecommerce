using System.ComponentModel.DataAnnotations;
using E_Commerce.Models;

namespace E_Commerce.Areas.Admin.ViewModels
{
    public class BookFormViewModel
    {
        public int? BookId { get; set; } // null on create

        [Required, StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [StringLength(50)]
        public string? ISBN { get; set; }

        public string? Description { get; set; }

        [StringLength(50)]
        public string? Language { get; set; }

        [Display(Name = "Publish year")]
        [Range(0, 3000, ErrorMessage = "Enter a valid year.")]
        public int? PublishYear { get; set; }

        [Display(Name = "Published date")]
        [DataType(DataType.Date)]
        public DateTime? PublishedDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Pages must be at least 1.")]
        public int? Pages { get; set; }

        [Required, Range(0.01, double.MaxValue, ErrorMessage = "Price must be above 0.")]
        public decimal Price { get; set; }

        [Display(Name = "Discount price")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The discount price must be above 0.")]
        public decimal? DiscountPrice { get; set; }

        [Display(Name = "Stock quantity")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
        public int StockQuantity { get; set; }

        [Display(Name = "Active (visible in the store)")]
        public bool Status { get; set; } = true;

        [Required(ErrorMessage = "Please choose a category.")]
        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Please choose a publisher.")]
        public int? PublisherId { get; set; }

        public List<int> SelectedAuthorIds { get; set; } = new();

        // Existing cover url (kept on edit when no new file is uploaded)
        public string? ImageUrl { get; set; }

        [Display(Name = "Cover image")]
        public IFormFile? CoverFile { get; set; }

        // ---- Display lists (not posted) ----
        public List<Category> Categories { get; set; } = new();
        public List<Publisher> Publishers { get; set; } = new();
        public List<Author> Authors { get; set; } = new();
    }
}
