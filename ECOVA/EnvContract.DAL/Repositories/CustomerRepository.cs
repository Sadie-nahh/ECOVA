using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        public async Task AddCustomerAsync(CustomerDto customer)
        {
            await SqlHelper.ExecuteSpAsync("sp_AddCustomer", new
            {
                customer.CustomerId, customer.TaxCode, customer.CompanyName,
                customer.Address, customer.Representative, customer.ContactEmail, customer.PhoneNumber
            });
        }

        public async Task<bool> CheckTaxCodeExistsAsync(string taxCode)
        {
            int count = await SqlHelper.QuerySingleOrDefaultSpAsync<int>("sp_CheckTaxCodeExists", new { TaxCode = taxCode });
            return count > 0;
        }

        public async Task<List<CustomerDto>> GetAllCustomersAsync()
        {
            var result = await SqlHelper.QuerySpAsync<CustomerDto>("sp_GetAllCustomers");
            return result.ToList();
        }

        public async Task UpdateCustomerAsync(CustomerDto customer)
        {
            await SqlHelper.ExecuteSpAsync("sp_UpdateCustomer", new
            {
                customer.CustomerId, customer.CompanyName, customer.Address,
                customer.Representative, customer.ContactEmail, customer.PhoneNumber
            });
        }

        public async Task DeleteCustomerAsync(string customerId)
        {
            await SqlHelper.ExecuteSpAsync("sp_DeleteCustomer", new { CustomerId = customerId });
        }

        public async Task<CustomerDto?> GetByIdAsync(string customerId)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<CustomerDto>("sp_GetCustomerById", new { CustomerId = customerId });
        }
    }
}
