using BulkyWeb_Temp.Data;
using BulkyWeb_Temp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkyWeb_Temp.Pages.Categories
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        public IndexModel(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public List<Category> CategoryList { get; set; }
        public void OnGet()
        {
            CategoryList = _dbContext.Categories.ToList();
        }
    }
}
