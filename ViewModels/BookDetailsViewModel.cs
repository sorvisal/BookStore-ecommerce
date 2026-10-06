using E_Commerce.Models;

namespace E_Commerce.ViewModels
{
    public class BookDetailsViewModel
    {
        public Book Book { get; set; } = null!;
        public List<Author> Authors { get; set; } = new();
        public double AverageRating { get; set; }
        public List<ReviewWithUser> Reviews { get; set; } = new();
        public List<Book> RelatedBooks { get; set; } = new();
        public bool CanReview { get; set; }
        public NewReview NewReview { get; set; } = new();
    }

    public class ReviewWithUser
    {
        public int ReviewId { get; set; }
        public int? Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    public class NewReview
    {
        public int BookId { get; set; }
        public int Rating { get; set; } = 5;
        public string? Comment { get; set; }
    }
}
