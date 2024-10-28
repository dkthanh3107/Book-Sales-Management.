using BulkyWeb_Temp.Data;
using BulkyWeb_Temp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkyWeb_Temp.Pages.Categories
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        [BindProperty]
        public Category Category { get; set; }
        public CreateModel(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public void OnGet()
        {

        }
        public IActionResult OnPost() 
        {
            _dbContext.Add(Category);
            _dbContext.SaveChanges();
            TempData["success"] = "Category Create successfully";
            return RedirectToPage("Index");
        }
    }
}
