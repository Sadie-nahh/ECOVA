using EnvContract.DTO.Entities;
using FluentValidation;
using System;

namespace EnvContract.BLL.Validators
{
    /// <summary>
    /// Validator cho ContractDto dùng FluentValidation AbstractValidator.
    ///
    /// Cách dùng trong ContractService:
    ///   _contractValidator.ValidateAndThrow(contract);  // Ném ValidationException tự động
    ///   hoặc:
    ///   var result = _contractValidator.Validate(contract);
    ///   if (!result.IsValid) { /* xử lý lỗi */ }
    ///
    /// Lý do chuyển sang FluentValidation:
    ///  - Báo lỗi có cấu trúc (PropertyName + ErrorMessage) thay vì chỉ 1 string
    ///  - Dễ extend thêm rule mà không sửa logic hiện có
    ///  - Consistent với ResultValidator đã dùng AbstractValidator<T>
    /// </summary>
    public class ContractValidator : AbstractValidator<ContractDto>
    {
        public ContractValidator()
        {
            RuleFor(x => x.ContractId)
                .NotEmpty()
                    .WithMessage("Mã hợp đồng không được để trống.")
                .MaximumLength(50)
                    .WithMessage("Mã hợp đồng tối đa 50 ký tự.");

            RuleFor(x => x.CustomerId)
                .NotEmpty()
                    .WithMessage("Hợp đồng phải được gán cho một Khách hàng.");

            RuleFor(x => x.ValidTo)
                .GreaterThan(x => x.ValidFrom)
                    .WithMessage("Ngày hết hiệu lực (ValidTo) phải lớn hơn Ngày bắt đầu (ValidFrom).");

            RuleFor(x => x.ValidTo)
                .Must(d => d > DateTime.Now.Date)
                    .WithMessage("Không thể tạo hợp đồng có ngày hết hạn nằm trong quá khứ.");

            RuleFor(x => x.TotalContractValue)
                .GreaterThanOrEqualTo(0)
                    .WithMessage("Giá trị hợp đồng không được âm.");
        }
    }
}