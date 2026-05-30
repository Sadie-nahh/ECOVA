using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    public interface ITestingService
    {
        Task EnterTestResultAsync(TestResultDTO result, string? reasonIfModified = null);
        Task<List<TestResultDTO>> GetResultsForSampleAsync(string sampleId);
        Task<List<AuditLogDTO>> GetResultHistoryAsync(string resultId);
    }
}
