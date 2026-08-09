using E_Commerce.Models;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Data
{
    public class AppDbInitializer
    {
        public static void Seed(BookStoreDbContext context)
        {
            // Ensure DB is up to date with migrations
            context.Database.Migrate();

            // ---------- Categories ----------
            if (!context.Categories.Any())
            {
                var categories = new List<Category>
                {
                    new Category { CategoryName = "Fiction",     Description = "Novels and fictional stories",       Image = "categories/fiction.jpg",    Status = true, CreatedAt = DateTime.Now },
                    new Category { CategoryName = "Non-Fiction",Description = "Fact-based and informative books",   Image = "categories/nonfiction.jpg", Status = true, CreatedAt = DateTime.Now },
                    new Category { CategoryName = "Technology", Description = "Programming and IT books",           Image = "categories/technology.jpg", Status = true, CreatedAt = DateTime.Now },
                    new Category { CategoryName = "History",    Description = "Historical events and biographies",  Image = "categories/history.jpg",    Status = true, CreatedAt = DateTime.Now },
                    new Category { CategoryName = "Children",   Description = "Books for children",                 Image = "categories/children.jpg",   Status = true, CreatedAt = DateTime.Now },
                };
                context.Categories.AddRange(categories);
                context.SaveChanges();
            }

            // ---------- Publishers ----------
            if (!context.Publishers.Any())
            {
                var publishers = new List<Publisher>
                {
                    new Publisher { PublisherName = "Penguin Books",  Phone = "0123456701", Email = "contact@penguin.com",      Website = "https://penguin.com",      Address = "New York, USA",  Description = "International publishing house",  CreatedAt = DateTime.Now },
                    new Publisher { PublisherName = "HarperCollins",  Phone = "0123456702", Email = "contact@harpercollins.com",Website = "https://harpercollins.com",Address = "New York, USA",  Description = "Publisher of fiction and non-fiction", CreatedAt = DateTime.Now },
                    new Publisher { PublisherName = "O'Reilly Media", Phone = "0123456703", Email = "contact@oreilly.com",      Website = "https://oreilly.com",      Address = "California, USA",Description = "Technology and programming books", CreatedAt = DateTime.Now },
                    new Publisher { PublisherName = "Macmillan",      Phone = "0123456704", Email = "contact@macmillan.com",    Website = "https://macmillan.com",    Address = "London, UK",     Description = "Global trade publisher", CreatedAt = DateTime.Now },
                    new Publisher { PublisherName = "Scholastic",     Phone = "0123456705", Email = "contact@scholastic.com",   Website = "https://scholastic.com",   Address = "New York, USA",  Description = "Children's book publisher", CreatedAt = DateTime.Now },
                };
                context.Publishers.AddRange(publishers);
                context.SaveChanges();
            }

            // ---------- Authors ----------
            if (!context.Authors.Any())
            {
                var authors = new List<Author>
                {
                    new Author { FullName = "George Orwell",    Biography = "English novelist known for dystopian fiction.",       Country = "United Kingdom", Photo = "authors/orwell.jpg",   CreatedAt = DateTime.Now },
                    new Author { FullName = "J.K. Rowling",     Biography = "British author, creator of the Harry Potter series.", Country = "United Kingdom", Photo = "authors/rowling.jpg",  CreatedAt = DateTime.Now },
                    new Author { FullName = "Robert C. Martin", Biography = "Software engineer and author on clean code.",         Country = "United States",  Photo = "authors/martin.jpg",   CreatedAt = DateTime.Now },
                    new Author { FullName = "Yuval Noah Harari",Biography = "Historian and author of Sapiens.",                    Country = "Israel",         Photo = "authors/harari.jpg",   CreatedAt = DateTime.Now },
                    new Author { FullName = "Andy Weir",        Biography = "American novelist known for science fiction.",        Country = "United States",  Photo = "authors/weir.jpg",     CreatedAt = DateTime.Now },
                };
                context.Authors.AddRange(authors);
                context.SaveChanges();
            }

            // ---------- Users ----------
            if (!context.Users.Any())
            {
                var users = new List<User>
                {
                    new User { FirstName = "Visal",  LastName = "Chan",  Gender = "Male",   Phone = "012345671", Email = "admin@bookstore.com", PasswordHash = "HASHED_PASSWORD_1", ProfileImage = "users/user1.jpg", Address = "Phnom Penh, Cambodia", Status = true, CreatedAt = DateTime.Now },
                    new User { FirstName = "Dara",   LastName = "Sok",   Gender = "Male",   Phone = "012345672", Email = "dara.sok@mail.com",   PasswordHash = "HASHED_PASSWORD_2", ProfileImage = "users/user2.jpg", Address = "Siem Reap, Cambodia",  Status = true, CreatedAt = DateTime.Now },
                    new User { FirstName = "Srey",   LastName = "Leang", Gender = "Female", Phone = "012345673", Email = "srey.leang@mail.com", PasswordHash = "HASHED_PASSWORD_3", ProfileImage = "users/user3.jpg", Address = "Battambang, Cambodia", Status = true, CreatedAt = DateTime.Now },
                    new User { FirstName = "Panha",  LastName = "Kim",   Gender = "Male",   Phone = "012345674", Email = "panha.kim@mail.com",  PasswordHash = "HASHED_PASSWORD_4", ProfileImage = "users/user4.jpg", Address = "Phnom Penh, Cambodia", Status = true, CreatedAt = DateTime.Now },
                    new User { FirstName = "Chenda", LastName = "Lim",   Gender = "Female", Phone = "012345675", Email = "chenda.lim@mail.com", PasswordHash = "HASHED_PASSWORD_5", ProfileImage = "users/user5.jpg", Address = "Kandal, Cambodia",     Status = true, CreatedAt = DateTime.Now },
                };
                context.Users.AddRange(users);
                context.SaveChanges();
            }

            // ---------- PaymentMethods ----------
            if (!context.PaymentMethods.Any())
            {
                var paymentMethods = new List<PaymentMethod>
                {
                    new PaymentMethod { MethodName = "Cash on Delivery", Type = "COD",    Description = "Pay when the order arrives", Status = true },
                    new PaymentMethod { MethodName = "Credit Card",      Type = "CARD",   Description = "Visa / MasterCard payment",  Status = true },
                    new PaymentMethod { MethodName = "ABA PayWay",       Type = "QR",     Description = "Pay via ABA QR code",        Status = true },
                    new PaymentMethod { MethodName = "Bank Transfer",    Type = "BANK",   Description = "Direct bank transfer",       Status = true },
                };
                context.PaymentMethods.AddRange(paymentMethods);
                context.SaveChanges();
            }

            // ---------- ShippingMethods ----------
            if (!context.ShippingMethods.Any())
            {
                var shippingMethods = new List<ShippingMethod>
                {
                    new ShippingMethod { MethodName = "Standard Shipping", Price = 2.50m, EstimatedDays = 5, Status = true },
                    new ShippingMethod { MethodName = "Express Shipping",  Price = 6.00m, EstimatedDays = 2, Status = true },
                    new ShippingMethod { MethodName = "Store Pickup",      Price = 0.00m, EstimatedDays = 0, Status = true },
                };
                context.ShippingMethods.AddRange(shippingMethods);
                context.SaveChanges();
            }

            // ---------- Books ----------
            if (!context.Books.Any())
            {
                var fiction = context.Categories.First(c => c.CategoryName == "Fiction");
                var tech = context.Categories.First(c => c.CategoryName == "Technology");
                var nonFiction = context.Categories.First(c => c.CategoryName == "Non-Fiction");
                var children = context.Categories.First(c => c.CategoryName == "Children");

                var penguin = context.Publishers.First(p => p.PublisherName == "Penguin Books");
                var harper = context.Publishers.First(p => p.PublisherName == "HarperCollins");
                var oreilly = context.Publishers.First(p => p.PublisherName == "O'Reilly Media");
                var macmillan = context.Publishers.First(p => p.PublisherName == "Macmillan");
                var scholastic = context.Publishers.First(p => p.PublisherName == "Scholastic");

                var books = new List<Book>
                {
                    new Book { CategoryId = fiction.CategoryId,    PublisherId = penguin.PublisherId,   Title = "1984",                     ISBN = "9780451524935", Description = "Dystopian social science fiction novel.", Language = "English", PublishYear = 1949, Pages = 328, Price = 12.99m, DiscountPrice = 9.99m, StockQuantity = 50, CoverImage = "books/1984.jpg",        Status = true, CreatedAt = DateTime.Now },
                    new Book { CategoryId = fiction.CategoryId,    PublisherId = harper.PublisherId,    Title = "Harry Potter and the Philosopher's Stone", ISBN = "9780747532699", Description = "A young wizard begins his journey at Hogwarts.", Language = "English", PublishYear = 1997, Pages = 223, Price = 15.99m, StockQuantity = 40, CoverImage = "books/hp1.jpg", Status = true, CreatedAt = DateTime.Now },
                    new Book { CategoryId = tech.CategoryId,       PublisherId = oreilly.PublisherId,   Title = "Clean Code",               ISBN = "9780132350884", Description = "A handbook of agile software craftsmanship.", Language = "English", PublishYear = 2008, Pages = 464, Price = 34.99m, DiscountPrice = 29.99m, StockQuantity = 25, CoverImage = "books/cleancode.jpg", Status = true, CreatedAt = DateTime.Now },
                    new Book { CategoryId = nonFiction.CategoryId, PublisherId = macmillan.PublisherId, Title = "Sapiens: A Brief History of Humankind", ISBN = "9780062316097", Description = "An exploration of the history of the human species.", Language = "English", PublishYear = 2011, Pages = 443, Price = 18.50m, StockQuantity = 35, CoverImage = "books/sapiens.jpg", Status = true, CreatedAt = DateTime.Now },
                    new Book { CategoryId = children.CategoryId,   PublisherId = scholastic.PublisherId,Title = "Charlotte's Web",          ISBN = "9780061124952", Description = "A children's story about friendship.", Language = "English", PublishYear = 1952, Pages = 184, Price = 8.99m, StockQuantity = 60, CoverImage = "books/charlottesweb.jpg", Status = true, CreatedAt = DateTime.Now },
                };
                context.Books.AddRange(books);
                context.SaveChanges();
            }

            // ---------- BookAuthors ----------
            if (!context.BookAuthors.Any())
            {
                var books = context.Books.ToList();
                var authors = context.Authors.ToList();

                var bookAuthors = new List<BookAuthor>
                {
                    new BookAuthor { BookId = books[0].BookId, AuthorId = authors[0].AuthorId }, // 1984 - Orwell
                    new BookAuthor { BookId = books[1].BookId, AuthorId = authors[1].AuthorId }, // HP - Rowling
                    new BookAuthor { BookId = books[2].BookId, AuthorId = authors[2].AuthorId }, // Clean Code - Martin
                    new BookAuthor { BookId = books[3].BookId, AuthorId = authors[3].AuthorId }, // Sapiens - Harari
                    new BookAuthor { BookId = books[4].BookId, AuthorId = authors[4].AuthorId }, // Charlotte's Web - Weir (placeholder pairing)
                };
                context.BookAuthors.AddRange(bookAuthors);
                context.SaveChanges();
            }

            // ---------- ShoppingCarts ----------
            if (!context.ShoppingCarts.Any())
            {
                var users = context.Users.ToList();
                var carts = users.Select(u => new ShoppingCart { UserId = u.UserId, CreatedAt = DateTime.Now }).ToList();
                context.ShoppingCarts.AddRange(carts);
                context.SaveChanges();
            }

            // ---------- CartItems ----------
            if (!context.CartItems.Any())
            {
                var carts = context.ShoppingCarts.ToList();
                var books = context.Books.ToList();

                var cartItems = new List<CartItem>
                {
                    new CartItem { CartId = carts[0].CartId, BookId = books[0].BookId, Quantity = 2, UnitPrice = books[0].Price, Subtotal = books[0].Price * 2 },
                    new CartItem { CartId = carts[1].CartId, BookId = books[2].BookId, Quantity = 1, UnitPrice = books[2].Price, Subtotal = books[2].Price },
                };
                context.CartItems.AddRange(cartItems);
                context.SaveChanges();
            }

            // ---------- Orders ----------
            if (!context.Orders.Any())
            {
                var users = context.Users.ToList();
                var shipping = context.ShippingMethods.ToList();
                var payment = context.PaymentMethods.ToList();

                var orders = new List<Order>
                {
                    new Order { OrderNumber = "ORD-0001", UserId = users[1].UserId, ShippingMethodId = shipping[0].ShippingMethodId, PaymentMethodId = payment[0].PaymentMethodId, ReceiverName = "Dara Sok", Phone = "012345672", Province = "Phnom Penh", District = "Chamkarmon", Commune = "Tonle Bassac", Village = "Village 1", Street = "Street 51", Subtotal = 19.98m, Discount = 0m, ShippingFee = 2.50m, Tax = 1.00m, GrandTotal = 23.48m, OrderStatus = "Pending", PaymentStatus = "Unpaid", Notes = "", CreatedAt = DateTime.Now },
                    new Order { OrderNumber = "ORD-0002", UserId = users[2].UserId, ShippingMethodId = shipping[1].ShippingMethodId, PaymentMethodId = payment[1].PaymentMethodId, ReceiverName = "Srey Leang", Phone = "012345673", Province = "Siem Reap", District = "Siem Reap", Commune = "Svay Dangkum", Village = "Village 2", Street = "Street 6", Subtotal = 34.99m, Discount = 3.00m, ShippingFee = 6.00m, Tax = 1.50m, GrandTotal = 39.49m, OrderStatus = "Processing", PaymentStatus = "Paid", Notes = "", CreatedAt = DateTime.Now },
                };
                context.Orders.AddRange(orders);
                context.SaveChanges();
            }

            // ---------- OrderDetails ----------
            if (!context.OrderDetails.Any())
            {
                var orders = context.Orders.ToList();
                var books = context.Books.ToList();

                var orderDetails = new List<OrderDetail>
                {
                    new OrderDetail { OrderId = orders[0].OrderId, BookId = books[0].BookId, Quantity = 2, UnitPrice = books[0].Price, Discount = 0m, Subtotal = books[0].Price * 2 },
                    new OrderDetail { OrderId = orders[1].OrderId, BookId = books[2].BookId, Quantity = 1, UnitPrice = books[2].Price, Discount = 3.00m, Subtotal = books[2].Price - 3.00m },
                };
                context.OrderDetails.AddRange(orderDetails);
                context.SaveChanges();
            }

            // ---------- Payments ----------
            if (!context.Payments.Any())
            {
                var orders = context.Orders.ToList();
                var payment = context.PaymentMethods.ToList();

                var payments = new List<Payment>
                {
                    new Payment { OrderId = orders[0].OrderId, PaymentMethodId = payment[0].PaymentMethodId, Amount = 23.48m, TransactionReference = "TXN-000001", QRCode = "", Status = "Pending", Remark = "" },
                    new Payment { OrderId = orders[1].OrderId, PaymentMethodId = payment[1].PaymentMethodId, Amount = 39.49m, TransactionReference = "TXN-000002", QRCode = "", PaidDate = DateTime.Now, Status = "Paid", Remark = "" },
                };
                context.Payments.AddRange(payments);
                context.SaveChanges();
            }

            // ---------- Reviews ----------
            if (!context.Reviews.Any())
            {
                var books = context.Books.ToList();
                var users = context.Users.ToList();

                var reviews = new List<Review>
                {
                    new Review { BookId = books[0].BookId, UserId = users[1].UserId, Rating = 5, Comment = "A must-read classic. Chilling and relevant.", CreatedAt = DateTime.Now },
                    new Review { BookId = books[1].BookId, UserId = users[2].UserId, Rating = 5, Comment = "Loved every page, great for all ages.",      CreatedAt = DateTime.Now },
                    new Review { BookId = books[2].BookId, UserId = users[3].UserId, Rating = 4, Comment = "Solid advice for writing maintainable code.",CreatedAt = DateTime.Now },
                };
                context.Reviews.AddRange(reviews);
                context.SaveChanges();
            }

            // ---------- Contacts ----------
            if (!context.Contacts.Any())
            {
                var contacts = new List<Contact>
                {
                    new Contact { FullName = "Sophea Chan",  Email = "sophea.chan@mail.com",  Subject = "Order Inquiry",       Message = "I have not received a confirmation email for my order.", CreatedAt = DateTime.Now },
                    new Contact { FullName = "Rithy Vann",   Email = "rithy.vann@mail.com",   Subject = "Book Recommendation", Message = "Do you have any book recommendations for beginners in tech?", CreatedAt = DateTime.Now },
                };
                context.Contacts.AddRange(contacts);
                context.SaveChanges();
            }
        }
    }
}