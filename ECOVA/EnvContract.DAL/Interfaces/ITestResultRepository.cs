using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface ITestResultRepository
    {
        /// <summary>
        /// Lưu kết quả (INSERT lần đầu hoặc UPDATE khi sửa).
        /// Gọi sp_SaveTestResult — trigger tự tính IsWarning và ghi ResultHistory.
        /// </summary>
        Task<TestResultDTO?> SaveTestResultAsync(string resultId, string sampleId,
            string paramId, double resultValue, string testerId);

        Task DeleteTestResultAsync(string resultId);
        Task<TestResultDTO?> GetTestResultByIdAsync(string resultId);
        Task<List<TestResultDTO>> GetTestResultsBySampleIdAsync(string sampleId);
    }
}
