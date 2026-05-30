using EnvContract.BLL.Services;
using EnvContract.DTO.Responses;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace EnvContract.Tests.Services;

/// <summary>
/// Unit tests cho AiIntegrationService.
///
/// Lưu ý quan trọng:
///  - AiIntegrationService sẽ thử load model từ file .zip hoặc train từ CSV.
///  - Trong test environment, cả 2 đều không có → service sẽ dùng fallback rule-based.
///  - Điều này là BÌNH THƯỜNG và được thiết kế trong EnsureModelTrained().
///  - Các test này kiểm tra logic fallback + PredictAllCustomers sorting.
/// </summary>
public class AiIntegrationServiceTests
{
    private readonly AiIntegrationService _sut;

    public AiIntegrationServiceTests()
    {
        // AiIntegrationService không có dependency injection — tạo trực tiếp
        _sut = new AiIntegrationService();
    }

    // ── Test: PredictRenewal — Fallback rule-based ────────────────────────────

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictRenewal_WithoutModel_UsesFallback_ScoreInValidRange()
    {
        // Act — không có model/CSV → fallback rule-based
        AiPredictionResponse result = _sut.PredictRenewal(
            totalContractValue: 50_000_000f,
            industryType:       "Manufacturing",
            responseTime:       24f,
            previousViolations: 0f);

        // Assert
        result.Should().NotBeNull("PredictRenewal không bao giờ trả về null");
        result.RenewalProbabilityScore
              .Should().BeInRange(0, 100,
              "Score phải nằm trong khoảng [0, 100]");
        result.PredictionDetails
              .Should().NotBeNullOrEmpty("PredictionDetails phải có giá trị");
    }

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictRenewal_HighContractValue_ShouldHaveHigherScore()
    {
        // Sử dụng cực đại vs cực tiểu features để chắc chắn chó lệch
        // dù ML model hay fallback rule-based.
        AiPredictionResponse highValue = _sut.PredictRenewal(
            totalContractValue: 430_000_000f, // Giá trị đầy đủ, phản hồi nhanh, không vi phạm
            industryType:       "Manufacturing",
            responseTime:       12f,
            previousViolations: 0f);

        AiPredictionResponse lowValue = _sut.PredictRenewal(
            totalContractValue: 1_000_000f,   // Giá trị rất thấp, phản hồi chậm, nhiều vi phạm
            industryType:       "Manufacturing",
            responseTime:       300f,
            previousViolations: 9f);

        // Assert: dù model hay fallback, khách hàng lý tưởng LUÔN cao hơn khách hàng xấu
        highValue.RenewalProbabilityScore
                 .Should().BeGreaterThan(lowValue.RenewalProbabilityScore,
                 "Khách hàng lý tưởng (giá cao, nhanh, không vi phạm) phải có score cao hơn khách xấu");
    }

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictRenewal_ManyViolations_ShouldHaveLowerScore()
    {
        // Dù ML hay fallback, nhiều violations + thấp hơn LUÔN có score thấp hơn.
        AiPredictionResponse noViolations = _sut.PredictRenewal(
            totalContractValue: 430_000_000f, // Tốt nhất
            industryType:       "Manufacturing",
            responseTime:       12f,
            previousViolations: 0f);

        AiPredictionResponse manyViolations = _sut.PredictRenewal(
            totalContractValue: 1_000_000f,  // Xấu nhất
            industryType:       "Manufacturing",
            responseTime:       300f,
            previousViolations: 9f);

        // Assert
        manyViolations.RenewalProbabilityScore
                      .Should().BeLessThan(noViolations.RenewalProbabilityScore,
                      "Khách hàng xấu nhất phải có score thấp hơn khách hàng lý tưởng");
    }

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictRenewal_Violations3OrMore_SetsPollutionWarningTrue()
    {
        // Arrange: Rule: IsPollutionWarning = previousViolations >= 3
        AiPredictionResponse result = _sut.PredictRenewal(
            totalContractValue: 50_000_000f,
            industryType:       "Manufacturing",
            responseTime:       24f,
            previousViolations: 3f); // Đúng ngưỡng cảnh báo

        // Assert
        result.IsPollutionWarning.Should().BeTrue(
            "Vi phạm >= 3 phải đặt IsPollutionWarning = true");
    }

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictRenewal_Violations2_PollutionWarningFalse()
    {
        // Arrange: 2 vi phạm — chưa đến ngưỡng
        AiPredictionResponse result = _sut.PredictRenewal(
            totalContractValue: 50_000_000f,
            industryType:       "Manufacturing",
            responseTime:       24f,
            previousViolations: 2f); // Dưới ngưỡng 3

        // Assert
        result.IsPollutionWarning.Should().BeFalse(
            "Vi phạm < 3 không được đặt IsPollutionWarning");
    }

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictRenewal_WithCustomerIdAndName_ShouldReturnInResponse()
    {
        // Arrange
        string customerId   = "KH-TEST-001";
        string companyName  = "Công ty TNHH Test";

        // Act
        AiPredictionResponse result = _sut.PredictRenewal(
            totalContractValue: 50_000_000f,
            industryType:       "Manufacturing",
            responseTime:       24f,
            previousViolations: 0f,
            customerId:         customerId,
            companyName:        companyName);

        // Assert
        result.CustomerID.Should().Be(customerId, "CustomerID phải được giữ nguyên trong response");
        result.CompanyName.Should().Be(companyName, "CompanyName phải được giữ nguyên trong response");
    }

    // ── Test: PredictAllCustomers ─────────────────────────────────────────────

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictAllCustomers_TopN_ShouldReturnExactCount()
    {
        // Arrange: 10 khách hàng, lấy top 3
        var customers = new List<(string, string, float, string, float, float)>();
        for (int i = 1; i <= 10; i++)
            customers.Add(($"KH{i:D3}", $"Công ty {i}", 50_000_000f + i * 1_000_000f,
                           "Manufacturing", 24f, (float)(i % 4)));

        // Act
        var results = _sut.PredictAllCustomers(customers, topN: 3);

        // Assert
        results.Should().NotBeNull("Kết quả không được null");
        results.Should().HaveCount(3,
            "topN=3 phải trả về đúng 3 records");
    }

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictAllCustomers_ShouldBeSortedByLowestScoreFirst()
    {
        // Arrange: 2 khách hàng rõ ràng khác biệt
        var customers = new List<(string, string, float, string, float, float)>
        {
            // Khách hàng tốt: giá trị cao + không vi phạm + phản hồi nhanh
            ("C1", "Công ty Tốt", 200_000_000f, "Manufacturing", 12f, 0f),
            // Khách hàng xấu: giá trị thấp + nhiều vi phạm + phản hồi chậm
            ("C2", "Công ty Kém", 5_000_000f,  "Manufacturing", 200f, 5f),
        };

        // Act
        var results = _sut.PredictAllCustomers(customers, topN: 2);

        // Assert: nguy cơ cao nhất (score thấp nhất) lên đầu
        results.Should().HaveCount(2, "Phải trả về cả 2 records");
        results[0].RenewalProbabilityScore
                  .Should().BeLessOrEqualTo(results[1].RenewalProbabilityScore,
                  "Khách hàng nguy cơ cao nhất (score thấp nhất) phải xếp trước");
    }

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictAllCustomers_EmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var emptyCustomers = new List<(string, string, float, string, float, float)>();

        // Act
        var results = _sut.PredictAllCustomers(emptyCustomers, topN: 5);

        // Assert
        results.Should().NotBeNull("Kết quả không được null dù input rỗng");
        results.Should().BeEmpty("Input rỗng phải trả về danh sách rỗng");
    }

    [Fact]
    [Trait("Category", "AiIntegration")]
    public void PredictAllCustomers_TopNGreaterThanCount_ShouldReturnAll()
    {
        // Arrange: 2 khách hàng, topN = 10
        var customers = new List<(string, string, float, string, float, float)>
        {
            ("C1", "Công ty A", 50_000_000f, "Manufacturing", 24f, 0f),
            ("C2", "Công ty B", 80_000_000f, "Textile",       48f, 1f),
        };

        // Act
        var results = _sut.PredictAllCustomers(customers, topN: 10);

        // Assert: Take(10) từ list 2 phần tử → trả về 2
        results.Should().HaveCount(2,
            "Khi topN > số customer, phải trả về tất cả");
    }

    // ── Phase 4 Tests ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Phase4")]
    public void ModelMetrics_IsNullOrStub_WhenNoFreshTraining()
    {
        // Mục đích: xác nhận ModelMetrics không chứa metric giả tạo khi
        // không có CSV để train. Có 2 trường hợp hợp lệ:
        //   (A) null            → dùng fallback rule-based (không có zip/csv)
        //   (B) Accuracy == -1  → load từ .zip cache (không Evaluate lại)
        // Cả 2 đều đúng — quan trọng là KHÔNG có giá trị "0.xx" giả.
        _ = _sut.PredictRenewal(50_000_000f, "Manufacturing", 24f, 0f);

        var m = _sut.ModelMetrics;

        if (m == null)
        {
            // Case A: thuần fallback — không có csv/zip
            m.Should().BeNull("fallback rule-based: ModelMetrics phải là null");
        }
        else
        {
            // Case B: load từ zip cache — metrics là stub (-1.0)
            m.Value.Accuracy.Should().Be(-1.0,
                "Cache load stub: Accuracy phải là -1.0 (không có fresh Evaluate)");
            m.Value.AUC.Should().Be(-1.0,
                "Cache load stub: AUC phải là -1.0 (không có fresh Evaluate)");
            m.Value.TrainCount.Should().Be(-1,
                "Cache load stub: TrainCount phải là -1 (không biết số lượng train)");
        }
    }

    [Fact]
    [Trait("Category", "Phase4")]
    public void AiConstants_ViolationWarningThreshold_IsThree()
    {
        // Kiểm tra constant có giá trị đúng — phòng ai đó vô tình sửa.
        EnvContract.Common.Constants.AiConstants.ViolationWarningThreshold
            .Should().Be(3f, "ViolationWarningThreshold phải là 3 để khớp với logic IsPollutionWarning");
    }

    [Fact]
    [Trait("Category", "Phase4")]
    public void AiConstants_ContractValueDivisor_IsOneMillion()
    {
        // Kiểm tra constant chia VNĐ → triệu đồng
        EnvContract.Common.Constants.AiConstants.ContractValueDivisor
            .Should().Be(1_000_000f, "ContractValueDivisor phải là 1,000,000 để scale từ VNĐ sang triệu đồng");
    }

    [Fact]
    [Trait("Category", "Phase4")]
    public void AiConstants_ResponseTimeMultiplier_IsFifteen()
    {
        // Kiểm tra hệ số nhân ResponseTime — quan trọng cho feature scaling
        EnvContract.Common.Constants.AiConstants.ResponseTimeMultiplier
            .Should().Be(15f, "ResponseTimeMultiplier phải là 15 để mapping DB giờ vào phân phối CSV training");
    }

    [Fact]
    [Trait("Category", "Phase4")]
    public void PredictRenewal_ConcurrentCalls_AllSucceed()
    {
        // Stress test thread safety của lock(_lock) trong PredictionEngine.Predict()
        // Dù không có model (fallback), vẫn kiểm tra không có race condition.
        const int N = 20;
        var results = new AiPredictionResponse[N];
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        System.Threading.Tasks.Parallel.For(0, N, i =>
        {
            try
            {
                results[i] = _sut.PredictRenewal(
                    50_000_000f + i * 1_000_000f,
                    "Manufacturing",
                    24f,
                    (float)(i % 5));
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert: không có exception nào từ concurrent calls
        exceptions.Should().BeEmpty(
            "PredictRenewal phải thread-safe — không được throw exception khi gọi đồng thời");

        // Tất cả results phải hợp lệ
        results.Should().OnlyContain(r => r != null && r.RenewalProbabilityScore >= 0 && r.RenewalProbabilityScore <= 100,
            "Tất cả concurrent predictions phải trả về score hợp lệ trong [0, 100]");
    }
}
