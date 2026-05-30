using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class StandardParameterRepository : IStandardParameterRepository
    {
        public async Task<List<TestParameterDTO>> GetAllParametersAsync()
        {
            var result = await SqlHelper.QuerySpAsync<TestParameterDTO>("sp_GetAllParameters");
            return (List<TestParameterDTO>)result;
        }

        public async Task<List<SampleParameterPlanDTO>> GetParametersByEnvironmentTypeAsync(string environmentType)
        {
            var result = await SqlHelper.QuerySpAsync<SampleParameterPlanDTO>(
                "sp_GetParametersByEnvironment", new { EnvironmentType = environmentType });
            return (List<SampleParameterPlanDTO>)result;
        }
    }
}
