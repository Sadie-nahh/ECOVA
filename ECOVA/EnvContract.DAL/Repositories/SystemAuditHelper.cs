using EnvContract.DAL.Database;
using Serilog;
using System;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    /// <summary>
    /// Static helper ghi nhận audit-log toàn hệ thống vào bảng AuditLogs.
    /// Dùng static method để gọi từ bất kỳ đâu mà không cần inject.
    ///
    /// Điểm cần ghi audit:
    ///   - LOGIN / LOGOUT
    ///   - CREATE_CONTRACT / UPDATE_CONTRACT / DELETE_CONTRACT
    ///   - CREATE_EMPLOYEE / UPDATE_EMPLOYEE / DELETE_EMPLOYEE
    ///   - APPROVE_ORDER / REJECT_ORDER
    ///
    /// Pattern dùng:
    ///   await SystemAuditHelper.LogAsync(userId, "LOGIN", detail: $"Máy: {Environment.MachineName}");
    /// </summary>
    public static class SystemAuditHelper
    {
        /// <summary>
        /// Ghi một bản ghi audit-log vào bảng AuditLogs qua sp_AddAuditLog.
        /// Fire-and-forget safe: lỗi chỉ log warning, không throw exception.
        /// </summary>
        /// <param name="userId">UserID thực hiện hành động (null nếu guest).</param>
        /// <param name="action">Tên hành động (VD: "LOGIN", "CREATE_CONTRACT").</param>
        /// <param name="entityType">Loại entity bị tác động (VD: "Contract", "Employee").</param>
        /// <param name="entityId">ID của entity bị tác động.</param>
        /// <param name="detail">Chi tiết bổ sung.</param>
        public static async Task LogAsync(
            string? userId,
            string action,
            string? entityType = null,
            string? entityId   = null,
            string? detail     = null)
        {
            try
            {
                await SqlHelper.ExecuteSpAsync("sp_AddAuditLog", new
                {
                    UserID     = userId,
                    Action     = action,
                    EntityType = entityType,
                    EntityID   = entityId,
                    Detail     = detail
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Lỗi ghi audit không được crash app — chỉ log warning
                Log.Warning(ex, "[Audit] Không thể ghi audit log: Action={A}, UserID={U}", action, userId);
            }
        }
    }
}
