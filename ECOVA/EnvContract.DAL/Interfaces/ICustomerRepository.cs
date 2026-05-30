using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface ICustomerRepository
    {
        Task<bool> CheckTaxCodeExistsAsync(string taxCode);
        Task AddCustomerAsync(CustomerDto customer);
        Task UpdateCustomerAsync(CustomerDto customer);
        Task<List<CustomerDto>> GetAllCustomersAsync();
        Task DeleteCustomerAsync(string customerId);
        Task<CustomerDto?> GetByIdAsync(string customerId);
    }
}
