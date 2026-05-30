using EnvContract.BLL.Validators;
using EnvContract.DTO.Entities;
using FluentAssertions;
using FluentValidation.Results;
using System;
using Xunit;

namespace EnvContract.Tests.Validators;

/// <summary>
/// Unit tests cho ContractValidator (FluentValidation).
/// ContractDto.TotalContractValue là decimal? — cần đặt giá trị đúng kiểu dữ liệu.
/// </summary>
public class ContractValidatorTests
{
    private readonly ContractValidator _sut = new();

    // ── Helper: tạo ContractDto hợp lệ mặc định ─────────────────────────────

    private static ContractDto ValidContract() => new()
    {
        ContractId         = "HD-2025-001",
        CustomerId         = "KH-001",
        ValidFrom          = DateTime.Now.AddDays(-30),
        ValidTo            = DateTime.Now.AddDays(335),  // còn hiệu lực 335 ngày
        TotalContractValue = 50_000_000m,               // decimal literal
        SignedDate         = DateTime.Now.AddDays(-30),
        Status             = 0,
        IndustryType       = "Manufacturing"
    };

    // ── Test: Valid contract ─────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "ContractValidator")]
    public void ValidContract_AllFieldsCorrect_ShouldPass()
    {
        ValidationResult result = _sut.Validate(ValidContract());
        result.IsValid.Should().BeTrue("Hợp đồng hợp lệ phải qua validate");
        result.Errors.Should().BeEmpty("Không có lỗi nào với contract hợp lệ");
    }

    // ── Test: ContractId ─────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "ContractValidator")]
    public void ContractId_Empty_ShouldFailWithEmptyError()
    {
        var contract = ValidContract();
        contract.ContractId = "";

        var result = _sut.Validate(contract);

        result.IsValid.Should().BeFalse("ContractId rỗng phải fail");
        result.Errors.Should().Contain(e => e.PropertyName == "ContractId",
            "Lỗi phải chỉ đúng property ContractId");
    }

    [Fact]
    [Trait("Category", "ContractValidator")]
    public void ContractId_Over50Characters_ShouldFail()
    {
        var contract = ValidContract();
        contract.ContractId = new string('X', 51); // 51 ký tự > max 50

        var result = _sut.Validate(contract);

        result.IsValid.Should().BeFalse("ContractId > 50 ký tự phải fail");
        result.Errors.Should().Contain(e
            => e.PropertyName == "ContractId" && e.ErrorMessage.Contains("50"),
            "Thông báo lỗi phải đề cập giới hạn 50 ký tự");
    }

    [Fact]
    [Trait("Category", "ContractValidator")]
    public void ContractId_Exactly50Characters_ShouldPass()
    {
        var contract = ValidContract();
        contract.ContractId = new string('A', 50); // Đúng biên trên

        var result = _sut.Validate(contract);

        result.IsValid.Should().BeTrue("ContractId đúng 50 ký tự phải pass (boundary value)");
    }

    // ── Test: CustomerId ─────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "ContractValidator")]
    public void CustomerId_Empty_ShouldFail()
    {
        var contract = ValidContract();
        contract.CustomerId = "";

        var result = _sut.Validate(contract);

        result.IsValid.Should().BeFalse("CustomerId rỗng phải fail");
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerId",
            "Lỗi phải chỉ rõ property CustomerId");
    }

    // ── Test: ValidTo/ValidFrom logic ─────────────────────────────────────────

    [Fact]
    [Trait("Category", "ContractValidator")]
    public void ValidTo_BeforeValidFrom_ShouldFail()
    {
        var contract = ValidContract();
        contract.ValidFrom = DateTime.Now.AddDays(10);
        contract.ValidTo   = DateTime.Now.AddDays(5); // ValidTo < ValidFrom

        var result = _sut.Validate(contract);

        result.IsValid.Should().BeFalse("ValidTo trước ValidFrom phải fail");
        result.Errors.Should().Contain(e => e.PropertyName == "ValidTo",
            "Lỗi phải ở property ValidTo");
    }

    [Fact]
    [Trait("Category", "ContractValidator")]
    public void ValidTo_InPast_ShouldFail()
    {
        var contract = ValidContract();
        contract.ValidFrom = DateTime.Now.AddDays(-60);
        contract.ValidTo   = DateTime.Now.AddDays(-1); // Đã hết hạn trong quá khứ

        var result = _sut.Validate(contract);

        result.IsValid.Should().BeFalse("Hợp đồng hết hạn trong quá khứ phải fail");
    }

    // ── Test: TotalContractValue ─────────────────────────────────────────────

    [Fact]
    [Trait("Category", "ContractValidator")]
    public void TotalContractValue_Negative_ShouldFail()
    {
        var contract = ValidContract();
        contract.TotalContractValue = -1m;

        var result = _sut.Validate(contract);

        result.IsValid.Should().BeFalse("Giá trị hợp đồng âm phải fail");
        result.Errors.Should().Contain(e => e.PropertyName == "TotalContractValue",
            "Lỗi phải chỉ rõ property TotalContractValue");
    }

    [Fact]
    [Trait("Category", "ContractValidator")]
    public void TotalContractValue_Zero_ShouldPass()
    {
        var contract = ValidContract();
        contract.TotalContractValue = 0m; // Biên dưới — 0 là hợp lệ (GreaterThanOrEqualTo 0)

        var result = _sut.Validate(contract);

        result.IsValid.Should().BeTrue("Giá trị hợp đồng = 0 phải pass (boundary value)");
    }
}
