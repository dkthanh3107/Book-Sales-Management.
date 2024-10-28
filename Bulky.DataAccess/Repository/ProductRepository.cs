using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bulky.DataAccess.Repository
{
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        private ApplicationDbContext _dbContext;
        public ProductRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public void Update(Product product)
        {
            var objFromDB = _dbContext.Products.FirstOrDefault(u => u.Product_Id == product.Product_Id);
            if (objFromDB != null) 
            {
                objFromDB.Title = product.Title;
                objFromDB.ISBN = product.ISBN;
                objFromDB.Price = product.Price;
                objFromDB.Description = product.Description;
                objFromDB.Price50 = product.Price50;
                objFromDB.Price100 = product.Price100;
                objFromDB.ListPrice = product.ListPrice;
                objFromDB.Author = product.Author;
                objFromDB.CategoryId =product.CategoryId;
                if(objFromDB.ImageUrl != null) 
                {
                    objFromDB.ImageUrl = product.ImageUrl;
                }
            }
        }
    }
}
