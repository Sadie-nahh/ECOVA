using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        public async Task<List<AuditLogDTO>> GetAuditLogsByResultIdAsync(string resultId)
        {
            var result = await SqlHelper.QuerySpAsync<AuditLogDTO>(
                "sp_GetResultHistory", new { ResultID = resultId });
            return (List<AuditLogDTO>)result;
        }
    }
}
