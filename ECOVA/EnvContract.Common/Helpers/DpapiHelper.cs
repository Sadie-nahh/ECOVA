using System;
using System.Security.Cryptography;
using System.Text;

namespace EnvContract.Common.Helpers
{
    /// <summary>
    /// Mã hóa/giải mã chuỗi nhạy cảm (mật khẩu SMTP, API key...) dùng Windows DPAPI.
    /// Dữ liệu được gắn với Windows user account — chỉ user đang chạy app mới giải mã được.
    /// Không cần key riêng, không cần thư viện bên ngoài — dùng System.Security.Cryptography.
    /// </summary>
    public static class DpapiHelper
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ECOVA_DPAPI_2025");

        /// <summary>
        /// Mã hóa plaintext → Base64 ciphertext dùng DPAPI CurrentUser scope.
        /// Chỉ cần chạy 1 lần khi setup, lưu kết quả vào appsettings.json.
        /// </summary>
        public static string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return plaintext;
            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plaintext),
                Entropy,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// Giải mã Base64 ciphertext → plaintext.
        /// Trả về null nếu giải mã thất bại (key không khớp, dữ liệu lỗi).
        /// </summary>
        public static string? Decrypt(string cipherBase64)
        {
            if (string.IsNullOrEmpty(cipherBase64)) return cipherBase64;
            try
            {
                byte[] decrypted = ProtectedData.Unprotect(
                    Convert.FromBase64String(cipherBase64),
                    Entropy,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null; // Key không khớp hoặc dữ liệu không hợp lệ
            }
        }

        /// <summary>Kiểm tra xem một chuỗi có phải ciphertext DPAPI hợp lệ không.</summary>
        public static bool IsEncrypted(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            try { Convert.FromBase64String(value); return true; }
            catch (FormatException) { return false; } // Không phải base64 hợp lệ — chưa mã hóa
        }
    }
}
