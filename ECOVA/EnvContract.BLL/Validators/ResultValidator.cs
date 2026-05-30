using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using FluentValidation;
using System.Threading.Tasks;

namespace EnvContract.BLL.Validators
{
    /// <summary>
    /// Input validation (application-level) cho TestResultDTO.
    /// Chỉ kiểm tra tính hợp lệ của dữ liệu đầu vào trước khi gửi xuống DB.
    ///
    /// IsWarning (kiểm tra vượt ngưỡng QCVN) đã được chuyển hoàn toàn xuống DB:
    ///   - Trigger trg_TestResults_AutoWarning tự động tính dựa trên RegulationLimits.
    ///   - fn_IsResultExceedingLimit có thể dùng trong các SP khác khi cần.
    /// </summary>
    public class ResultValidator : AbstractValidator<TestResultDTO>
    {
        public ResultValidator()
        {
            RuleFor(x => x.ResultValue)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Giá trị kết quả không được âm.");

            RuleFor(x => x.ParamID)
                .NotEmpty()
                .WithMessage("Mã thông số không được để trống.");

            RuleFor(x => x.TesterID)
                .NotEmpty()
                .WithMessage("Người nhập kết quả không được trống.");
        }
    }
}
