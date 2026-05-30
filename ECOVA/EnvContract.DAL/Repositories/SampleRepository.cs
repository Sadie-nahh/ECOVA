using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class SampleRepository : ISampleRepository
    {
        public async Task AddSampleAsync(SampleDTO sample)
        {
            await SqlHelper.ExecuteSpAsync("sp_AddSample", new
            {
                sample.SampleID, sample.OrderID, sample.RegulationID, sample.Barcode,
                sample.SamplingLocation, sample.SamplingTime, sample.FieldTemperature,
                sample.FieldHumidity, sample.WeatherCondition, sample.FieldImage,
                sample.IsWarning, sample.SamplerID, sample.Status
            });
        }

        public async Task DeleteSampleAsync(string sampleId)
        {
            await SqlHelper.ExecuteSpAsync("sp_DeleteSample", new { SampleID = sampleId });
        }

        public async Task<List<SampleDTO>> GetAllSamplesAsync()
        {
            var result = await SqlHelper.QuerySpAsync<SampleDTO>("sp_GetAllSamples");
            return result.ToList();
        }

        public async Task<SampleDTO?> GetSampleByIdAsync(string sampleId)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<SampleDTO>(
                "sp_GetSampleById", new { SampleID = sampleId });
        }

        public async Task<List<SampleDTO>> GetSamplesByOrderIdAsync(string orderId)
        {
            var result = await SqlHelper.QuerySpAsync<SampleDTO>(
                "sp_GetSamplesByOrder", new { OrderID = orderId });
            return result.ToList();
        }

        public async Task UpdateSampleAsync(SampleDTO sample)
        {
            await SqlHelper.ExecuteSpAsync("sp_UpdateSample", new
            {
                sample.SampleID, sample.SamplingLocation, sample.SamplingTime,
                sample.FieldTemperature, sample.FieldHumidity, sample.WeatherCondition,
                sample.FieldImage, sample.IsWarning, sample.SamplerID, sample.Status
            });
        }

        public async Task DeleteSamplingAreaAsync(string orderId)
        {
            await SqlHelper.ExecuteSpAsync("sp_DeleteSamplingArea", new { OrderID = orderId });
        }
    }
}
