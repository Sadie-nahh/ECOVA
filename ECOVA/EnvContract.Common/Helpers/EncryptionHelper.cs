using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EnvContract.Common.Helpers
{
    /// <summary>
    /// Helper mã hóa/giải mã dữ liệu nhạy cảm bằng AES-256.
    /// Dùng để mã hóa thông tin cá nhân (CCCD, số điện thoại) khi lưu vào DB nếu cần.
    /// </summary>
    public static class EncryptionHelper
    {
        // Key và IV mặc định (32 bytes = AES-256)
        // Trong production nên đọc từ appsettings.json hoặc biến môi trường
        private static readonly byte[] DefaultKey = Encoding.UTF8.GetBytes("ECOVA_AES256_KEY_32BYTES_12345678");
        private static readonly byte[] DefaultIV  = Encoding.UTF8.GetBytes("ECOVA_IV_16BYTES");

        /// <summary>
        /// Mã hóa chuỗi bằng AES-256 CBC, trả về Base64 string.
        /// </summary>
        public static string Encrypt(string plainText, byte[]? key = null, byte[]? iv = null)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            byte[] keyBytes = key ?? DefaultKey;
            byte[] ivBytes  = iv  ?? DefaultIV;

            using var aes = Aes.Create();
            aes.KeySize  = 256;
            aes.Key = keyBytes[..32];  // Đảm bảo đúng 32 bytes
            aes.IV  = ivBytes[..16];   // Đảm bảo đúng 16 bytes
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Giải mã chuỗi Base64 đã được mã hóa bằng AES-256 CBC.
        /// </summary>
        public static string Decrypt(string cipherText, byte[]? key = null, byte[]? iv = null)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                byte[] keyBytes = key ?? DefaultKey;
                byte[] ivBytes  = iv  ?? DefaultIV;

                using var aes = Aes.Create();
                aes.KeySize  = 256;
                aes.Key = keyBytes[..32];
                aes.IV  = ivBytes[..16];
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes  = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // Không thể giải mã — trả về chuỗi gốc
                return cipherText;
            }
        }
    }
}
