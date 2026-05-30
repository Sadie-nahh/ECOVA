using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface ISampleRepository
    {
        Task AddSampleAsync(SampleDTO sample);
        Task UpdateSampleAsync(SampleDTO sample);
        Task DeleteSampleAsync(string sampleId);
        Task<SampleDTO?> GetSampleByIdAsync(string sampleId);
        Task<List<SampleDTO>> GetSamplesByOrderIdAsync(string orderId);
        Task<List<SampleDTO>> GetAllSamplesAsync();

        /// <summary>
        /// Xóa toàn bộ khu vực lấy mẫu (cascade 5 bảng) qua sp_DeleteSamplingArea — atomic.
        /// </summary>
        Task DeleteSamplingAreaAsync(string orderId);
    }
}
