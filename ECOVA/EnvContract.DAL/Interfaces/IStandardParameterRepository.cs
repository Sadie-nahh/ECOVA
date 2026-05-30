using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface IStandardParameterRepository
    {
        Task<List<TestParameterDTO>> GetAllParametersAsync();
        Task<List<SampleParameterPlanDTO>> GetParametersByEnvironmentTypeAsync(string environmentType);
    }
}
