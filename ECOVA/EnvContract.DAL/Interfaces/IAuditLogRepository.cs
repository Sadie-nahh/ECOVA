using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface IAuditLogRepository
    {
        /// <summary>
        /// Lấy lịch sử sửa đổi của 1 kết quả.
        /// (AddAuditLogAsync đã bị xóa — trigger trg_TestResults_AuditHistory tự ghi)
        /// </summary>
        Task<List<AuditLogDTO>> GetAuditLogsByResultIdAsync(string resultId);
    }
}
