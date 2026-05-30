using EnvContract.Common.Constants;
using EnvContract.DTO.Responses;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace EnvContract.BLL.Services
{
    // ===== ML.NET Input/Output Classes =====

    /// <summary>
    /// Schema mapping đúng cột CSV: TotalContractValue, IndustryType, ResponseTime, PreviousViolations, Label
    /// </summary>
    public class ContractRenewalInput
    {
        [LoadColumn(0)]
        public float TotalContractValue { get; set; }

        [LoadColumn(1)]
        public string IndustryType { get; set; } = string.Empty;

        [LoadColumn(2)]
        public float ResponseTime { get; set; }

        [LoadColumn(3)]
        public float PreviousViolations { get; set; }

        [LoadColumn(4)]
        [ColumnName("Label")]
        public bool Label { get; set; }
    }

    public class ContractRenewalPrediction
    {
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }

        [ColumnName("Score")]
        public float Score { get; set; }
    }

    // ===== Service chính =====

    /// <summary>
    /// Service tích hợp AI dùng ML.NET để dự đoán khả năng tái ký hợp đồng.
    /// Model được train lazy 1 lần duy nhất từ file SeedData_ML.csv.
    ///
    /// Phase 4 Improvements:
    ///   - Magic numbers → AiConstants (Common layer, configurable qua appsettings.json)
    ///   - PredictionEngine.Predict() thread-safe qua lock(_lock)
    ///   - ModelMetrics property để Dashboard có thể hiển thị chất lượng model
    /// </summary>
    public class AiIntegrationService
    {
        private readonly MLContext _mlContext;
        private ITransformer? _trainedModel;
        private PredictionEngine<ContractRenewalInput, ContractRenewalPrediction>? _predictionEngine;
        private readonly object _lock = new object();
        // volatile: bắt buộc tất cả thread đọc giá trị mới nhất từ RAM thay vì CPU cache.
        // Cần thiết cho double-checked locking pattern (C# Memory Model).
        private volatile bool _isModelTrained = false;

        /// <summary>
        /// Thông tin chất lượng model sau khi train thành công.
        /// null nếu model chưa train hoặc đang dùng fallback rule-based.
        /// </summary>
        public (double Accuracy, double AUC, int TrainCount, DateTime TrainedAt)? ModelMetrics
        { get; private set; }

        public AiIntegrationService()
        {
            _mlContext = new MLContext(seed: 42);
        }

        // Đường dẫn lưu model đã train — dùng lại lần khởi động sau
        private static readonly string ModelZipPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ecova_renewal_model.zip");

        /// <summary>
        /// Train model từ CSV hoặc load từ .zip đã lưu. Gọi 1 lần duy nhất (lazy init, thread-safe).
        /// Lần đầu: train + lưu .zip (~2-5 giây).
        /// Lần sau: load .zip (~0.3 giây). Không cần train lại.
        /// </summary>
        private void EnsureModelTrained()
        {
            if (_isModelTrained) return;

            lock (_lock)
            {
                if (_isModelTrained) return;

                try
                {
                    // ── Bước 1: Thử load model đã lưu từ cache ───────────────────────────
                    if (File.Exists(ModelZipPath))
                    {
                        _trainedModel = _mlContext.Model.Load(ModelZipPath, out _);
                        _predictionEngine = _mlContext.Model
                            .CreatePredictionEngine<ContractRenewalInput, ContractRenewalPrediction>(
                                _trainedModel);
                        _isModelTrained = true;
                        // ModelMetrics không có Accuracy/AUC khi load từ cache (không chạy Evaluate).
                        // Đặt stub để Dashboard biết model sẵn sàng (ModelMetrics != null),
                        // nhưng các metric đo lường = -1.0 thể hiện "không có dữ liệu".
                        ModelMetrics = (
                            Accuracy:   -1.0,
                            AUC:        -1.0,
                            TrainCount: -1,
                            TrainedAt:  System.IO.File.GetLastWriteTimeUtc(ModelZipPath)
                        );
                        Log.Information("[AI] Model loaded từ cache: {P}", ModelZipPath);
                        return;
                    }

                    // ── Bước 2: Tìm CSV để train mới ─────────────────────────────────────
                    string csvPath = FindCsvPath();

                    if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                    {
                        Log.Warning("[AI] Không tìm thấy SeedData_ML.csv, dùng fallback rule-based scoring.");
                        _isModelTrained = true;
                        return;
                    }

                    Log.Information("[AI] Training model từ: {P}", csvPath);

                    IDataView dataView = _mlContext.Data.LoadFromTextFile<ContractRenewalInput>(
                        path: csvPath, hasHeader: true, separatorChar: ',');

                    var splitData = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);

                    var pipeline = _mlContext.Transforms.Categorical.OneHotEncoding("IndustryTypeEncoded", "IndustryType")
                        .Append(_mlContext.Transforms.NormalizeMinMax("TotalContractValue"))
                        .Append(_mlContext.Transforms.NormalizeMinMax("ResponseTime"))
                        .Append(_mlContext.Transforms.NormalizeMinMax("PreviousViolations"))
                        .Append(_mlContext.Transforms.Concatenate("Features",
                            "TotalContractValue", "IndustryTypeEncoded", "ResponseTime", "PreviousViolations"))
                        .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                            labelColumnName: "Label", featureColumnName: "Features"));

                    _trainedModel = pipeline.Fit(splitData.TrainSet);
                    _predictionEngine = _mlContext.Model
                        .CreatePredictionEngine<ContractRenewalInput, ContractRenewalPrediction>(_trainedModel);

                    var predictions = _trainedModel.Transform(splitData.TestSet);
                    var metrics = _mlContext.BinaryClassification.Evaluate(predictions, "Label");

                    // ── Phase 4.3: Expose ModelMetrics để Dashboard hiển thị ──────────────
                    int trainCount = (int)(splitData.TrainSet.GetRowCount() ?? 0);
                    ModelMetrics = (
                        Accuracy:  metrics.Accuracy,
                        AUC:       metrics.AreaUnderRocCurve,
                        TrainCount: trainCount,
                        TrainedAt: DateTime.UtcNow
                    );
                    Log.Information("[AI] Model trained! Accuracy={A:P2}, AUC={U:P2}, TrainCount={N} (20% test set)",
                        metrics.Accuracy, metrics.AreaUnderRocCurve, trainCount);

                    // ── Bước 3: Lưu model .zip để dùng lại lần sau ───────────────────────
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(ModelZipPath)!);
                        _mlContext.Model.Save(_trainedModel, dataView.Schema, ModelZipPath);
                        Log.Information("[AI] Model đã lưu: {P}", ModelZipPath);
                    }
                    catch (Exception saveEx)
                    {
                        // Lỗi lưu không crash app — chỉ log warning
                        Log.Warning(saveEx, "[AI] Không thể lưu model cache, sẽ train lại lần sau.");
                    }

                    _isModelTrained = true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[AI] Lỗi training/loading model");
                    _isModelTrained = true; // Đánh dấu đã thử → dùng fallback
                }
            }
        }

        /// <summary>
        /// Tìm file SeedData_ML.csv trong nhiều vị trí có thể.
        /// </summary>
        private string FindCsvPath()
        {
            string[] possiblePaths = new[]
            {
                // C1: ưu tiên tìm trong bin output (copy từ .csproj CopyToOutputDirectory)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "SeedData_ML.csv"),
                // Relative từ thư mục chạy làm việc (dev mode: bin\Debug\net10.0-windows\ → ..\..\..\..\Data)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Data", "SeedData_ML.csv"),
                // Fallback: cùng cấp với solution
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data", "SeedData_ML.csv"),
            };

            foreach (var path in possiblePaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return string.Empty;
        }

        /// <summary>
        /// Dự đoán khả năng tái ký hợp đồng.
        /// </summary>
        /// <param name="totalContractValue">Giá trị hợp đồng (VNĐ)</param>
        /// <param name="industryType">Ngành nghề (e.g. "Manufacturing", "Textile")</param>
        /// <param name="responseTime">Thời gian phản hồi trung bình (giờ)</param>
        /// <param name="previousViolations">Số lần vi phạm trước đó</param>
        /// <returns>AiPredictionResponse với xác suất tái ký</returns>
        public AiPredictionResponse PredictRenewal(
            float totalContractValue,
            string industryType,
            float responseTime,
            float previousViolations,
            string? customerId = null,
            string? companyName = null)
        {
            EnsureModelTrained();

            if (_predictionEngine != null)
            {
                // ── Feature Scaling tập trung tại đây (1 nơi duy nhất, toàn hệ thống) ──
                // TotalContractValue: DB dùng VNĐ (14M-430M) → chia ContractValueDivisor → scale về 14-430
                //   CSV training đã ở đơn vị này nên NormalizeMinMax học đúng.
                // ResponseTime: DB seed lưu đơn vị giờ (1-7h), CSV training cũng là giờ (12-300h)
                //   → Pipeline NormalizeMinMax sẽ map 1-7 về khoảng [0..0.02] rất thấp.
                //   → Nhân ResponseTimeMultiplier để đưa vào vùng 15-105h, khớp phân phối training.
                // PreviousViolations: DB seed = 0-3, CSV = 0-9 → giữ nguyên, NormalizeMinMax đủ bù.
                float scaledValue = Math.Min(
                    totalContractValue / AiConstants.ContractValueDivisor,
                    AiConstants.ContractValueCap);
                float mlRT  = responseTime * AiConstants.ResponseTimeMultiplier;
                float mlVio = previousViolations; // Raw, pipeline tự normalize

                var input = new ContractRenewalInput
                {
                    TotalContractValue = scaledValue,
                    IndustryType       = industryType ?? "Manufacturing",
                    ResponseTime       = mlRT,
                    PreviousViolations = mlVio
                };

                // ── Phase 4.2: Thread-safe Predict ──────────────────────────────────────
                // PredictionEngine<T> không thread-safe (Microsoft docs).
                // Wrap với lock để đảm bảo an toàn khi nhiều threads gọi đồng thời.
                ContractRenewalPrediction prediction;
                lock (_lock)
                {
                    prediction = _predictionEngine.Predict(input);
                }

                return new AiPredictionResponse
                {
                    CustomerID  = customerId ?? "",
                    CompanyName = companyName ?? "",
                    RenewalProbabilityScore = Math.Round(prediction.Probability * 100, 1),
                    // IsPollutionWarning dùng AiConstants thay magic number 3
                    IsPollutionWarning = previousViolations >= AiConstants.ViolationWarningThreshold,
                    PredictionDetails  = prediction.PredictedLabel
                        ? $"AI dự đoán khả năng tái ký CAO ({prediction.Probability:P1}). Khách hàng có hồ sơ tốt."
                        : $"AI dự đoán khả năng tái ký THẤP ({prediction.Probability:P1}). Cần tăng cường chăm sóc."
                };
            }

            // Fallback: dùng rule-based scoring nếu model không train được
            return FallbackPrediction(totalContractValue, industryType, responseTime, previousViolations, customerId, companyName);
        }

        /// <summary>
        /// Dự đoán tổng hợp cho tất cả khách hàng (dùng cho Dashboard).
        /// Trả về danh sách top-N khách hàng có nguy cơ KHÔNG tái ký cao nhất.
        /// </summary>
        public List<AiPredictionResponse> PredictAllCustomers(
            List<(string CustomerId, string CompanyName, float ContractValue, string IndustryType, float ResponseTime, float Violations)> customers,
            int topN = 5)
        {
            var results = new List<AiPredictionResponse>();

            foreach (var c in customers)
            {
                var prediction = PredictRenewal(c.ContractValue, c.IndustryType, c.ResponseTime, c.Violations, c.CustomerId, c.CompanyName);
                results.Add(prediction);
            }

            // Sắp xếp: nguy cơ không tái ký cao nhất lên đầu
            return results
                .OrderBy(r => r.RenewalProbabilityScore)
                .Take(topN)
                .ToList();
        }

        /// <summary>
        /// Fallback rule-based prediction khi ML model không khả dụng.
        /// Sử dụng AiConstants.ViolationWarningThreshold thay vì magic number.
        /// </summary>
        private AiPredictionResponse FallbackPrediction(
            float totalContractValue, string industryType,
            float responseTime, float previousViolations,
            string? customerId, string? companyName)
        {
            double score = 50;

            // Giá trị hợp đồng cao → khả năng tái ký cao
            if (totalContractValue > 100_000_000)      score += 15;
            else if (totalContractValue > 50_000_000)  score += 10;

            // Phản hồi nhanh → tốt
            if      (responseTime <= 24) score += 15;
            else if (responseTime <= 48) score += 5;
            else if (responseTime > 96)  score -= 10;

            // Nhiều vi phạm → xấu (dùng AiConstants.ViolationWarningThreshold thay 3f)
            if (previousViolations == 0)
                score += 10;
            else if (previousViolations >= AiConstants.ViolationWarningThreshold)
                score -= 20;
            else if (previousViolations >= 1)
                score -= 5;

            score = Math.Clamp(score, 0, 100);
            bool isRenewal = score >= 50;

            return new AiPredictionResponse
            {
                CustomerID  = customerId ?? "",
                CompanyName = companyName ?? "",
                RenewalProbabilityScore = score,
                IsPollutionWarning = previousViolations >= AiConstants.ViolationWarningThreshold,
                PredictionDetails  = isRenewal
                    ? $"Rule-based: Khả năng tái ký ({score:F0}%). Khách hàng tiềm năng."
                    : $"Rule-based: Nguy cơ mất khách hàng ({score:F0}%). Cần hành động ngay."
            };
        }
    }
}
