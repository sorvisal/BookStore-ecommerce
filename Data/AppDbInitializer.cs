using E_Commerce.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Data
{
    public class AppDbInitializer
    {
        private static readonly PasswordHasher<User> PasswordHasher = new PasswordHasher<User>();

        public static string GenerateOrderNumber(BookStoreDbContext context)
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            var prefix = $"ORD-{today}-";

            var used = context.Orders
                .Where(o => o.OrderNumber != null && o.OrderNumber.StartsWith(prefix))
                .Select(o => o.OrderNumber)
                .ToList();

            var sequence = 1;
            string candidate;
            do
            {
                candidate = $"{prefix}{sequence:D4}";
                sequence++;
            }
            while (used.Contains(candidate));

            return candidate;
        }

        public static void Seed(BookStoreDbContext context)
        {
            context.Database.Migrate();

            SeedUsers(context);
            SeedCategories(context);
            SeedPublishers(context);
            SeedAuthors(context);
            SeedShippingMethods(context);
            SeedPaymentMethods(context);
            SeedBooks(context);
            SeedBookAuthors(context);
            SeedShoppingCarts(context);
            SeedCartItems(context);
            SeedOrders(context);
            SeedReviews(context);
            SeedContacts(context);
        }

        // ------------------------------------------------------------------ Users
        private static void SeedUsers(BookStoreDbContext context)
        {
            var seedUsers = new List<User>
            {
                new User { FirstName = "Visal",  LastName = "Chan",  Gender = "Male",   DateOfBirth = new DateTime(1995, 4, 12),  Phone = "012345671", Email = "admin@bookstore.com",   ProfileImage = "/images/avatars/user1.jpg", Address = "Phnom Penh, Cambodia",   Role = "Admin",    Status = true, CreatedAt = DateTime.Now },
                new User { FirstName = "Dara",   LastName = "Sok",   Gender = "Male",   DateOfBirth = new DateTime(1997, 9, 3),   Phone = "012345672", Email = "dara.sok@mail.com",     ProfileImage = "/images/avatars/user2.jpg", Address = "Siem Reap, Cambodia",   Role = "Customer", Status = true, CreatedAt = DateTime.Now },
                new User { FirstName = "Srey",   LastName = "Leang", Gender = "Female", DateOfBirth = new DateTime(1999, 1, 27),  Phone = "012345673", Email = "srey.leang@mail.com",   ProfileImage = "/images/avatars/user3.jpg", Address = "Battambang, Cambodia", Role = "Customer", Status = true, CreatedAt = DateTime.Now },
                new User { FirstName = "Panha",  LastName = "Kim",   Gender = "Male",   DateOfBirth = new DateTime(1993, 6, 18),  Phone = "012345674", Email = "panha.kim@mail.com",    ProfileImage = "/images/avatars/user4.jpg", Address = "Phnom Penh, Cambodia",   Role = "Customer", Status = true, CreatedAt = DateTime.Now },
                new User { FirstName = "Chenda", LastName = "Lim",   Gender = "Female", DateOfBirth = new DateTime(1996, 11, 8),  Phone = "012345675", Email = "chenda.lim@mail.com",   ProfileImage = "/images/avatars/user5.jpg", Address = "Kandal, Cambodia",       Role = "Customer", Status = true, CreatedAt = DateTime.Now },
            };

            if (!context.Users.Any())
            {
                foreach (var user in seedUsers)
                {
                    user.PasswordHash = HashPassword(user, user.Role == "Admin" ? "Admin@123" : "User@123");
                }
                context.Users.AddRange(seedUsers);
                context.SaveChanges();
                return;
            }

            // Repair any legacy placeholder hashes and fill missing fields.
            var changed = false;
            foreach (var user in seedUsers)
            {
                var existing = context.Users.FirstOrDefault(u => u.Email == user.Email);
                if (existing == null)
                {
                    user.PasswordHash = HashPassword(user, user.Role == "Admin" ? "Admin@123" : "User@123");
                    context.Users.Add(user);
                    changed = true;
                    continue;
                }

                if (string.IsNullOrEmpty(existing.PasswordHash) || existing.PasswordHash.StartsWith("HASHED_PASSWORD"))
                {
                    existing.PasswordHash = HashPassword(existing, existing.Role == "Admin" ? "Admin@123" : "User@123");
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existing.FirstName)) { existing.FirstName = user.FirstName; changed = true; }
                if (string.IsNullOrWhiteSpace(existing.LastName)) { existing.LastName = user.LastName; changed = true; }
                if (string.IsNullOrWhiteSpace(existing.Role)) { existing.Role = "Customer"; changed = true; }
                if (string.IsNullOrWhiteSpace(existing.ProfileImage)) { existing.ProfileImage = user.ProfileImage; changed = true; }
            }

            if (changed) context.SaveChanges();
        }

        private static string HashPassword(User user, string password)
        {
            return PasswordHasher.HashPassword(user, password);
        }

        // ------------------------------------------------------------- Categories
        private static void SeedCategories(BookStoreDbContext context)
        {
            var categories = new List<Category>
            {
                new Category { CategoryName = "Fiction",     Description = "Novels, short stories and literary works",   Image = "/images/categories/fiction.jpg",     Status = true, CreatedAt = DateTime.Now },
                new Category { CategoryName = "Non-Fiction", Description = "Essays, memoirs and fact-based titles",       Image = "/images/categories/nonfiction.jpg", Status = true, CreatedAt = DateTime.Now },
                new Category { CategoryName = "Science",     Description = "Physics, biology, maths and popular science", Image = "/images/categories/science.jpg",     Status = true, CreatedAt = DateTime.Now },
                new Category { CategoryName = "Business",    Description = "Economics, management and entrepreneurship",   Image = "/images/categories/business.jpg",    Status = true, CreatedAt = DateTime.Now },
                new Category { CategoryName = "Children",    Description = "Picture books and stories for young readers", Image = "/images/categories/children.jpg",    Status = true, CreatedAt = DateTime.Now },
                new Category { CategoryName = "Technology",  Description = "Programming, software engineering and IT",    Image = "/images/categories/technology.jpg",  Status = true, CreatedAt = DateTime.Now },
            };

            if (!context.Categories.Any())
            {
                context.Categories.AddRange(categories);
                context.SaveChanges();
                return;
            }

            foreach (var category in categories)
            {
                if (!context.Categories.Any(c => c.CategoryName == category.CategoryName))
                {
                    context.Categories.Add(category);
                }
            }
            context.SaveChanges();
        }

        // ------------------------------------------------------------- Publishers
        private static void SeedPublishers(BookStoreDbContext context)
        {
            var publishers = new List<Publisher>
            {
                new Publisher { PublisherName = "Penguin Random House", Phone = "0123456701", Email = "contact@penguinrandomhouse.com", Website = "https://penguinrandomhouse.com", Address = "1745 Broadway, New York, USA",   Description = "International publisher of literary and commercial fiction.", CreatedAt = DateTime.Now },
                new Publisher { PublisherName = "HarperCollins",        Phone = "0123456702", Email = "contact@harpercollins.com",      Website = "https://harpercollins.com",      Address = "195 Broadway, New York, USA",     Description = "Publisher of fiction and non-fiction titles.",        CreatedAt = DateTime.Now },
                new Publisher { PublisherName = "O'Reilly Media",        Phone = "0123456703", Email = "contact@oreilly.com",           Website = "https://oreilly.com",             Address = "1005 Gravenstein Highway, Sebastopol, USA", Description = "Technology and programming books.", CreatedAt = DateTime.Now },
                new Publisher { PublisherName = "Scholastic",           Phone = "0123456704", Email = "contact@scholastic.com",        Website = "https://scholastic.com",          Address = "557 Broadway, New York, USA",     Description = "Children's books and educational titles.",          CreatedAt = DateTime.Now },
            };

            if (!context.Publishers.Any())
            {
                context.Publishers.AddRange(publishers);
                context.SaveChanges();
                return;
            }

            foreach (var publisher in publishers)
            {
                if (!context.Publishers.Any(p => p.PublisherName == publisher.PublisherName))
                {
                    context.Publishers.Add(publisher);
                }
            }
            context.SaveChanges();
        }

        // ---------------------------------------------------------------- Authors
        private static void SeedAuthors(BookStoreDbContext context)
        {
            var authors = new List<Author>
            {
                new Author { FullName = "George Orwell",     Biography = "English novelist famous for dystopian political fiction.",              Country = "United Kingdom", Photo = "/images/avatars/orwell.jpg",  CreatedAt = DateTime.Now },
                new Author { FullName = "Gabriel Garcia Marquez", Biography = "Colombian novelist and Nobel laureate known for magical realism.",   Country = "Colombia",       Photo = "/images/avatars/marquez.jpg", CreatedAt = DateTime.Now },
                new Author { FullName = "Toni Morrison",     Biography = "American novelist and editor whose work explores race and identity.",     Country = "United States",  Photo = "/images/avatars/morrison.jpg", CreatedAt = DateTime.Now },
                new Author { FullName = "Yuval Noah Harari", Biography = "Historian best known for Sapiens and Humankind.",                        Country = "Israel",         Photo = "/images/avatars/harari.jpg",   CreatedAt = DateTime.Now },
                new Author { FullName = "Robert C. Martin",  Biography = "Software engineer and author of Clean Code and The Clean Coder.",        Country = "United States",  Photo = "/images/avatars/martin.jpg",   CreatedAt = DateTime.Now },
                new Author { FullName = "Martin Fowler",     Biography = "Software designer and author focused on refactoring and enterprise patterns.", Country = "United Kingdom", Photo = "/images/avatars/fowler.jpg", CreatedAt = DateTime.Now },
                new Author { FullName = "Carl Sagan",        Biography = "Astronomer and science communicator who popularised the cosmos.",        Country = "United States",  Photo = "/images/avatars/sagan.jpg",    CreatedAt = DateTime.Now },
                new Author { FullName = "Bill Bryson",       Biography = "British author of humorous and meticulously researched non-fiction.",   Country = "United Kingdom", Photo = "/images/avatars/bryson.jpg",   CreatedAt = DateTime.Now },
                new Author { FullName = "Peter Drucker",     Biography = "Austrian-American consultant considered the father of modern management.", Country = "United States",  Photo = "/images/avatars/drucker.jpg",  CreatedAt = DateTime.Now },
                new Author { FullName = "E. B. White",       Biography = "American writer best known for Charlotte's Web and Stuart Little.",     Country = "United States",  Photo = "/images/avatars/white.jpg",    CreatedAt = DateTime.Now },
            };

            if (!context.Authors.Any())
            {
                context.Authors.AddRange(authors);
                context.SaveChanges();
                return;
            }

            foreach (var author in authors)
            {
                if (!context.Authors.Any(a => a.FullName == author.FullName))
                {
                    context.Authors.Add(author);
                }
            }
            context.SaveChanges();
        }

        // -------------------------------------------------------- ShippingMethods
        private static void SeedShippingMethods(BookStoreDbContext context)
        {
            var shippingMethods = new List<ShippingMethod>
            {
                new ShippingMethod { MethodName = "Standard Shipping", Price = 2.50m, EstimatedDays = 6, Status = true },
                new ShippingMethod { MethodName = "Express Shipping",  Price = 6.00m, EstimatedDays = 2, Status = true },
                new ShippingMethod { MethodName = "Store Pickup",      Price = 0.00m, EstimatedDays = 0, Status = true },
            };

            if (!context.ShippingMethods.Any())
            {
                context.ShippingMethods.AddRange(shippingMethods);
                context.SaveChanges();
                return;
            }

            foreach (var method in shippingMethods)
            {
                if (!context.ShippingMethods.Any(s => s.MethodName == method.MethodName))
                {
                    context.ShippingMethods.Add(method);
                }
            }
            context.SaveChanges();
        }

        // ---------------------------------------------------------- PaymentMethods
        private static void SeedPaymentMethods(BookStoreDbContext context)
        {
            var paymentMethods = new List<PaymentMethod>
            {
                new PaymentMethod { MethodName = "Cash on Delivery", Type = "COD",  Description = "Pay in cash when the order is delivered.", Status = true },
                new PaymentMethod { MethodName = "ABA Pay",          Type = "QR",   Description = "Scan the ABA QR code to pay instantly.",  Status = true },
            };

            if (!context.PaymentMethods.Any())
            {
                context.PaymentMethods.AddRange(paymentMethods);
                context.SaveChanges();
                return;
            }

            foreach (var method in paymentMethods)
            {
                if (!context.PaymentMethods.Any(p => p.MethodName == method.MethodName))
                {
                    context.PaymentMethods.Add(method);
                }
            }
            context.SaveChanges();

            // Idempotent cleanup of the removed Credit Card method. Never delete rows old orders use.
            var legacyCards = context.PaymentMethods
                .Where(p => p.MethodName.Contains("Credit") || p.MethodName.Contains("Card")
                            || p.Type == "CARD" || p.Type == "STRIPE")
                .ToList();
            foreach (var card in legacyCards)
            {
                var referenced = context.Orders.Any(o => o.PaymentMethodId == card.PaymentMethodId)
                                  || context.Payments.Any(p => p.PaymentMethodId == card.PaymentMethodId);
                if (referenced)
                    card.Status = false; // keep history valid, hide from checkout
                else
                    context.PaymentMethods.Remove(card);
            }
            context.SaveChanges();
        }

        // ------------------------------------------------------------------ Books
        private static readonly (string Title, string Description, string ISBN, decimal Price, decimal? DiscountPrice, int Stock, string Category, string Publisher, string[] Authors)[] BookSeed =
        {
            // ---- Fiction (4) ----
            ("1984", "A dystopian novel about a totalitarian regime that erases individual thought.", "9780451524935", 12.99m, 9.99m, 50, "Fiction", "Penguin Random House", new[] { "George Orwell" }),
            ("Animal Farm", "A political allegory in which farm animals overthrow their owners.", "9780452284241", 9.95m, null, 47, "Fiction", "Penguin Random House", new[] { "George Orwell" }),
            ("One Hundred Years of Solitude", "The multi-generational history of the Buendia family.", "9780060883287", 16.75m, 13.50m, 28, "Fiction", "HarperCollins", new[] { "Gabriel Garcia Marquez" }),
            ("Beloved", "A haunting novel about memory and the legacy of slavery in the United States.", "9781400033416", 14.20m, null, 22, "Fiction", "HarperCollins", new[] { "Toni Morrison", "George Orwell" }),

            // ---- Non-Fiction (4) ----
            ("Sapiens: A Brief History of Humankind", "An exploration of how Homo sapiens came to dominate the planet.", "9780062316097", 18.50m, 15.99m, 38, "Non-Fiction", "HarperCollins", new[] { "Yuval Noah Harari" }),
            ("Humankind: A Hopeful History", "A look at the institutions that have carried humanity forward.", "9781786636828", 17.00m, null, 26, "Non-Fiction", "Penguin Random House", new[] { "Yuval Noah Harari" }),
            ("A Short History of Nearly Everything", "A witty tour of science from the Big Bang to complexity.", "9780767908184", 15.25m, 12.75m, 44, "Non-Fiction", "HarperCollins", new[] { "Bill Bryson" }),
            ("The Life and Times of the Thunderbolt Kid", "A memoir of growing up in the 1970s British Midlands.", "9780767908185", 13.40m, null, 33, "Non-Fiction", "Penguin Random House", new[] { "Bill Bryson", "Yuval Noah Harari" }),

            // ---- Science (4) ----
            ("Cosmos", "A personal journey through the history of astronomy and the universe.", "9780345539434", 19.99m, null, 23, "Science", "HarperCollins", new[] { "Carl Sagan" }),
            ("The Demon-Haunted World", "An argument for science as the bulwark against superstition.", "9780345539435", 18.20m, 15.40m, 31, "Science", "HarperCollins", new[] { "Carl Sagan" }),
            ("The Emperor's New Mind", "How mathematics, computation and symmetry relate to the mind.", "9780199548318", 25.60m, null, 17, "Science", "Penguin Random House", new[] { "Carl Sagan", "Bill Bryson" }),
            ("The Story of Science", "From fire and flint to the modern laboratory.", "9780553380163", 21.50m, 18.25m, 27, "Science", "HarperCollins", new[] { "Bill Bryson" }),

            // ---- Business (4) ----
            ("The Practice of Management", "The foundational text on modern management practice.", "9780735208869", 32.00m, null, 14, "Business", "Penguin Random House", new[] { "Peter Drucker" }),
            ("The Effective Executive", "How to be effective by focusing on what matters most.", "9780735208870", 28.50m, 24.99m, 20, "Business", "HarperCollins", new[] { "Peter Drucker" }),
            ("Management Challenges for the 21st Century", " Drucker's views on the changing role of the manager.", "9780735212210", 26.75m, null, 18, "Business", "HarperCollins", new[] { "Peter Drucker" }),
            ("The Innovator's Dilemma", "How successful companies can fail by doing the right thing.", "9780060852084", 29.30m, 24.00m, 25, "Business", "Penguin Random House", new[] { "Peter Drucker", "Yuval Noah Harari" }),

            // ---- Children (4) ----
            ("Charlotte's Web", "A spider weaves words into her web to save a runt pig.", "9780061124952", 8.99m, null, 60, "Children", "HarperCollins", new[] { "E. B. White" }),
            ("Stuart Little", "A mouse leaves his adoptive home and ventures into the wide world.", "9780061124953", 9.50m, 7.99m, 52, "Children", "HarperCollins", new[] { "E. B. White" }),
            ("The Trumpet of the Swan", "A young swan finds his voice with the help of a brass trumpet.", "9780061124954", 10.25m, null, 41, "Children", "HarperCollins", new[] { "E. B. White" }),
            ("Animal Tales and Fables", "Classic animal stories gathered for young readers.", "9780061124955", 11.00m, 9.25m, 36, "Children", "Scholastic", new[] { "George Orwell", "E. B. White" }),

            // ---- Technology (4) ----
            ("Clean Code", "A handbook of agile software craftsmanship.", "9780132350884", 34.99m, 29.99m, 26, "Technology", "O'Reilly Media", new[] { "Robert C. Martin" }),
            ("The Clean Coder", "What it means to be a professional software craftsman.", "9780132350891", 29.99m, null, 24, "Technology", "O'Reilly Media", new[] { "Robert C. Martin", "Martin Fowler" }),
            ("Refactoring", "Improving the design of existing code without changing its behaviour.", "9780134757599", 44.99m, 39.99m, 15, "Technology", "O'Reilly Media", new[] { "Martin Fowler" }),
            ("Patterns of Enterprise Application Architecture", "A reference for designing maintainable business applications.", "9780321127426", 59.00m, null, 8, "Technology", "O'Reilly Media", new[] { "Martin Fowler", "Robert C. Martin" }),
        };

        private static void SeedBooks(BookStoreDbContext context)
        {
            if (!context.Books.Any())
            {
                var categories = context.Categories.ToList();
                var publishers = context.Publishers.ToList();
                var created = DateTime.Now.AddYears(-2);

                var index = 0;
                foreach (var item in BookSeed)
                {
                    index++;
                    var category = categories.FirstOrDefault(c => c.CategoryName == item.Category) ?? categories.First();
                    var publisher = publishers.FirstOrDefault(p => p.PublisherName == item.Publisher) ?? publishers.First();

                    context.Books.Add(new Book
                    {
                        CategoryId = category.CategoryId,
                        PublisherId = publisher.PublisherId,
                        Title = item.Title,
                        Description = item.Description,
                        ISBN = item.ISBN,
                        Language = "English",
                        Price = item.Price,
                        DiscountPrice = item.DiscountPrice,
                        StockQuantity = item.Stock,
                        ImageUrl = $"/images/books/book{index}.jpg",
                        CoverImage = $"/images/books/book{index}.jpg",
                        PublishedDate = created.AddDays(index * 24),
                        PublishYear = created.AddDays(index * 24).Year,
                        Pages = 180 + index * 9,
                        Status = true,
                        CreatedAt = DateTime.Now
                    });

                    if (index % 8 == 0) context.SaveChanges();
                }

                context.SaveChanges();
                return;
            }

            var existingIsbns = context.Books.Select(b => b.ISBN).ToHashSet();
            var existingCount = context.Books.Count();
            foreach (var item in BookSeed)
            {
                if (item.ISBN != null && existingIsbns.Contains(item.ISBN)) continue;
                existingCount++;

                var category = context.Categories.FirstOrDefault(c => c.CategoryName == item.Category) ?? context.Categories.First();
                var publisher = context.Publishers.FirstOrDefault(p => p.PublisherName == item.Publisher) ?? context.Publishers.First();

                context.Books.Add(new Book
                {
                    CategoryId = category.CategoryId,
                    PublisherId = publisher.PublisherId,
                    Title = item.Title,
                    Description = item.Description,
                    ISBN = item.ISBN,
                    Language = "English",
                    Price = item.Price,
                    DiscountPrice = item.DiscountPrice,
                    StockQuantity = item.Stock,
                    ImageUrl = $"/images/books/book{existingCount}.jpg",
                    CoverImage = $"/images/books/book{existingCount}.jpg",
                    PublishedDate = DateTime.Now.AddYears(-1).AddDays(existingCount * 12),
                    PublishYear = DateTime.Now.AddYears(-1).Year,
                    Pages = 180 + existingCount * 9,
                    Status = true,
                    CreatedAt = DateTime.Now
                });
            }
            context.SaveChanges();
        }

        // ---------------------------------------------------------- BookAuthors
        private static void SeedBookAuthors(BookStoreDbContext context)
        {
            if (!context.BookAuthors.Any())
            {
                var books = context.Books.OrderBy(b => b.BookId).ToList();
                var authors = context.Authors.ToList();

                var index = 0;
                foreach (var item in BookSeed)
                {
                    if (index >= books.Count) break;

                    foreach (var authorName in item.Authors)
                    {
                        var author = authors.FirstOrDefault(a => a.FullName == authorName);
                        if (author == null) continue;

                        context.BookAuthors.Add(new BookAuthor { BookId = books[index].BookId, AuthorId = author.AuthorId });
                    }

                    index++;
                    if (index % 8 == 0) context.SaveChanges();
                }

                context.SaveChanges();
                return;
            }

            var authors2 = context.Authors.ToList();
            var books2 = context.Books.OrderBy(b => b.BookId).ToList();
            var existingPairs = context.BookAuthors.Select(ba => new { ba.BookId, ba.AuthorId }).ToList();

            var i = 0;
            foreach (var item in BookSeed)
            {
                if (i >= books2.Count) break;

                foreach (var authorName in item.Authors)
                {
                    var author = authors2.FirstOrDefault(a => a.FullName == authorName);
                    if (author == null) continue;
                    if (existingPairs.Any(p => p.BookId == books2[i].BookId && p.AuthorId == author.AuthorId)) continue;

                    context.BookAuthors.Add(new BookAuthor { BookId = books2[i].BookId, AuthorId = author.AuthorId });
                }
                i++;
            }
            context.SaveChanges();
        }

        // --------------------------------------------------------- ShoppingCarts
        private static void SeedShoppingCarts(BookStoreDbContext context)
        {
            if (!context.ShoppingCarts.Any())
            {
                var carts = context.Users
                    .Select(u => new ShoppingCart { UserId = u.UserId, CreatedAt = DateTime.Now })
                    .ToList();
                context.ShoppingCarts.AddRange(carts);
                context.SaveChanges();
                return;
            }

            foreach (var user in context.Users.ToList())
            {
                if (!context.ShoppingCarts.Any(c => c.UserId == user.UserId))
                {
                    context.ShoppingCarts.Add(new ShoppingCart { UserId = user.UserId, CreatedAt = DateTime.Now });
                }
            }
            context.SaveChanges();
        }

        // ------------------------------------------------------------- CartItems
        private static void SeedCartItems(BookStoreDbContext context)
        {
            if (!context.CartItems.Any())
            {
                var carts = context.ShoppingCarts.OrderBy(c => c.CartId).ToList();
                var books = context.Books.OrderBy(b => b.BookId).ToList();
                if (carts.Count == 0 || books.Count == 0) return;

                var cartItems = new List<CartItem>
                {
                    new CartItem { CartId = carts[0].CartId, BookId = books[0].BookId, Quantity = 2, UnitPrice = books[0].Price, Subtotal = books[0].Price * 2 },
                    new CartItem { CartId = carts[0].CartId, BookId = books[1].BookId, Quantity = 1, UnitPrice = books[1].Price, Subtotal = books[1].Price },
                    new CartItem { CartId = carts[1 % carts.Count].CartId, BookId = books[2].BookId, Quantity = 1, UnitPrice = books[2].Price, Subtotal = books[2].Price },
                    new CartItem { CartId = carts[2 % carts.Count].CartId, BookId = books[3].BookId, Quantity = 3, UnitPrice = books[3].Price, Subtotal = books[3].Price * 3 },
                };

                context.CartItems.AddRange(cartItems);
                context.SaveChanges();
            }
        }

        // --------------------------------------------------------------- Orders
        private static readonly string[] OrderStatuses = { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" };

        private static void SeedOrders(BookStoreDbContext context)
        {
            if (context.Orders.Any()) return;

            var users = context.Users.OrderBy(u => u.UserId).ToList();
            var shippingMethods = context.ShippingMethods.OrderBy(s => s.ShippingMethodId).ToList();
            var paymentMethods = context.PaymentMethods.OrderBy(p => p.PaymentMethodId).ToList();
            var books = context.Books.OrderBy(b => b.BookId).ToList();
            if (users.Count == 0 || shippingMethods.Count == 0 || paymentMethods.Count == 0 || books.Count == 0) return;

            var provinces = new[]
            {
                ("Phnom Penh", "Chamkarmon", "Tonle Bassac", "Village 1", "Street 51"),
                ("Siem Reap",  "Siem Reap",  "Svay Dangkum", "Village 2", "Street 6"),
                ("Battambang", "Battambang", "Sangkam",      "Village 3", "Street 12"),
                ("Kampong Cham", "Kampong Cham", "Kampong Cham", "Village 4", "Street 27"),
                ("Kandal",     "Kandal",     "Kandal Stueng", "Village 5", "Street 88")
            };

            var random = new Random(20260101);
            var orderDate = DateTime.Now.AddMonths(-6).AddDays(1);

            for (var i = 0; i < 20; i++)
            {
                var user = users[i % users.Count];
                var shipping = shippingMethods[i % shippingMethods.Count];
                var paymentMethod = paymentMethods[i % paymentMethods.Count];
                var address = provinces[i % provinces.Length];
                var status = OrderStatuses[i % OrderStatuses.Length];

                var detailCount = 1 + (i % 4);
                var selected = new List<Book>();
                for (var d = 0; d < detailCount; d++)
                {
                    var book = books[random.Next(books.Count)];
                    if (!selected.Contains(book)) selected.Add(book);
                }

                var subtotal = selected.Sum(b => b.Price);
                var discount = selected.Sum(b => (b.DiscountPrice ?? b.Price) - b.Price) * 1m;
                if (discount < 0) discount = 0;
                var shippingFee = shipping.Price ?? 0m;
                var tax = Math.Round(subtotal * 0.10m, 2);
                var grandTotal = Math.Round(subtotal - discount + shippingFee + tax, 2);

                var order = new Order
                {
                    OrderNumber = GenerateOrderNumber(context),
                    UserId = user.UserId,
                    ShippingMethodId = shipping.ShippingMethodId,
                    PaymentMethodId = paymentMethod.PaymentMethodId,
                    OrderDate = orderDate,
                    ReceiverName = $"{user.FirstName} {user.LastName}",
                    Phone = user.Phone,
                    Province = address.Item1,
                    District = address.Item2,
                    Commune = address.Item3,
                    Village = address.Item4,
                    Street = address.Item5,
                    ShippingAddress = $"{address.Item4}, {address.Item5}, {address.Item3}, {address.Item2}, {address.Item1}",
                    Subtotal = Math.Round(subtotal, 2),
                    Discount = Math.Round(discount, 2),
                    ShippingFee = shippingFee,
                    Tax = tax,
                    GrandTotal = grandTotal,
                    TotalAmount = grandTotal,
                    OrderStatus = status,
                    Status = status,
                    PaymentStatus = status == "Pending" ? "Unpaid" : "Paid",
                    Notes = status == "Cancelled" ? "Cancelled at customer request." : "",
                    CreatedAt = orderDate,
                    UpdatedAt = orderDate
                };

                context.Orders.Add(order);
                context.SaveChanges();

                foreach (var book in selected)
                {
                    var quantity = 1 + random.Next(2);
                    var unitPrice = book.Price;
                    var lineDiscount = Math.Round(((book.DiscountPrice ?? book.Price) - book.Price) * quantity, 2);

                    context.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.OrderId,
                        BookId = book.BookId,
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        Discount = lineDiscount,
                        Subtotal = Math.Round(unitPrice * quantity, 2)
                    });
                }
                context.SaveChanges();

                context.Payments.Add(new Payment
                {
                    OrderId = order.OrderId,
                    PaymentMethodId = paymentMethod.PaymentMethodId,
                    Amount = grandTotal,
                    TransactionReference = $"TXN-{DateTime.Now:yyyyMMdd}-{i + 1:D4}",
                    QRCode = "",
                    PaidDate = status == "Pending" ? null : orderDate,
                    Status = status == "Pending" ? "Pending" : "Paid",
                    Remark = ""
                });
                context.SaveChanges();

                orderDate = orderDate.AddDays(random.Next(8, 12));
                if (orderDate > DateTime.Now) orderDate = DateTime.Now.AddHours(-i);
            }
        }

        // -------------------------------------------------------------- Reviews
        private static void SeedReviews(BookStoreDbContext context)
        {
            if (!context.Reviews.Any())
            {
                var books = context.Books.OrderBy(b => b.BookId).ToList();
                var users = context.Users.OrderBy(u => u.UserId).ToList();
                if (books.Count == 0 || users.Count < 2) return;

                var reviews = new List<Review>
                {
                    new Review { BookId = books[0].BookId,  UserId = users[1].UserId, Rating = 5, Comment = "A chilling classic that still feels uncomfortably current.", CreatedAt = DateTime.Now.AddDays(-40) },
                    new Review { BookId = books[1].BookId,  UserId = users[2].UserId, Rating = 4, Comment = "Sparse, powerful and beautifully written. Took me a while to get in.", CreatedAt = DateTime.Now.AddDays(-35) },
                    new Review { BookId = books[5].BookId,  UserId = users[3].UserId, Rating = 3, Comment = "Clever allegory, though I finished it in one sitting.",             CreatedAt = DateTime.Now.AddDays(-30) },
                    new Review { BookId = books[6].BookId,  UserId = users[1].UserId, Rating = 5, Comment = "Changed how I think about large-scale history. Highly recommended.", CreatedAt = DateTime.Now.AddDays(-25) },
                    new Review { BookId = books[8].BookId,  UserId = users[2].UserId, Rating = 4, Comment = "Funny and genuinely informative. A great beginner's science book.",     CreatedAt = DateTime.Now.AddDays(-22) },
                    new Review { BookId = books[22].BookId, UserId = users[3].UserId, Rating = 5, Comment = "Every developer should read this once. The examples are in C# too.",        CreatedAt = DateTime.Now.AddDays(-18) },
                    new Review { BookId = books[23].BookId, UserId = users[4].UserId, Rating = 4, Comment = "Refactoring patterns are still relevant a decade on.",                    CreatedAt = DateTime.Now.AddDays(-14) },
                    new Review { BookId = books[20].BookId, UserId = users[1].UserId, Rating = 2, Comment = "Useful ideas but the pacing drags in the middle chapters.",                 CreatedAt = DateTime.Now.AddDays(-9) },
                    new Review { BookId = books[24 % books.Count].BookId, UserId = users[2].UserId, Rating = 5, Comment = "My children ask for this one every single night.",                  CreatedAt = DateTime.Now.AddDays(-5) },
                    new Review { BookId = books[12].BookId, UserId = users[4].UserId, Rating = 4, Comment = "Clear explanations of difficult physics without losing the reader.",         CreatedAt = DateTime.Now.AddDays(-3) },
                };

                context.Reviews.AddRange(reviews);
                context.SaveChanges();
            }
        }

        // ------------------------------------------------------------- Contacts
        private static void SeedContacts(BookStoreDbContext context)
        {
            if (!context.Contacts.Any())
            {
                var contacts = new List<Contact>
                {
                    new Contact { FullName = "Sophea Chan",  Email = "sophea.chan@mail.com",  Subject = "Order Inquiry",       Message = "I have not received a confirmation email for my order ORD-0001.",       CreatedAt = DateTime.Now.AddDays(-12) },
                    new Contact { FullName = "Rithy Vann",   Email = "rithy.vann@mail.com",   Subject = "Book Recommendation", Message = "Do you have any book recommendations for beginners in programming?",      CreatedAt = DateTime.Now.AddDays(-7) },
                    new Contact { FullName = "Bopha Chea",   Email = "bopha.chea@mail.com",   Subject = "Shipping Question",   Message = "How long does express shipping to Siem Reap usually take?",             CreatedAt = DateTime.Now.AddDays(-2) },
                };

                context.Contacts.AddRange(contacts);
                context.SaveChanges();
            }
        }
    }
}
