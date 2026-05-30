using EnvContract.BLL.Interfaces;
using EnvContract.BLL.Validators;
using EnvContract.Common.Constants;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.DTO.Requests;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepository; 
        private readonly ContractValidator _contractValidator;
        private readonly AiIntegrationService _aiService;

        public ContractService(IContractRepository contractRepository, ContractValidator contractValidator, AiIntegrationService aiService)
        {
            _contractRepository = contractRepository;
            _contractValidator = contractValidator;
            _aiService = aiService;
        }

        public async Task CreateContractAsync(CreateContractRequest request)
        {
            _contractValidator.ValidateAndThrow(request.Contract);

            if (!string.IsNullOrEmpty(request.SourcePdfPath) && File.Exists(request.SourcePdfPath))
            {
                string fileName = $"{request.Contract.ContractId}_{DateTime.Now:yyyyMMdd}_{Path.GetFileName(request.SourcePdfPath)}";
                
                string targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UploadedContracts");
                if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

                string targetPath = Path.Combine(targetFolder, fileName);
                File.Copy(request.SourcePdfPath, targetPath, true);
                
                request.Contract.ContractFilePath = targetPath;
            }

            request.Contract.Status = (int)ContractStatus.Active; 

            await _contractRepository.AddContractAsync(request.Contract);

            // Ghi audit log tạo hợp đồng
            _ = EnvContract.DAL.Repositories.SystemAuditHelper.LogAsync(
                EnvContract.Common.AppState.Instance.CurrentUser?.UserID,
                "CREATE_CONTRACT", "Contract", request.Contract.ContractId,
                $"Tạo HĐ {request.Contract.ContractId} cho KH {request.Contract.CustomerId}");
        }

        public async Task<List<ContractDto>> GetAllContractsAsync()
        {
            return await _contractRepository.GetAllContractsAsync();
        }

        public async Task<List<ContractCardDTO>> GetContractCardsAsync()
        {
            var cards = await _contractRepository.GetContractCardsAsync();
            foreach (var card in cards)
            {
                card.RenewalScore = CalculateRenewalScore(card);
            }
            return cards;
        }

        public async Task<List<ContractCardDTO>> SearchContractCardsAsync(string keyword)
        {
            var cards = await _contractRepository.SearchContractCardsAsync(keyword);
            foreach (var card in cards)
            {
                card.RenewalScore = CalculateRenewalScore(card);
            }
            return cards;
        }

        public async Task UpdateContractAsync(ContractDto contract)
        {
            await _contractRepository.UpdateContractAsync(contract);
        }

        public async Task<ContractCardDTO?> GetContractCardByIdAsync(string contractId)
        {
            var card = await _contractRepository.GetContractCardByIdAsync(contractId);
            if (card != null)
            {
                card.RenewalScore = CalculateRenewalScore(card);
            }
            return card;
        }

        /// <summary>
        /// AI-powered scoring: dự đoán khả năng tái ký hợp đồng dùng ML.NET model.
        /// Ưu tiên dùng dữ liệu thực từ CustomerFeedbacks (ResponseTime, PreviousViolations).
        /// Fallback về rule-based nếu model không khả dụng.
        /// </summary>
        private int CalculateRenewalScore(ContractCardDTO card)
        {
            try
            {
                string industry = string.IsNullOrEmpty(card.IndustryType) ? "Manufacturing" : card.IndustryType;
                float contractValue = (float)card.TotalContractValue;
                if (contractValue <= 0) contractValue = 50_000_000f;

                // ── Dùng dữ liệu thực từ CustomerFeedbacks ───────────────────────────
                // ResponseTime và PreviousViolations được JOIN vào ContractCardDTO từ DB.
                // ISNULL → 72h và 0 violations nếu khách hàng chưa có feedback.
                float responseTime  = card.ResponseTime > 0 ? card.ResponseTime : 72f;
                float violations    = card.PreviousViolations;

                var prediction = _aiService.PredictRenewal(
                    totalContractValue: contractValue,
                    industryType:       industry,
                    responseTime:       responseTime,
                    previousViolations: violations,
                    customerId:         card.CustomerId,
                    companyName:        card.CompanyName
                );

                return (int)Math.Clamp(prediction.RenewalProbabilityScore, 0, 100);
            }
            catch
            {
                // Fallback rule-based nếu AI lỗi
                int score = 50;
                if (card.Status == (int)ContractStatus.Pending) score += 10;
                if (card.ValidTo > DateTime.Now) score += 10;
                if ((card.ValidTo - card.SignedDate).TotalDays > 180) score += 10;
                return Math.Clamp(score, 0, 100);
            }
        }
    }
}