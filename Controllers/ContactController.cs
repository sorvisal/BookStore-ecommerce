using E_Commerce.Data;
using E_Commerce.Models;
using Microsoft.AspNetCore.Mvc;

namespace E_Commerce.Controllers
{
    public class ContactController : Controller
    {
        private readonly BookStoreDbContext _context;

        public ContactController(BookStoreDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(Contact contact)
        {
            if (ModelState.IsValid)
            {
                contact.CreatedAt = DateTime.Now;
                _context.Contacts.Add(contact);
                _context.SaveChanges();
                TempData["Success"] = "Thanks! We will reply soon.";
                return RedirectToAction("Index");
            }
            return View(contact);
        }
    }
}
