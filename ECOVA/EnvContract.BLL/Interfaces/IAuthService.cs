using EnvContract.DTO.Entities;
using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    /// <summary>
    /// Service xử lý xác thực quên mật khẩu qua OTP email.
    /// Tách biệt khỏi IUserBLL để tuân thủ Single Responsibility Principle.
    /// 
    /// Quy trình:
    ///   1. User nhập email → GenerateOtp(email) → gửi email OTP
    ///   2. User nhập mã 6 số → ValidateOtpAsync(email, code) → true/false
    ///   3. Nếu hợp lệ → ChangePasswordAsync(email, newPassword)
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Tạo OTP 6 chữ số và lưu vào memory với thời gian hết hạn 5 phút.
        /// Trả về code để gọi nơi gửi email.
        /// </summary>
        string GenerateOtp(string email);

        /// <summary>
        /// Xác thực OTP — kiểm tra đúng code và chưa hết hạn.
        /// OTP chỉ dùng được 1 lần (bị xóa sau khi validate thành công).
        /// <para>
        /// <b>Lưu ý kiến trúc</b>: Mệnh đề này ảo (thực hiện synchronous bằng
        /// in-memory dictionary + lock). Interface signature là <c>Task&lt;bool&gt;</c>
        /// để dễ chuyển sang distributed cache (Redis, SQL) sau này mà không phải
        /// đổi toàn bộ caller. Không cần thêm <c>ConfigureAwait</c> vì
        /// impl trả <c>Task.FromResult</c> ngay lập tức.
        /// </para>
        /// </summary>
        Task<bool> ValidateOtpAsync(string email, string otp);

        /// <summary>
        /// Đổi mật khẩu bằng email (sau khi OTP đã được xác thực).
        /// Hash mật khẩu mới bằng BCrypt trước khi lưu.
        /// </summary>
        Task<bool> ChangePasswordByEmailAsync(string email, string newPassword);
    }
}
