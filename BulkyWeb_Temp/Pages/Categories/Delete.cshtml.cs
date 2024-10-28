using BulkyWeb_Temp.Data;
using BulkyWeb_Temp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkyWeb_Temp.Pages.Categories
{
    [BindProperties]
    public class DeleteModel : PageModel
    {
            private readonly ApplicationDbContext _dbContext;

            public Category Category { get; set; }
            public DeleteModel(ApplicationDbContext dbContext)
            {
                _dbContext = dbContext;
            }
            public void OnGet(int? id)
            {
                if (id != null && id != 0)
                {
                    Category = _dbContext.Categories.Find(id);
                }
            }

            public IActionResult OnPost()
            {
                Category? obj = _dbContext.Categories.Find(Category.Category_Id);
                if (obj == null)
                {
                     return NotFound();
                }
                 _dbContext.Categories.Remove(obj);
                 _dbContext.SaveChanges();
                 TempData["success"] = "Category Detele successfully";
                 return RedirectToPage("Index");
            }
    }
}
