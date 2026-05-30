using EnvContract.BLL.Interfaces;
using EnvContract.BLL.Validators;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    public class TestingService : ITestingService
    {
        private readonly ITestResultRepository _testResultRepository;
        private readonly IAuditLogRepository   _auditLogRepository;
        private readonly ResultValidator       _resultValidator;

        public TestingService(
            ITestResultRepository testResultRepository,
            IAuditLogRepository   auditLogRepository,
            ResultValidator       resultValidator)
        {
            _testResultRepository = testResultRepository;
            _auditLogRepository   = auditLogRepository;
            _resultValidator      = resultValidator;
        }

        /// <summary>
        /// Lưu kết quả kiểm nghiệm (tạo mới hoặc sửa đổi).
        /// C# chỉ validate input rồi gọi 1 SP duy nhất.
        /// DB xử lý toàn bộ:
        ///   - INSERT hoặc UPDATE.
        ///   - Trigger trg_TestResults_AutoWarning tự tính IsWarning từ RegulationLimits.
        ///   - Trigger trg_TestResults_AuditHistory tự ghi ResultHistory (ChangedBy, ChangedAt).
        /// </summary>
        public async Task EnterTestResultAsync(TestResultDTO result, string? reasonIfModified = null)
        {
            // Validate input (FluentValidation — application-level)
            var validation = _resultValidator.Validate(result);
            if (!validation.IsValid)
                throw new ArgumentException(validation.ToString());

            try
            {
                // 1 SP call — DB xử lý toàn bộ
                var saved = await _testResultRepository.SaveTestResultAsync(
                    result.ResultID,
                    result.SampleID,
                    result.ParamID,
                    result.ResultValue,
                    result.TesterID);

                // Đồng bộ IsWarning về DTO để GUI hiển thị cảnh báo ngay
                if (saved != null)
                    result.IsWarning = saved.IsWarning;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TestingService] Lỗi lưu kết quả {ResultID}", result.ResultID);
                throw;
            }
        }

        public async Task<List<AuditLogDTO>> GetResultHistoryAsync(string resultId)
        {
            return await _auditLogRepository.GetAuditLogsByResultIdAsync(resultId);
        }

        public async Task<List<TestResultDTO>> GetResultsForSampleAsync(string sampleId)
        {
            return await _testResultRepository.GetTestResultsBySampleIdAsync(sampleId);
        }
    }
}
