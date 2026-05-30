using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace EnvContract.GUI
{
    /// <summary>
    /// Đọc cấu hình từ appsettings.json một lần khi khởi động (lazy singleton).
    /// Tránh hardcode magic numbers rải rác khắp project.
    /// 
    /// Cách dùng: AppConfig.FaceId.Threshold
    /// </summary>
    public static class AppConfig
    {
        private static readonly Lazy<IConfigurationRoot> _config = new(() =>
            new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build());

        // ── FaceID settings ───────────────────────────────────────────────────
        public static class FaceId
        {
            /// <summary>Ngưỡng tương đồng tối thiểu (0.0–1.0). Mặc định 0.60.</summary>
            public static double Threshold
                => double.TryParse(_config.Value["FaceId:Threshold"], out var v) ? v : 0.65;

            /// <summary>Số lần quét tự động tối đa trước khi hiện failed options.</summary>
            public static int MaxAttempts
                => int.TryParse(_config.Value["FaceId:MaxAttempts"], out var v) ? v : 5;

            /// <summary>Chu kỳ auto-scan (ms). Mặc định 2000.</summary>
            public static int ScanIntervalMs
                => int.TryParse(_config.Value["FaceId:ScanIntervalMs"], out var v) ? v : 2000;
        }

        // ── Security settings ─────────────────────────────────────────────────
        public static class Security
        {
            /// <summary>Số lần đăng nhập sai tối đa trước khi bị khóa. Mặc định 5.</summary>
            public static int MaxLoginAttempts
                => int.TryParse(_config.Value["Security:MaxLoginAttempts"], out var v) ? v : 5;

            /// <summary>Thời gian khóa tài khoản sau khi vi phạm MaxLoginAttempts (phút). Mặc định 5.</summary>
            public static int LockoutMinutes
                => int.TryParse(_config.Value["Security:LockoutMinutes"], out var v) ? v : 5;
        }

        // ── Session settings ──────────────────────────────────────────────────
        public static class Session
        {
            /// <summary>Thời gian tự đăng xuất khi không có hoạt động (phút). Mặc định 30.</summary>
            public static int TimeoutMinutes
                => int.TryParse(_config.Value["Session:TimeoutMinutes"], out var v) ? v : 30;
        }

        // ── AI / ML settings ──────────────────────────────────────────────
        public static class Ai
        {
            /// <summary>
            /// Hệ số nhân ResponseTime (DB giờ → khớp phân phối CSV training).
            /// Override AiConstants.ResponseTimeMultiplier qua appsettings.json.
            /// </summary>
            public static float ResponseTimeMultiplier
                => float.TryParse(_config.Value["Ai:ResponseTimeMultiplier"],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v)
                    ? v : EnvContract.Common.Constants.AiConstants.ResponseTimeMultiplier;

            /// <summary>ResponseTime mặc định (giờ) khi không có feedback. Override qua JSON.</summary>
            public static float DefaultResponseTimeHours
                => float.TryParse(_config.Value["Ai:DefaultResponseTimeHours"],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v)
                    ? v : EnvContract.Common.Constants.AiConstants.DefaultResponseTimeHours;

            /// <summary>Ngưỡng số vi phạm để đặt IsPollutionWarning. Override qua JSON.</summary>
            public static float ViolationWarningThreshold
                => float.TryParse(_config.Value["Ai:ViolationWarningThreshold"],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v)
                    ? v : EnvContract.Common.Constants.AiConstants.ViolationWarningThreshold;

            /// <summary>Giá trị hợp đồng max (triệu đồng) cap trước khi vào model. Override qua JSON.</summary>
            public static float ContractValueCap
                => float.TryParse(_config.Value["Ai:ContractValueCap"],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v)
                    ? v : EnvContract.Common.Constants.AiConstants.ContractValueCap;
        }

        // ── VoiceSearch settings ──────────────────────────────────────────
        public static class VoiceSearch
        {
            /// <summary>
            /// Delay (ms) sau khi cancel để Chrome audio stream giải phóng microphone.
            /// Chrome cần ~300-500ms sau recognition.stop() trước khi start() được chấp nhận.
            /// Mặc định 500.
            /// </summary>
            public static int MicReleaseDelayMs
                => int.TryParse(_config.Value["VoiceSearch:MicReleaseDelayMs"], out var v) ? v : 500;

            /// <summary>
            /// Timeout tổng (ms) cho 1 lần ghi âm chính. Mặc định 15000 (15 giây).
            /// </summary>
            public static int RecordingTimeoutMs
                => int.TryParse(_config.Value["VoiceSearch:RecordingTimeoutMs"], out var v) ? v : 15_000;

            /// <summary>
            /// Silence threshold (ms) — ngừng ghi khi không phát hiện tiếng nói sau khoảng này.
            /// Mặc định 2000 (2 giây).
            /// </summary>
            public static int RecordingSilenceMs
                => int.TryParse(_config.Value["VoiceSearch:RecordingSilenceMs"], out var v) ? v : 2_000;

            /// <summary>
            /// Thời gian (ms) background warm-up ListenAsync để thiết lập kết nối MS Speech Server.
            /// Mặc định 6000 (6 giây).
            /// </summary>
            public static int WarmupDurationMs
                => int.TryParse(_config.Value["VoiceSearch:WarmupDurationMs"], out var v) ? v : 6_000;

            /// <summary>
            /// Silence threshold (ms) cho warm-up — ngắn hơn để cancel nhanh khi user click lần 2.
            /// Mặc định 500.
            /// </summary>
            public static int WarmupSilenceMs
                => int.TryParse(_config.Value["VoiceSearch:WarmupSilenceMs"], out var v) ? v : 500;

            /// <summary>
            /// Polling interval (ms) khi chờ listenTask hoàn thành. Mặc định 80.
            /// </summary>
            public static int PollingIntervalMs
                => int.TryParse(_config.Value["VoiceSearch:PollingIntervalMs"], out var v) ? v : 80;
        }
    }
}
