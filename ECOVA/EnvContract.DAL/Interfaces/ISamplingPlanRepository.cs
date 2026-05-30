using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface ISamplingPlanRepository
    {
        /// <summary>
        /// Lưu kế hoạch cho 1 khu vực (xóa cũ + update Order.Status + insert mới) — atomic.
        /// Gọi sp_SaveSamplingPlan với JSON payload.
        /// </summary>
        Task SavePlanItemsAsync(string orderId, List<SampleParameterPlanDTO> items);

        /// <summary>
        /// Lấy danh sách thông số kế hoạch đã lưu cho 1 khu vực.
        /// </summary>
        Task<List<SampleParameterPlanDTO>> GetPlanItemsByOrderAsync(string orderId);
    }
}
