using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface IContractRepository
    {
        Task AddContractAsync(ContractDto contract);
        Task UpdateContractAsync(ContractDto contract);
        Task<List<ContractDto>> GetAllContractsAsync();
        Task<List<ContractDisplayDTO>> GetContractsWithCustomerNameAsync();
        Task<List<ContractCardDTO>> GetContractCardsAsync();
        Task<List<ContractCardDTO>> SearchContractCardsAsync(string keyword);
        Task<ContractCardDTO?> GetContractCardByIdAsync(string contractId);
        Task<List<ContractNotificationDTO>> GetExpiringContractEmailsAsync(int daysThreshold);
        Task<List<ContractNotificationDTO>> GetContractsForNotificationAsync(int daysThreshold);
    }
}