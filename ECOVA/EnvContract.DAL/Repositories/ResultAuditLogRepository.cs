using EnvContract.DAL.Database;
using EnvContract.DTO.Entities;
using Serilog;
using System;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    /// <summary>
    /// Repository ghi nhận lịch sử thay đổi kết quả kiểm thử.
    /// Mỗi lần chỉnh sửa ResultValue trong TestResults sẽ tạo một bản ghi HistoryID.
    /// </summary>
    public class ResultAuditLogRepository
    {
        /// <summary>
        /// Ghi nhận thay đổi giá trị kết quả kiểm thử vào ResultHistory.
        /// Gọi sau khi cập nhật TestResults thành công.
        /// </summary>
        /// <param name="resultId">ID của bản ghi TestResults bị chỉnh sửa.</param>
        /// <param name="oldValue">Giá trị cũ trước khi thay đổi.</param>
        /// <param name="newValue">Giá trị mới sau khi thay đổi.</param>
        /// <param name="changedByUserId">UserID của người thực hiện thay đổi.</param>
        public static async Task LogResultChangeAsync(
            string resultId,
            double oldValue,
            double newValue,
            string changedByUserId)
        {
            try
            {
                await SqlHelper.ExecuteSpAsync("sp_AddResultHistory", new
                {
                    HistoryID = Guid.NewGuid().ToString(),
                    ResultID  = resultId,
                    OldValue  = oldValue,
                    NewValue  = newValue,
                    ChangedBy = changedByUserId
                });
                Log.Information("[ResultAudit] Ghi nhận thay đổi ResultID={R}: {Old} → {New} bởi UserID={U}",
                    resultId, oldValue, newValue, changedByUserId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ResultAudit] Lỗi ghi nhận thay đổi ResultID={R}", resultId);
            }
        }
    }
}
