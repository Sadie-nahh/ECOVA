using EnvContract.DAL.Database;
using EnvContract.DTO.Entities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    /// <summary>
    /// Repository lấy kết quả kiểm thử tổng hợp (Field + Lab) theo đơn hàng.
    /// Khác TestResultRepository (dữ liệu raw từng mẫu),
    /// TestingResultRepository lấy view kết quả đã ghép theo thông số và khu vực.
    /// </summary>
    public class TestingResultRepository
    {
        /// <summary>
        /// Lấy toàn bộ kết quả kiểm thử của một đơn hàng,
        /// bao gồm thông tin thông số, đơn vị, QCVN và giá trị đo.
        /// </summary>
        public async Task<List<QcvnParameterDTO>> GetTestingResultsByOrderAsync(string orderId)
        {
            try
            {
                var result = await SqlHelper.QuerySpAsync<QcvnParameterDTO>(
                    "sp_GetTestingResultsByOrder", new { OrderID = orderId });
                return result.ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TestingResult] Lỗi lấy kết quả đơn hàng {OrderID}", orderId);
                return new List<QcvnParameterDTO>();
            }
        }

        /// <summary>
        /// Kiểm tra xem đơn hàng đã có đủ kết quả để xuất PDF chưa.
        /// Trả về true nếu tất cả SamplingPlanItems đều đã có TestResults.
        /// </summary>
        public async Task<bool> IsOrderResultCompleteAsync(string orderId)
        {
            try
            {
                int pending = await SqlHelper.QuerySingleOrDefaultSpAsync<int>(
                    "sp_GetPendingResultCount", new { OrderID = orderId });
                return pending == 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TestingResult] Lỗi kiểm tra hoàn thành đơn hàng {OrderID}", orderId);
                return false;
            }
        }
    }
}
