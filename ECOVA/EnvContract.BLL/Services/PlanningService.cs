using EnvContract.BLL.Interfaces;
using EnvContract.Common.Constants;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    public class PlanningService : IPlanningService
    {
        private readonly ISampleRepository         _sampleRepository;
        private readonly IContractRepository       _contractRepository;
        private readonly IOrderRepository          _orderRepository;
        private readonly IStandardParameterRepository _parameterRepository;
        private readonly ISamplingPlanRepository   _planRepository;

        public PlanningService(
            ISampleRepository sampleRepository,
            IContractRepository contractRepository,
            IOrderRepository orderRepository,
            IStandardParameterRepository parameterRepository,
            ISamplingPlanRepository planRepository)
        {
            _sampleRepository    = sampleRepository;
            _contractRepository  = contractRepository;
            _orderRepository     = orderRepository;
            _parameterRepository = parameterRepository;
            _planRepository      = planRepository;
        }

        public async Task CancelSampleAsync(string sampleId)
        {
            var sample = await _sampleRepository.GetSampleByIdAsync(sampleId);
            if (sample != null)
            {
                sample.Status = (int)SampleStatus.Cancelled;
                await _sampleRepository.UpdateSampleAsync(sample);
            }
        }

        public async Task ConfigureSampleLocationsAsync(string orderId, List<SampleDTO> samples)
        {
            foreach (var sample in samples)
            {
                sample.SampleID = Guid.NewGuid().ToString();
                sample.OrderID  = orderId;
                sample.Status   = (int)SampleStatus.New;
                sample.Barcode  = "BC-" + Guid.NewGuid().ToString().ToUpper()[..8];
                await _sampleRepository.AddSampleAsync(sample);
            }
        }

        public async Task<List<SampleDTO>> GetSamplesByOrderAsync(string orderId)
        {
            return await _sampleRepository.GetSamplesByOrderIdAsync(orderId);
        }

        /// <inheritdoc/>
        public async Task<string> EnsureSampleExistsAsync(string orderId, string regulationId, string samplerUserId)
        {
            // Lấy sample hiện có (nếu có) — tránh tạo duplicate
            var existing = await _sampleRepository.GetSamplesByOrderIdAsync(orderId);
            if (existing != null && existing.Count > 0)
                return existing[0].SampleID;

            // Tạo mới Sample bằng raw SQL INSERT để tránh type mismatch trong sp_AddSample
            // (@FieldImage VARBINARY(MAX) trong SP nhưng bảng Samples có FieldImage NVARCHAR(500))
            string newId  = Guid.NewGuid().ToString();
            string barcode = "BC-" + DateTime.Now.ToString("yyyyMMddHHmmss")
                             + "-" + new System.Random().Next(1000, 9999);

            // Nullable FK fields: chỉ gán nếu có giá trị hợp lệ
            string? regId   = string.IsNullOrWhiteSpace(regulationId)  ? null : regulationId;
            string? sampler = string.IsNullOrWhiteSpace(samplerUserId) ? null : samplerUserId;

            const string sql = @"
                INSERT INTO Samples
                    (SampleID, OrderID, RegulationID, Barcode, SamplingTime,
                     IsWarning, SamplerID, Status)
                VALUES
                    (@SampleID, @OrderID, @RegulationID, @Barcode, @SamplingTime,
                     0, @SamplerID, 0)";

            try
            {
                await EnvContract.DAL.Database.SqlHelper.ExecuteNonQueryAsync(sql, new
                {
                    SampleID     = newId,
                    OrderID      = orderId,
                    RegulationID = regId,
                    Barcode      = barcode,
                    SamplingTime = DateTime.Now,
                    SamplerID    = sampler,
                });
                Serilog.Log.Information("[PlanningService] Auto-created Sample {SampleID} for Order {OrderID}", newId, orderId);
                return newId;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[PlanningService] EnsureSampleExistsAsync FAILED for Order {OrderID}", orderId);
                throw;   // re-throw → UC hiển thị lỗi rõ ràng
            }
        }

        // ── Phòng Kế Hoạch UI ────────────────────────────────────────────────

        public async Task<List<ContractDisplayDTO>> GetContractListAsync()
        {
            return await _contractRepository.GetContractsWithCustomerNameAsync();
        }

        public async Task<List<OrderDTO>> GetOrdersByContractAsync(string contractId)
        {
            return await _orderRepository.GetOrdersByContractIdAsync(contractId);
        }

        public async Task<List<SampleParameterPlanDTO>> GetParametersForPlanAsync(string environmentType)
        {
            return await _parameterRepository.GetParametersByEnvironmentTypeAsync(environmentType);
        }

        // ── Area management ───────────────────────────────────────────────────

        public async Task<OrderDTO> CreateSamplingAreaAsync(string contractId, string areaName, string environmentType)
        {
            var order = new OrderDTO
            {
                OrderID         = "ORD-" + Guid.NewGuid().ToString()[..8].ToUpper(),
                ContractID      = contractId,
                OrderName       = areaName,
                EnvironmentType = environmentType,
                OrderDate       = DateTime.Now,
                Status          = 0,
                IsApproved      = 0
            };
            await _orderRepository.AddOrderAsync(order);
            return order;
        }

        /// <summary>
        /// Xóa khu vực lấy mẫu và toàn bộ dữ liệu bên trong (atomic).
        /// Gọi sp_DeleteSamplingArea — DB cascade 5 bảng trong 1 transaction.
        /// </summary>
        public async Task DeleteSamplingAreaAsync(string orderId)
        {
            await _sampleRepository.DeleteSamplingAreaAsync(orderId);
        }

        /// <summary>
        /// Lưu kế hoạch lấy mẫu cho 1 khu vực (atomic).
        /// sp_SaveSamplingPlan tự: xóa cũ + update Order.Status=1 + insert mới.
        /// </summary>
        public async Task SaveSamplingPlanAsync(string orderId, List<SampleParameterPlanDTO> parameters)
        {
            await _planRepository.SavePlanItemsAsync(orderId, parameters);
        }

        public async Task<List<SampleParameterPlanDTO>> GetParametersByOrderAsync(string orderId)
        {
            return await _planRepository.GetPlanItemsByOrderAsync(orderId);
        }
    }
}
