using EnvContract.DTO.Entities;
using System.Collections.Generic;

using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    public interface ICustomerService
    {
        Task<List<CustomerDto>> GetAllCustomersAsync();
        Task<bool> CheckTaxCodeExistsAsync(string taxCode);
        Task AddCustomerAsync(CustomerDto customer);
        Task UpdateCustomerAsync(CustomerDto customer);
        Task<CustomerDto?> GetCustomerByIdAsync(string customerId);
    }
}
