using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.DAL.Database;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class ContractRepository : IContractRepository
    {
        public async Task AddContractAsync(ContractDto contract)
        {
            await SqlHelper.ExecuteSpAsync("sp_AddContract", new
            {
                ContractID = contract.ContractId,
                CustomerID = contract.CustomerId,
                contract.SignedDate, contract.ValidFrom, contract.ValidTo,
                contract.ContractFilePath, contract.Status, contract.CreatedBy,
                contract.TotalContractValue, contract.IndustryType, contract.RenewalLabel
            });
        }

        public async Task<List<ContractDto>> GetAllContractsAsync()
        {
            var result = await SqlHelper.QuerySpAsync<ContractDto>("sp_GetAllContracts");
            return result.ToList();
        }

        public async Task<ContractDto?> GetByIdAsync(string contractId)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<ContractDto>(
                "sp_GetContractById", new { ContractID = contractId });
        }

        public async Task UpdateContractAsync(ContractDto contract)
        {
            await SqlHelper.ExecuteSpAsync("sp_UpdateContract", new
            {
                ContractID = contract.ContractId,
                CustomerID = contract.CustomerId,
                contract.SignedDate, contract.ValidFrom, contract.ValidTo,
                contract.ContractFilePath, contract.Status,
                contract.TotalContractValue, contract.IndustryType, contract.RenewalLabel
            });
        }

        public async Task DeleteContractAsync(string contractId)
        {
            await SqlHelper.ExecuteSpAsync("sp_DeleteContract", new { ContractID = contractId });
        }

        public async Task<List<ContractDto>> GetContractsByCustomerIdAsync(string customerId)
        {
            var result = await SqlHelper.QuerySpAsync<ContractDto>(
                "sp_GetContractsByCustomer", new { CustomerID = customerId });
            return result.ToList();
        }

        public async Task<List<ContractDisplayDTO>> GetContractsWithCustomerNameAsync()
        {
            var result = await SqlHelper.QuerySpAsync<ContractDisplayDTO>("sp_GetContractsWithCustomerName");
            return result.ToList();
        }

        public async Task<List<ContractCardDTO>> GetContractCardsAsync()
        {
            var result = await SqlHelper.QuerySpAsync<ContractCardDTO>("sp_GetContractCards");
            return result.ToList();
        }

        public async Task<List<ContractCardDTO>> SearchContractCardsAsync(string keyword)
        {
            var result = await SqlHelper.QuerySpAsync<ContractCardDTO>(
                "sp_SearchContracts", new { Keyword = keyword });
            return result.ToList();
        }

        public async Task<ContractCardDTO?> GetContractCardByIdAsync(string contractId)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<ContractCardDTO?>(
                "sp_GetContractCardById", new { ContractID = contractId });
        }

        public async Task<List<ContractNotificationDTO>> GetExpiringContractEmailsAsync(int daysThreshold)
        {
            var result = await SqlHelper.QuerySpAsync<ContractNotificationDTO>(
                "sp_GetExpiringContracts", new { DaysThreshold = daysThreshold });
            return result.ToList();
        }

        public async Task<List<ContractNotificationDTO>> GetContractsForNotificationAsync(int daysThreshold)
        {
            var result = await SqlHelper.QuerySpAsync<ContractNotificationDTO>(
                "sp_GetExpiringContracts", new { DaysThreshold = daysThreshold });
            return result.ToList();
        }
    }
}