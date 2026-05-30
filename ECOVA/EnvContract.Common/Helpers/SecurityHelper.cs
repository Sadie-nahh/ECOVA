using BCrypt.Net;
using System;
using System.Security.Cryptography;

namespace EnvContract.Common.Helpers
{
    public static class SecurityHelper
    {
        private const string PasswordChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789@#$!";

        /// <summary>
        /// Generates a cryptographically secure random password.
        /// Dùng RandomNumberGenerator thay vì System.Random để tránh predictability.
        /// </summary>
        public static string GenerateRandomPassword(int length = 12)
        {
            var password = new char[length];
            var randomBytes = RandomNumberGenerator.GetBytes(length);
            for (int i = 0; i < length; i++)
            {
                password[i] = PasswordChars[randomBytes[i] % PasswordChars.Length];
            }
            return new string(password);
        }

        /// <summary>
        /// Hashes a password using BCrypt.
        /// </summary>
        /// <param name="password">The plain-text password.</param>
        /// <returns>The hashed password.</returns>
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        /// <summary>
        /// Verifies a password against a hash using BCrypt.
        /// </summary>
        /// <param name="password">The plain-text password.</param>
        /// <param name="hashedPassword">The hash to verify against.</param>
        /// <returns>True if the password matches the hash, false otherwise.</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword)) return false;
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}
