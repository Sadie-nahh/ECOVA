using EnvContract.BLL.Interfaces;
using EnvContract.Common;
using EnvContract.Common.Helpers;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using Serilog;
using System;
using System.Text;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    public class UserBLL : IUserBLL
    {
        private readonly IUserRepository _userRepository;

        public UserBLL(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return false;

            try
            {
                // B2: Gộp thành 1 DB query — GetByUsernameAsync đã SELECT PasswordHash
                var user = await _userRepository.GetByUsernameAsync(username);
                if (user != null
                    && !string.IsNullOrEmpty(user.PasswordHash)
                    && SecurityHelper.VerifyPassword(password, user.PasswordHash))
                {
                    AppState.Instance.CurrentUser = user;

                    // Ghi audit log đăng nhập thành công
                    _ = EnvContract.DAL.Repositories.SystemAuditHelper.LogAsync(
                        user.UserID, "LOGIN",
                        detail: $"Username={username}, Máy={Environment.MachineName}");

                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Login] Lỗi kết nối DB khi đăng nhập user '{Username}'", username);
            }

            return false;
        }

        /// <summary>
        /// Lấy FaceIDData từ DB và giải mã AES-256 để GUI so sánh bằng EmguCV Histogram.
        /// Dữ liệu trong DB được lưu dưới dạng JPEG bytes đã mã hóa (Base64 AES → byte[]).
        /// Fallback: nếu row cũ chưa mã hóa → trả về raw bytes để không bị vỡ backward compat.
        /// Trả về:
        ///   null       = tài khoản không tồn tại hoặc đã bị khoá
        ///   byte[0]    = tài khoản có nhưng CHƯA đăng ký FaceID
        ///   byte[n>0]  = JPEG khuôn mặt đã giải mã, sẵn sàng so sánh
        /// </summary>
        public async Task<byte[]?> GetFaceIDDataAsync(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;

            try
            {
                var user = await _userRepository.GetByUsernameAsync(username);
                if (user == null || !user.IsActive) return null;

                if (!user.IsFaceIDRegistered || user.FaceIDData == null || user.FaceIDData.Length == 0)
                    return Array.Empty<byte>();

                // Phase 6.1: Giải mã AES-256 face data trước khi trả về cho GUI
                // Fallback: nếu data cũ (chưa mã hóa) → DecryptBytes trả null → dùng raw bytes
                string base64 = Encoding.UTF8.GetString(user.FaceIDData);
                byte[]? decrypted = AesHelper.DecryptBytes(base64);
                return decrypted ?? user.FaceIDData;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[FaceID] Lỗi lấy dữ liệu FaceID cho user '{Username}'", username);
                return null;
            }
        }


        /// <summary>
        /// Được gọi sau khi GUI đã xác nhận histogram khớp.
        /// Chỉ tải thông tin user vào AppState.CurrentUser để hoàn tất đăng nhập.
        /// </summary>
        public async Task<bool> LoginWithFaceIDAsync(string username)
        {
            if (string.IsNullOrEmpty(username)) return false;

            try
            {
                var user = await _userRepository.GetByUsernameAsync(username);
                if (user == null || !user.IsActive) return false;

                AppState.Instance.CurrentUser = user;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[FaceID] Lỗi set session cho user '{Username}'", username);
                return false;
            }
        }

        /// <summary>
        /// Đăng kí FaceID lần đầu: xác minh mật khẩu trước để đảm bảo đúng chủ tài khoản,
        /// sau đó lưu ảnh khuôn mặt vào DB.
        /// </summary>
        public async Task<bool> RegisterFaceIDAsync(string username, string password, byte[] faceData)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)
                || faceData == null || faceData.Length == 0)
                return false;

            try
            {
                // 1. Xác minh mật khẩu — đảm bảo đúng chủ tài khoản
                string hash = await _userRepository.GetPasswordHashByUsernameAsync(username);
                if (hash == null || !SecurityHelper.VerifyPassword(password, hash))
                    return false;

                // 2. Lấy UserID
                var user = await _userRepository.GetByUsernameAsync(username);
                if (user == null || !user.IsActive) return false;

                // 3. Mã hóa face data AES-256 trước khi lưu vào DB (Phase 6.1)
                // FaceIDData VARBINARY(MAX) lưu JPEG bytes thuần → biometric leak nếu DB dump.
                // Giải pháp: EncryptBytes(faceData) → Base64 string → UTF8 bytes → lưu vào DB.
                string encryptedBase64 = AesHelper.EncryptBytes(faceData);
                byte[] encryptedFace   = Encoding.UTF8.GetBytes(encryptedBase64);
                await _userRepository.RegisterFaceIDAsync(user.UserID, encryptedFace);

                Log.Information("[FaceID] Đăng ký thành công (AES-encrypted) cho user '{Username}'", username);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[FaceID] Lỗi đăng ký FaceID cho user '{Username}'", username);
                return false;
            }
        }

        // B3: Bỏ fake async — không cần async keyword + await Task.FromResult
        public Task<bool> LogoutAsync()
        {
            AppState.Instance.Logout();
            return Task.FromResult(true);
        }

        public async Task ResetPasswordAsync(string username, string newPassword)
        {
            string newHashedPassword = SecurityHelper.HashPassword(newPassword);
            await _userRepository.UpdatePasswordAsync(username, newHashedPassword);
        }

        public async Task ResetPasswordByEmailAsync(string email, string newPassword)
        {
            string newHashedPassword = SecurityHelper.HashPassword(newPassword);
            await _userRepository.UpdatePasswordByEmailAsync(email, newHashedPassword);
        }

        public async Task<bool> CheckEmailExistsAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            return user != null;
        }


        public async Task UpdateProfileAsync(UserDTO user)
        {
            await _userRepository.UpdateProfileAsync(user);
        }

        public async Task UpdateAvatarAsync(string userId, byte[] avatarData)
        {
            // Lưu vào cột AvatarData (KHÔNG phải FaceIDData)
            await _userRepository.UpdateAvatarDataAsync(userId, avatarData);
            // Cập nhật AppState để sidebar refresh ngay
            if (AppState.Instance.CurrentUser?.UserID == userId)
                AppState.Instance.CurrentUser.AvatarData = avatarData;
        }
    }
}
