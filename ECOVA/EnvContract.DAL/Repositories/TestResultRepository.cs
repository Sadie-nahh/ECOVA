using Dapper;
using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class TestResultRepository : ITestResultRepository
    {
        public async Task<TestResultDTO?> SaveTestResultAsync(
            string resultId, string sampleId, string paramId,
            double resultValue, string testerId)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@ResultID",    resultId);
            parameters.Add("@SampleID",    sampleId);
            parameters.Add("@ParamID",     paramId);
            parameters.Add("@ResultValue", resultValue);
            parameters.Add("@TesterID",    testerId);

            return await SqlHelper.QuerySingleOrDefaultSpAsync<TestResultDTO>(
                "sp_SaveTestResult", parameters);
        }

        public async Task DeleteTestResultAsync(string resultId)
        {
            await SqlHelper.ExecuteSpAsync("sp_DeleteTestResult", new { ResultID = resultId });
        }

        public async Task<TestResultDTO?> GetTestResultByIdAsync(string resultId)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<TestResultDTO>(
                "sp_GetTestResultById", new { ResultID = resultId });
        }

        public async Task<List<TestResultDTO>> GetTestResultsBySampleIdAsync(string sampleId)
        {
            var result = await SqlHelper.QuerySpAsync<TestResultDTO>(
                "sp_GetTestResultsBySample", new { SampleID = sampleId });
            return result.ToList();
        }
    }
}
