using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class BookAuthor
    {
        [Key]
        public int BookAuthorId { get; set; }

        [Required]
        public int BookId { get; set; }

        [Required]
        public int AuthorId { get; set; }

        [ForeignKey(nameof(BookId))]
        public Book? Book { get; set; }

        [ForeignKey(nameof(AuthorId))]
        public Author? Author { get; set; }
    }
}
