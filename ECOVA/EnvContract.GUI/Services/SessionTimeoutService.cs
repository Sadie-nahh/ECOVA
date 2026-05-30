#nullable enable
using System;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
namespace EnvContract.GUI.Services
{
    /// <summary>
    /// Session inactivity timeout — tự logout khi user không tương tác trong N phút.
    /// Sử dụng WinForms Timer (UI thread) để an toàn khi gọi UI.
    /// 
    /// Cách dùng trong MainForm:
    ///   OnLoad:          SessionTimeoutService.Start(minutes, callback)
    ///   OnMouseMove:     SessionTimeoutService.Reset()
    ///   ProcessKeyPreview: SessionTimeoutService.Reset()
    ///   OnFormClosed:    SessionTimeoutService.Stop()
    /// </summary>
    public static class SessionTimeoutService
    {
        private static Timer?  _timer;
        private static Action? _onTimeout;

        /// <summary>
        /// Bắt đầu đếm ngược. Sau timeoutMinutes không có activity → gọi onTimeout.
        /// </summary>
        public static void Start(int timeoutMinutes, Action onTimeout)
        {
            Stop();
            _onTimeout = onTimeout;
            _timer = new Timer { Interval = timeoutMinutes * 60_000 };
            _timer.Tick += (_, _) =>
            {
                Stop();
                _onTimeout?.Invoke();
            };
            _timer.Start();
            AppLogger.Info($"Session: Timeout đặt {timeoutMinutes} phút.");
        }

        /// <summary>Reset đồng hồ khi có activity (mouse/keyboard).</summary>
        public static void Reset()
        {
            if (_timer == null || !_timer.Enabled) return;
            _timer.Stop();
            _timer.Start();
        }

        /// <summary>Dừng và giải phóng timer.</summary>
        public static void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        public static bool IsRunning => _timer?.Enabled == true;
    }
}
