using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EnvContract.Common.Helpers
{
    /// <summary>
    /// Mã hóa/giải mã mật khẩu SMTP bằng AES-256 với key cố định trong ứng dụng.
    /// KHÁC với DPAPI: hoạt động trên MỌI máy tính, mọi tài khoản Windows.
    /// Dùng cho settings cần chia sẻ giữa nhiều máy (appsettings.json).
    ///
    /// Security model: Security-by-obscurity — key được nhúng trong binary.
    /// Phù hợp cho ứng dụng desktop học thuật/nội bộ.
    /// </summary>
    public static class AesHelper
    {
        /// <summary>Prefix nhận diện ciphertext AES (phân biệt với DPAPI cũ).</summary>
        public const string Prefix = "AES:";

        // ── Key derivation (SHA-256 → 32 bytes, MD5 → 16 bytes) ──────────
        // Key và IV cố định, nhúng trong binary — giống như mọi desktop app
        // lưu credentials (FileZilla, VS, SSMS, etc.)
        private static readonly byte[] _key;
        private static readonly byte[] _iv;

        static AesHelper()
        {
            using var sha256 = SHA256.Create();
            // Ưu tiên đọc từ environment variable (production/secure deployment).
            // Fallback sang literal khi không có env var (dev/demo — Security-by-obscurity).
            // Lưu ý: nếu thay ECOVA_AES_KEY, phải re-encrypt lại SMTP password trong appsettings.json.
            string keySource = Environment.GetEnvironmentVariable("ECOVA_AES_KEY")
                ?? "ECOVA|SMTP|AES256|KEY|2025|ENV_MONITOR";
            _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(keySource));

            using var md5 = MD5.Create();
            _iv = md5.ComputeHash(Encoding.UTF8.GetBytes("ECOVA|IV|FIXED|2025"));
        }

        /// <summary>
        /// Mã hóa plaintext → "AES:" + Base64 ciphertext.
        /// Kết quả có thể lưu vào appsettings.json và dùng trên mọi máy.
        /// </summary>
        public static string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return plaintext;

            using var aes = Aes.Create();
            aes.Key     = _key;
            aes.IV      = _iv;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms        = new MemoryStream();
            using var encryptor = aes.CreateEncryptor();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs, Encoding.UTF8))
                sw.Write(plaintext);

            return Prefix + Convert.ToBase64String(ms.ToArray());
        }

        /// <summary>
        /// Giải mã "AES:BASE64" → plaintext.
        /// Trả về null nếu chuỗi không đúng định dạng hoặc giải mã thất bại.
        /// </summary>
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            // Bỏ prefix nếu có
            string base64 = cipherText.StartsWith(Prefix, StringComparison.Ordinal)
                ? cipherText.Substring(Prefix.Length)
                : cipherText;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(base64);

                using var aes = Aes.Create();
                aes.Key     = _key;
                aes.IV      = _iv;
                aes.Mode    = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var ms        = new MemoryStream(cipherBytes);
                using var decryptor = aes.CreateDecryptor();
                using var cs        = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr        = new StreamReader(cs, Encoding.UTF8);
                return sr.ReadToEnd();
            }
            catch
            {
                #pragma warning disable CS8603
                return null; // Sai key, sai format, hoặc dữ liệu bị corrupt
                #pragma warning restore CS8603

            }
        }


        // ── Byte array overloads (Phase 6 — FaceID biometric at rest) ────────────
        // Dùng để mã hóa FaceIDData (JPEG binary) trước khi lưu vào DB.
        // Key/IV giống hệt string overloads → dùng chung cùng AES-256 CBC setup.
        // Lưu ý: EncryptBytes KHÔNG thêm "AES:" prefix vì kết quả trả về string
        //        sẽ được encode lại lần nữa thành byte[] qua Encoding.UTF8.GetBytes().
        //        DecryptBytes hỗ trợ fallback: nếu data chưa mã hóa (byte[] thuần JPEG)
        //        → gọi giải mã thất bại → caller dùng raw bytes (backward compat).

        /// <summary>
        /// Mã hóa byte array (e.g. JPEG face data) → Base64 ciphertext string bằng AES-256 CBC.
        /// Kết quả là pure Base64 (không có prefix "AES:") để tiện lưu lại dưới dạng byte[].
        /// </summary>
        /// <param name="data">Raw binary data cần mã hóa (JPEG bytes, v.v.)</param>
        /// <returns>Base64 ciphertext string, hoặc empty nếu data null/rỗng.</returns>
        public static string EncryptBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;

            using var aes = Aes.Create();
            aes.Key     = _key;
            aes.IV      = _iv;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();
            return Convert.ToBase64String(ms.ToArray());
        }

        /// <summary>
        /// Giải mã Base64 ciphertext string → byte array gốc.
        /// Trả về null nếu base64 không hợp lệ hoặc giải mã thất bại
        /// (ví dụ: dữ liệu cũ chưa mã hóa — caller dùng raw bytes làm fallback).
        /// </summary>
        /// <param name="base64">Base64 ciphertext string (không có prefix "AES:").</param>
        /// <returns>Byte array giải mã, hoặc null nếu thất bại.</returns>
        public static byte[]? DecryptBytes(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(base64);

                using var aes = Aes.Create();
                aes.Key     = _key;
                aes.IV      = _iv;
                aes.Mode    = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var ms     = new MemoryStream(cipherBytes);
                using var cs     = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var result = new MemoryStream();
                cs.CopyTo(result);
                return result.ToArray();
            }
            catch
            {
                return null; // Dữ liệu không phải ciphertext (chưa mã hóa hoặc bị corrupt)
            }
        }

        /// <summary>Kiểm tra chuỗi có phải AES ciphertext hợp lệ không.</summary>
        public static bool IsAesCipherText(string value)
            => !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
