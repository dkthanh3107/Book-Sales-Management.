using BulkyWeb_Temp.Data;
using BulkyWeb_Temp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkyWeb_Temp.Pages.Categories
{
    [BindProperties]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;

        public Category Category { get; set; }
        public EditModel(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public void OnGet(int? id)
        {
            if(id !=null && id !=0)
            {
                Category = _dbContext.Categories.Find(id);
            }    
        }

        public IActionResult OnPost()
        {
            if (ModelState.IsValid)
            {
                _dbContext.Categories.Update(Category);
                _dbContext.SaveChanges();
                TempData["success"] = "Category Update successfully";
                return RedirectToPage("Index");
            }
            return Page();
        }
    }
}
