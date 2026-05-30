using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    public interface IPlanningService
    {
        Task ConfigureSampleLocationsAsync(string orderId, List<SampleDTO> samples);
        Task CancelSampleAsync(string sampleId);
        Task<List<SampleDTO>> GetSamplesByOrderAsync(string orderId);

        /// <summary>
        /// Trả về SampleID của Sample đầu tiên thuộc Order,
        /// hoặc TỰ ĐỘNG TẠO MỚI một Sample nếu chưa có (tuân đúng FK constraints).
        /// </summary>
        Task<string> EnsureSampleExistsAsync(string orderId, string regulationId, string samplerUserId);

        // Methods for Phòng Kế Hoạch UI
        Task<List<ContractDisplayDTO>> GetContractListAsync();
        Task<List<OrderDTO>> GetOrdersByContractAsync(string contractId);
        Task<List<SampleParameterPlanDTO>> GetParametersForPlanAsync(string environmentType);

        // Area management
        Task<OrderDTO> CreateSamplingAreaAsync(string contractId, string areaName, string environmentType);
        Task DeleteSamplingAreaAsync(string orderId);
        Task SaveSamplingPlanAsync(string orderId, List<SampleParameterPlanDTO> parameters);
        Task<List<SampleParameterPlanDTO>> GetParametersByOrderAsync(string orderId);
    }
}
