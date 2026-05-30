using System;
using System.Collections.Generic;

namespace EnvContract.GUI.Services
{
    /// <summary>
    /// In-memory brute-force protection cho màn hình đăng nhập.
    /// Track số lần đăng nhập sai theo username → lockout tạm thời sau MaxAttempts lần.
    /// Thread-safe, không cần DB, reset khi app khởi động lại (bảo vệ tấn công trong phiên).
    /// 
    /// Cách dùng:
    ///   LoginThrottleService.IsLockedOut(user, out remaining) → hiển thị thông báo
    ///   LoginThrottleService.RecordFailure(user)              → sau mỗi lần sai
    ///   LoginThrottleService.RecordSuccess(user)              → sau khi thành công
    /// </summary>
    public static class LoginThrottleService
    {
        private static readonly int MaxAttempts    = AppConfig.Security.MaxLoginAttempts;
        private static readonly int LockoutMinutes = AppConfig.Security.LockoutMinutes;

        // username (case-insensitive) → (failCount, lockoutUntil)
        private static readonly Dictionary<string, (int Count, DateTime? LockedUntil)>
            _attempts = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new();

        /// <summary>
        /// Kiểm tra tài khoản có đang bị lockout không.
        /// Trả về true nếu bị khóa, out remaining = thời gian còn lại.
        /// </summary>
        public static bool IsLockedOut(string username, out TimeSpan remaining)
        {
            lock (_lock)
            {
                remaining = TimeSpan.Zero;
                if (!_attempts.TryGetValue(username, out var state)) return false;
                if (state.LockedUntil == null) return false;

                remaining = state.LockedUntil.Value - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero) return true;

                // Hết thời gian lockout → reset
                _attempts[username] = (0, null);
                return false;
            }
        }

        /// <summary>Ghi nhận 1 lần đăng nhập sai. Kích hoạt lockout nếu đủ MaxAttempts.</summary>
        public static void RecordFailure(string username)
        {
            lock (_lock)
            {
                _attempts.TryGetValue(username, out var state);
                int newCount = state.Count + 1;
                DateTime? lockUntil = newCount >= MaxAttempts
                    ? DateTime.UtcNow.AddMinutes(LockoutMinutes)
                    : (DateTime?)null;
                _attempts[username] = (newCount, lockUntil);
            }
        }

        /// <summary>Xóa lịch sử khi đăng nhập thành công.</summary>
        public static void RecordSuccess(string username)
        {
            lock (_lock) { _attempts.Remove(username); }
        }

        /// <summary>Bao nhiêu lần thử còn lại trước khi bị lockout.</summary>
        public static int RemainingAttempts(string username)
        {
            lock (_lock)
            {
                _attempts.TryGetValue(username, out var state);
                return Math.Max(0, MaxAttempts - state.Count);
            }
        }
    }
}
