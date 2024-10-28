using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.DataAccess.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        public CompanyController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            List<Company> companies = _unitOfWork.Company.GetAll().ToList();
            return View(companies);
        }

        public IActionResult UpsertCompany(int? id) // update and insert
        {
            //ViewBag.CategoryList = CategoryList;
            //ViewData["CategoryList"] = CategoryList;
            
            if(id == null || id == 0)
            {
                //create 
                return View(new Company());
            }
            else
            {
                //update
                Company company = _unitOfWork.Company.Get(u => u.Company_Id == id);
                return View(company);
            }
        }
        [HttpPost]
        public IActionResult UpsertCompany(Company company)
        {
            if (ModelState.IsValid)
            {
                
                if(company.Company_Id == 0) 
                {
                    _unitOfWork.Company.Add(company);
                }
                else
                {
                    _unitOfWork.Company.Update(company);
                }
                _unitOfWork.Save();
                TempData["success"] = "Company Created successfully";
                return RedirectToAction("Index");
            }
            else
            {
                return View(company);
            }    
        }
        
        #region API CALLS
        [HttpGet]
        public IActionResult GetAll()
        {
            List<Company> companies = _unitOfWork.Company.GetAll().ToList();
            return Json(new {data =  companies});
        }
        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            var companyDelete = _unitOfWork.Company.Get(u=>u.Company_Id == id);

            if(companyDelete == null)
            {
                return Json(new {success = false , massage="Error While deleting"});
            }
            _unitOfWork.Company.Remove(companyDelete);
            _unitOfWork.Save();
            
            return Json(new { success = true , massage = "DELETE SUCCESS" });
        }
        #endregion
    }
}
