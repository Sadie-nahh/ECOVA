using EnvContract.BLL.Interfaces;
using EnvContract.Common.Helpers;
using EnvContract.DAL.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    /// <summary>
    /// Service xử lý OTP quên mật khẩu — tách biệt khỏi UserBLL.
    ///
    /// Thiết kế:
    ///  - OTP lưu in-memory (Dictionary thread-safe) với expiry 5 phút
    ///  - OTP dùng 1 lần: xóa ngay sau khi validate thành công
    ///  - Không persist OTP vào DB (bảo mật + đơn giản)
    ///
    /// Lý do tách thành service riêng:
    ///  - SRP: UserBLL đã đảm nhận login/FaceID/profile
    ///  - Dễ unit test: OTP logic độc lập, không phụ thuộc DB
    ///  - Sau này có thể chuyển sang Redis/cache mà không ảnh hưởng UserBLL
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;

        // email (case-insensitive) → (OTP code, expiry UTC)
        private static readonly Dictionary<string, (string Code, DateTime ExpiresAt)>
            _otpStore = new(StringComparer.OrdinalIgnoreCase);

        // email → (số lần nhập sai, thời điểm hết window)
        // Bảo vệ chống brute-force: tối đa 5 lần sai trong 15 phút.
        private static readonly Dictionary<string, (int Count, DateTime ResetAt)>
            _otpAttempts = new(StringComparer.OrdinalIgnoreCase);

        private static readonly object _otpLock = new();

        public AuthService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>
        /// Tạo OTP 6 chữ số bằng CSPRNG, lưu vào memory dict với expiry 5 phút.
        /// Đồng thời reset attempt counter của email này (vì OTP mới được cấp).
        /// Gọi code trả về rồi gửi email ngoài service này.
        /// </summary>
        public string GenerateOtp(string email)
        {
            // CSPRNG — RandomNumberGenerator thay thế Random.Shared
            // Random.Shared là thread-safe nhưng KHÔNG phải CSPRNG —
            // kẻ tấn công có thể predict giá trị tiếp theo nếu biết seed/state.
            byte[] bytes = RandomNumberGenerator.GetBytes(4);
            int raw = BitConverter.ToInt32(bytes, 0) & 0x7FFF_FFFF; // Loại symbol bit
            string code = (raw % 900_000 + 100_000).ToString("D6"); // Đảm bảo 6 chữ số

            lock (_otpLock)
            {
                _otpStore[email]    = (code, DateTime.UtcNow.AddMinutes(5));
                _otpAttempts.Remove(email); // Reset counter khi cấp OTP mới
            }

            Log.Information("[Auth] OTP generated (CSPRNG) cho {Email} — hết hạn sau 5 phút.", email);
            return code;
        }

        /// <summary>
        /// Xác thực OTP với rate limiting (tối đa 5 lần sai / 15 phút).
        /// Trả về true nếu code đúng và chưa hết hạn.
        /// OTP bị xóa sau khi validate thành công (one-time use).
        /// <para>
        /// <b>Lưu ý</b>: Synchronous internally (Task.FromResult) — không thực sự async.
        /// Giữ <c>Task&lt;bool&gt;</c> để interface không bị breaking change khi
        /// chuyển sang Redis/distributed cache. Xem <see cref="IAuthService.ValidateOtpAsync"/>.
        /// </para>
        /// </summary>
        public Task<bool> ValidateOtpAsync(string email, string otp)
        {
            lock (_otpLock)
            {
                // ── 1. Rate limit check ───────────────────────────────────────────────
                if (_otpAttempts.TryGetValue(email, out var attempts))
                {
                    if (DateTime.UtcNow >= attempts.ResetAt)
                    {
                        // Hết window → reset counter
                        _otpAttempts.Remove(email);
                    }
                    else if (attempts.Count >= 5)
                    {
                        // Trong window và đã vượt giới hạn
                        Log.Warning("[Auth] OTP rate limited cho {Email} — quá 5 lần sai trong window.", email);
                        return Task.FromResult(false);
                    }
                }

                // ── 2. Kiểm tra OTP tồn tại ─────────────────────────────────────────
                if (!_otpStore.TryGetValue(email, out var entry))
                {
                    Log.Warning("[Auth] Không tìm thấy OTP cho {Email}.", email);
                    return Task.FromResult(false);
                }

                // ── 3. Kiểm tra hết hạn ────────────────────────────────────────────
                if (DateTime.UtcNow > entry.ExpiresAt)
                {
                    _otpStore.Remove(email);
                    Log.Warning("[Auth] OTP cho {Email} đã hết hạn.", email);
                    return Task.FromResult(false);
                }

                // ── 4. So sánh OTP ──────────────────────────────────────────────────
                bool isValid = string.Equals(entry.Code, otp?.Trim(), StringComparison.Ordinal);

                if (isValid)
                {
                    _otpStore.Remove(email);    // One-time use: xóa sau khi dùng
                    _otpAttempts.Remove(email); // Reset counter khi đúng
                    Log.Information("[Auth] OTP hợp lệ cho {Email}.", email);
                }
                else
                {
                    // Track attempt: tăng counter trong window 15 phút
                    var cur = _otpAttempts.TryGetValue(email, out var prev)
                        ? prev
                        : (Count: 0, ResetAt: DateTime.UtcNow.AddMinutes(15));
                    _otpAttempts[email] = (cur.Count + 1, cur.ResetAt);
                    Log.Warning("[Auth] OTP sai cho {Email}. Lần {N}/5.", email, cur.Count + 1);
                }

                return Task.FromResult(isValid);
            }
        }

        /// <summary>
        /// Đổi mật khẩu theo email (gọi sau khi ValidateOtp trả về true).
        /// Hash BCrypt trước khi lưu — không lưu plaintext.
        /// </summary>
        public async Task<bool> ChangePasswordByEmailAsync(string email, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            try
            {
                string hash = SecurityHelper.HashPassword(newPassword);
                await _userRepository.UpdatePasswordByEmailAsync(email, hash);
                Log.Information("[Auth] Đã đổi mật khẩu thành công cho {Email}.", email);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Auth] Lỗi đổi mật khẩu cho {Email}.", email);
                return false;
            }
        }
    }
}
