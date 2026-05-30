using EnvContract.BLL.Validators;
using EnvContract.DTO.Entities;
using FluentAssertions;
using FluentValidation.Results;
using Xunit;

namespace EnvContract.Tests.Validators;

/// <summary>
/// Unit tests cho ResultValidator.
/// TestResultDTO.ResultValue là double (không phải float như trong plan).
/// </summary>
public class ResultValidatorTests
{
    private readonly ResultValidator _sut = new();

    // ── Helper ───────────────────────────────────────────────────────────────

    private static TestResultDTO ValidResult() => new()
    {
        ResultID    = "KQ-001",
        SampleID    = "M-001",
        ParamID     = "PM-pH",
        ResultValue = 7.5,      // double, giá trị pH bình thường
        TesterID    = "NV-001",
        IsWarning   = false
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "ResultValidator")]
    public void ValidResult_AllFieldsCorrect_ShouldPass()
    {
        ValidationResult result = _sut.Validate(ValidResult());
        result.IsValid.Should().BeTrue("TestResult hợp lệ phải qua validate");
        result.Errors.Should().BeEmpty("Không có lỗi nào");
    }

    [Fact]
    [Trait("Category", "ResultValidator")]
    public void ResultValue_Negative_ShouldFail()
    {
        var dto = ValidResult();
        dto.ResultValue = -0.001; // Âm — không hợp lệ (ví dụ: nồng độ không thể âm)

        var result = _sut.Validate(dto);

        result.IsValid.Should().BeFalse("ResultValue âm phải fail");
        result.Errors.Should().Contain(e => e.PropertyName == "ResultValue",
            "Lỗi phải chỉ rõ property ResultValue");
    }

    [Fact]
    [Trait("Category", "ResultValidator")]
    public void ResultValue_Zero_ShouldPass()
    {
        var dto = ValidResult();
        dto.ResultValue = 0.0; // Biên dưới — 0 hợp lệ (GreaterThanOrEqualTo 0)

        var result = _sut.Validate(dto);

        result.IsValid.Should().BeTrue("ResultValue = 0 là hợp lệ (boundary value)");
    }

    [Fact]
    [Trait("Category", "ResultValidator")]
    public void ParamId_Empty_ShouldFail()
    {
        var dto = ValidResult();
        dto.ParamID = "";

        var result = _sut.Validate(dto);

        result.IsValid.Should().BeFalse("ParamID rỗng phải fail");
        result.Errors.Should().Contain(e => e.PropertyName == "ParamID",
            "Lỗi phải chỉ rõ property ParamID");
    }

    [Fact]
    [Trait("Category", "ResultValidator")]
    public void TesterId_Empty_ShouldFail()
    {
        var dto = ValidResult();
        dto.TesterID = "";

        var result = _sut.Validate(dto);

        result.IsValid.Should().BeFalse("TesterID rỗng phải fail");
        result.Errors.Should().Contain(e => e.PropertyName == "TesterID",
            "Lỗi phải chỉ rõ property TesterID");
    }
}
