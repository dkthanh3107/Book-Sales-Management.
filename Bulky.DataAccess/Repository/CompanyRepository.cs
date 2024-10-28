using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bulky.DataAccess.Repository
{
    public class CompanyRepository : Repository<Company>, ICompanyRepository
    {
        private ApplicationDbContext _dbContext;
    public CompanyRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

        public void Update(Company company)
        {
            var objFromDB = _dbContext.Companies.FirstOrDefault(u => u.Company_Id == company.Company_Id);
            if (objFromDB != null)
            {
                objFromDB.Company_Name = company.Company_Name;
                objFromDB.StreetAddress = company.StreetAddress;
                objFromDB.City = company.City;
                objFromDB.State = company.State;
                objFromDB.PostalCode = company.PostalCode;
                objFromDB.PhoneNumber = company.PhoneNumber;
            }
        }
    }
}
