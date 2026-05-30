using EnvContract.DTO.Requests;
using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    public interface IContractService
    {
        Task CreateContractAsync(CreateContractRequest request);
        Task UpdateContractAsync(ContractDto contract);
        Task<List<ContractDto>> GetAllContractsAsync();
        Task<List<ContractCardDTO>> GetContractCardsAsync();
        Task<List<ContractCardDTO>> SearchContractCardsAsync(string keyword);
        Task<ContractCardDTO?> GetContractCardByIdAsync(string contractId);
    }
}
