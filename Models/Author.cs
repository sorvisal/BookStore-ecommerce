using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class Author
    {
        [Key]
        public int AuthorId {  get; set; }
        [Required, StringLength(150)]
        public string FullName { get; set; } = string.Empty;
        public string? Biography { get; set; }
        [StringLength(100)]
        public string? Country { get; set; }
        [StringLength(255)]
        public string? Photo { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();

    }
}
