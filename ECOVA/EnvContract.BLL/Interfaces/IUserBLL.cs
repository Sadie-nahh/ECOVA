using EnvContract.DTO.Entities;
using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    public interface IUserBLL
    {
        Task<bool> LoginAsync(string username, string password);
        /// <summary>Lấy FaceIDData từ DB để GUI so sánh bằng EmguCV Histogram.</summary>
        Task<byte[]?> GetFaceIDDataAsync(string username);
        /// <summary>Gọi sau khi GUI đã xác nhận khuôn mặt khớp — chỉ set AppState.</summary>
        Task<bool> LoginWithFaceIDAsync(string username);
        /// <summary>Đăng kí FaceID lần đầu: xác minh mật khẩu rồi lưu nh khuôn mặt vào DB.</summary>
        Task<bool> RegisterFaceIDAsync(string username, string password, byte[] faceData);
        Task<bool> LogoutAsync();
        Task ResetPasswordAsync(string username, string newPassword);
        Task ResetPasswordByEmailAsync(string email, string newPassword);
        /// <summary>Kiểm tra email có tồn tại trong DB — dùng bước 1 của ForgotPassword trước khi gửi OTP.</summary>
        Task<bool> CheckEmailExistsAsync(string email);

        Task UpdateProfileAsync(UserDTO user);
        Task UpdateAvatarAsync(string userId, byte[] avatarData);
    }
}
