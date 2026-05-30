using System.Text.Json;
using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class SamplingPlanRepository : ISamplingPlanRepository
    {
        public async Task SavePlanItemsAsync(string orderId, List<SampleParameterPlanDTO> items)
        {
            var payload = items.ConvertAll(i => new
            {
                i.ParamID,
                RegulationID = i.RegulationID ?? string.Empty,
                i.Department,
                QcvnLimit    = i.QcvnLimit ?? string.Empty,
                ParamName    = i.ParamName ?? string.Empty,
                Unit         = i.Unit ?? string.Empty
            });

            string json = JsonSerializer.Serialize(payload);

            await SqlHelper.ExecuteSpAsync("sp_SaveSamplingPlan", new
            {
                OrderID   = orderId,
                ItemsJson = json
            });
        }

        public async Task<List<SampleParameterPlanDTO>> GetPlanItemsByOrderAsync(string orderId)
        {
            var result = await SqlHelper.QuerySpAsync<SampleParameterPlanDTO>(
                "sp_GetSamplingPlanByOrder", new { OrderID = orderId });
            return (List<SampleParameterPlanDTO>)result;
        }
    }
}
