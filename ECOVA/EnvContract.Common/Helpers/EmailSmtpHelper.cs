using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace EnvContract.Common.Helpers
{
    /// <summary>
    /// Helper gửi email qua SMTP — thread-safe, có timeout, validate email format.
    /// Dùng singleton SmtpClient (không tạo mới mỗi lần gửi).
    /// Thông tin cấu hình được đẩy vào từ Program.cs qua phương thức Configure().
    /// </summary>
    public static class EmailSmtpHelper
    {
        // ── Config ────────────────────────────────────────────────────────────
        private static string _smtpServer   = "smtp.gmail.com";
        private static int    _smtpPort     = 587;
        private static string _smtpUser     = "";
        private static string _smtpPass     = "";
        private static bool   _enableSsl    = true;
        private static string _displayName  = "ECOVA System (No Reply)";
        private static bool   _isConfigured = false;

        /// <summary>Timeout mặc định 15 giây mỗi lần gửi email.</summary>
        public static int TimeoutSeconds { get; set; } = 15;

        // ── Singleton SmtpClient + concurrency guard ──────────────────────────
        private static SmtpClient?      _client;
        private static readonly SemaphoreSlim _guard = new SemaphoreSlim(1, 1);
        private static readonly object  _initLock   = new();

        // ── Email format check ────────────────────────────────────────────────
        private static readonly Regex _emailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Khởi tạo cài đặt SMTP. Gọi 1 lần từ Program.cs sau khi đọc appsettings.json.
        /// Nếu gọi lại (reconfigure), singleton SmtpClient cũ sẽ bị dispose và tạo mới.
        /// </summary>
        public static void Configure(
            string server,
            int    port,
            string username,
            string password,
            bool   enableSsl   = true,
            string displayName = "ECOVA System (No Reply)")
        {
            lock (_initLock)
            {
                _smtpServer  = server      ?? _smtpServer;
                _smtpPort    = port > 0    ? port : _smtpPort;
                _smtpUser    = username    ?? "";
                _smtpPass    = password    ?? "";
                _enableSsl   = enableSsl;
                _displayName = displayName ?? _displayName;
                _isConfigured = true;

                // Dispose client cũ nếu re-configure
                _client?.Dispose();
                _client = null;
            }
        }

        /// <summary>
        /// Kiểm tra cấu hình còn hợp lệ không (để UI hiện cảnh báo nếu chưa setup).
        /// </summary>
        public static bool IsConfigured => _isConfigured
            && !string.IsNullOrWhiteSpace(_smtpUser)
            && !string.IsNullOrWhiteSpace(_smtpPass);

        /// <summary>
        /// Gửi Email bất đồng bộ — thread-safe, có timeout, validate format.
        /// Trả về true nếu gửi thành công, false nếu có lỗi (lỗi đã được log).
        /// </summary>
        /// <param name="toEmail">Địa chỉ email người nhận.</param>
        /// <param name="subject">Tiêu đề email.</param>
        /// <param name="body">Nội dung HTML.</param>
        /// <param name="cancellationToken">Token huỷ optional.</param>
        public static async Task<bool> SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            // ── 1. Pre-condition checks ────────────────────────────────────────
            if (!IsConfigured)
            {
                Log.Warning("[Email] SMTP chưa được cấu hình. Gọi EmailSmtpHelper.Configure() trước.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(toEmail) || !_emailRegex.IsMatch(toEmail))
            {
                Log.Warning("[Email] Địa chỉ email không hợp lệ: '{ToEmail}'", toEmail);
                return false;
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                Log.Warning("[Email] Tiêu đề email không được để trống.");
                return false;
            }

            // ── 2. Acquire concurrency guard với timeout ───────────────────────
            bool acquired = await _guard.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds), cancellationToken)
                .ConfigureAwait(false);
            if (!acquired)
            {
                Log.Warning("[Email] SMTP đang gửi email khác, thử lại sau.");
                return false;
            }

            try
            {
                // ── 3. Lazy init singleton SmtpClient ─────────────────────────
                if (_client == null)
                {
                    lock (_initLock)
                    {
                        if (_client == null)
                        {
                            _client = new SmtpClient(_smtpServer, _smtpPort)
                            {
                                UseDefaultCredentials = false,          // QUAN TRỌNG: tắt Windows auth
                                Credentials  = new NetworkCredential(_smtpUser, _smtpPass),
                                EnableSsl    = _enableSsl,
                                DeliveryMethod = SmtpDeliveryMethod.Network,
                                Timeout      = TimeoutSeconds * 1000   // ms
                            };
                        }
                    }
                }

                // ── 4. Build message ───────────────────────────────────────────
                using var mailMsg = new MailMessage
                {
                    From       = new MailAddress(_smtpUser, _displayName, Encoding.UTF8),
                    Subject    = subject,
                    Body       = body,
                    IsBodyHtml = true,
                    BodyEncoding    = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                mailMsg.To.Add(new MailAddress(toEmail));

                // ── 5. Send với CancellationToken ──────────────────────────────
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

                await _client.SendMailAsync(mailMsg, cts.Token).ConfigureAwait(false);

                Log.Information("[Email] Gửi thành công tới {ToEmail} | Chủ đề: {Subject}", toEmail, subject);
                return true;
            }
            catch (OperationCanceledException)
            {
                Log.Warning("[Email] Timeout khi gửi email tới {ToEmail} ({S}s)", toEmail, TimeoutSeconds);
                // Reset client để lần sau thử lại với connection mới
                lock (_initLock) { _client?.Dispose(); _client = null; }
                return false;
            }
            catch (SmtpException ex)
            {
                Log.Error(ex, "[Email] Lỗi SMTP ({Code}) khi gửi tới {ToEmail}", ex.StatusCode, toEmail);
                // Reset client nếu lỗi kết nối
                if (ex.StatusCode == SmtpStatusCode.ServiceNotAvailable ||
                    ex.StatusCode == SmtpStatusCode.GeneralFailure)
                {
                    lock (_initLock) { _client?.Dispose(); _client = null; }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Email] Lỗi không xác định khi gửi tới {ToEmail}", toEmail);
                return false;
            }
            finally
            {
                _guard.Release();
            }
        }

        /// <summary>
        /// Dispose SmtpClient khi app tắt. Gọi từ Program.cs trước Log.CloseAndFlush().
        /// </summary>
        public static void Shutdown()
        {
            lock (_initLock)
            {
                _client?.Dispose();
                _client = null;
            }
        }
    }
}