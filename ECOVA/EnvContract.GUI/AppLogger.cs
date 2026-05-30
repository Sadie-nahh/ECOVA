#nullable enable
using Serilog;

namespace EnvContract.GUI
{
    // ─────────────────────────────────────────────────────────────────────────
    // IAppLogger — interface cho DI injection.
    // Dùng khi cần inject logger vào form/service qua constructor.
    // ─────────────────────────────────────────────────────────────────────────
    public interface IAppLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warning(string message);
        void Error(string message, System.Exception? ex = null);
        void Fatal(string message, System.Exception? ex = null);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AppLogger — static wrapper (backward-compatible) + IAppLogger implementation.
    //
    // Hai cách dùng:
    //   1. Static:   AppLogger.Info("msg")          — dùng ở code hiện tại
    //   2. Inject:   IAppLogger logger → logger.Info — dùng khi viết code mới
    //
    // Tại sao giữ cả hai?
    //   → Hàng trăm call site cũ không cần sửa (static vẫn hoạt động).
    //   → Form/service mới sẽ inject qua DI → testable, mockable.
    // ─────────────────────────────────────────────────────────────────────────
    public class AppLogger : IAppLogger
    {
        private static ILogger _logger = Log.Logger;

        /// <summary>Gọi từ Program.cs sau khi config Serilog.</summary>
        public static void Initialize(ILogger logger) => _logger = logger;

        // ── Instance methods (IAppLogger implementation) ──────────────────────
        void IAppLogger.Debug(string msg)                       => _logger.Debug(msg);
        void IAppLogger.Info(string msg)                        => _logger.Information(msg);
        void IAppLogger.Warning(string msg)                     => _logger.Warning(msg);
        void IAppLogger.Error(string msg, System.Exception? ex) => Error(msg, ex);
        void IAppLogger.Fatal(string msg, System.Exception? ex) => Fatal(msg, ex);

        // ── Static methods (backward-compatible với code cũ) ──────────────────
        public static void Debug(string message)   => _logger.Debug(message);
        public static void Info(string message)    => _logger.Information(message);
        public static void Warning(string message) => _logger.Warning(message);

        public static void Error(string message, System.Exception? ex = null)
        {
            if (ex != null) _logger.Error(ex, message);
            else            _logger.Error(message);
        }

        public static void Fatal(string message, System.Exception? ex = null)
        {
            if (ex != null) _logger.Fatal(ex, message);
            else            _logger.Fatal(message);
        }
    }
}
