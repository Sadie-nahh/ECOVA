using EnvContract.BLL.Interfaces;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerService(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        public async Task AddCustomerAsync(CustomerDto customer)
        {
            await _customerRepository.AddCustomerAsync(customer);
        }

        public async Task<bool> CheckTaxCodeExistsAsync(string taxCode)
        {
            return await _customerRepository.CheckTaxCodeExistsAsync(taxCode);
        }

        public async Task<List<CustomerDto>> GetAllCustomersAsync()
        {
            return await _customerRepository.GetAllCustomersAsync();
        }

        public async Task UpdateCustomerAsync(CustomerDto customer)
        {
            await _customerRepository.UpdateCustomerAsync(customer);
        }

        public async Task<CustomerDto?> GetCustomerByIdAsync(string customerId)
        {
            return await _customerRepository.GetByIdAsync(customerId);
        }
    }
}
