using EnvContract.BLL.Interfaces;
using EnvContract.Common.Helpers;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IContractRepository _contractRepository;

        public NotificationService(IContractRepository contractRepository)
        {
            _contractRepository = contractRepository;
        }

        /// <summary>
        /// Gửi email cảnh báo tới các khách hàng có hợp đồng sắp hết hạn trong 15 ngày.
        /// Trong thực tế gọi bởi Hangfire background job lúc 0:00 mỗi ngày.
        /// </summary>
        public async Task RunDailyDeadlineCheckAsync()
        {
            Log.Information("[Notification] Bắt đầu quét hợp đồng sắp hết hạn...");
            int sentCount = 0, skippedCount = 0;
            try
            {
                var expiringContracts = await _contractRepository.GetExpiringContractEmailsAsync(daysThreshold: 15);

                foreach (var contract in expiringContracts)
                {
                    if (string.IsNullOrWhiteSpace(contract.ContactEmail))
                    {
                        Log.Warning("[Notification] Hợp đồng {ContractId} không có email khách hàng, bỏ qua.",
                            contract.ContractId);
                        skippedCount++;
                        continue;
                    }

                    string subject = $"CẢNH BÁO: Hợp đồng ECOVA sắp Hết Hạn - {contract.CompanyName}";
                    string body    = BuildExpiryEmailBody(contract.CompanyName, contract.ValidTo);

                    bool isSuccess = await EmailSmtpHelper.SendEmailAsync(contract.ContactEmail, subject, body);
                    if (isSuccess)
                    {
                        Log.Information("[Notification] Đã gửi email cảnh báo tới {Email} (HĐ: {ContractId})",
                            contract.ContactEmail, contract.ContractId);
                        sentCount++;
                    }
                    else
                    {
                        Log.Warning("[Notification] Gửi email thất bại tới {Email} (HĐ: {ContractId})",
                            contract.ContactEmail, contract.ContractId);
                    }
                }

                Log.Information("[Notification] Hoàn tất: gửi {Sent}, bỏ qua {Skipped}",
                    sentCount, skippedCount);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Notification] Lỗi quét hợp đồng sắp hết hạn");
            }
        }

        private static string BuildExpiryEmailBody(string companyName, DateTime validTo)
        {
            int daysLeft = (int)(validTo - DateTime.Now).TotalDays + 1;
            return $@"<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='UTF-8'><meta name='viewport' content='width=device-width,initial-scale=1'></head>
<body style='margin:0;padding:0;background:#f4f6f4;font-family:Segoe UI,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f6f4;padding:32px 0;'>
    <tr><td align='center'>
      <table width='560' cellpadding='0' cellspacing='0'
             style='background:#fff;border-radius:12px;border:1px solid #dde8d9;'>
        <tr>
          <td style='background:#31572C;border-radius:12px 12px 0 0;padding:24px;text-align:center;'>
            <h1 style='margin:0;color:#ECF39E;font-size:22px;letter-spacing:2px;'>ECOVA</h1>
            <p style='margin:4px 0 0;color:#c8e6a0;font-size:12px;'>Hệ thống quan trắc môi trường</p>
          </td>
        </tr>
        <tr>
          <td style='padding:32px 36px;'>
            <h2 style='margin:0 0 16px;font-size:18px;color:#c62828;'>⚠️ Cảnh báo hợp đồng sắp hết hạn</h2>
            <p style='margin:0 0 12px;font-size:15px;color:#333;'>Kính gửi <strong>{System.Net.WebUtility.HtmlEncode(companyName)}</strong>,</p>
            <p style='margin:0 0 20px;font-size:15px;color:#333;'>
              Hệ thống ECOVA nhận thấy <strong>Hợp đồng Quan trắc Môi trường</strong> của Quý công ty
              sẽ <strong style='color:#c62828;'>hết hạn trong {daysLeft} ngày nữa</strong>
              (ngày <strong>{validTo:dd/MM/yyyy}</strong>).
            </p>
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px;'>
              <tr><td style='background:#fff8e1;border-left:4px solid #f9a825;border-radius:0 6px 6px 0;
                             padding:14px 18px;font-size:14px;color:#555;'>
                Kính đề nghị Quý khách liên hệ <strong>Phòng Kinh Doanh ECOVA</strong>
                để tiến hành thủ tục gia hạn kịp thời.
              </td></tr>
            </table>
            <p style='margin:0;font-size:14px;color:#555;font-style:italic;'>
              Trân trọng,<br/><strong>Phòng CSKH ECOVA</strong>
            </p>
          </td>
        </tr>
        <tr>
          <td style='background:#f8f9f6;border-radius:0 0 12px 12px;padding:16px 36px;
                     border-top:1px solid #eee;text-align:center;'>
            <p style='margin:0;font-size:11px;color:#aaa;'>
              Email này được gửi tự động từ hệ thống ECOVA. Vui lòng không trả lời.
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }

        /// <summary>
        /// Lấy danh sách thông báo cho panel Notification trong UI.
        /// Gồm hợp đồng đã quá hạn và sắp hết hạn (trong 20 ngày).
        /// </summary>
        public async Task<List<NotificationDTO>> GetNotifications()
        {
            try
            {
                var contracts = await _contractRepository.GetContractsForNotificationAsync(daysThreshold: 20);
                var notifications = new List<NotificationDTO>();

                foreach (var c in contracts)
                {
                    int overdueDays = (int)(DateTime.Now - c.ValidTo).TotalDays;
                    string title = overdueDays > 0
                        ? $"Hợp đồng trễ hạn: {c.CompanyName}"
                        : $"Hợp đồng sắp hết hạn: {c.CompanyName}";

                    notifications.Add(new NotificationDTO
                    {
                        Title        = title,
                        Code         = c.ContractId,
                        SignedDate   = c.SignedDate,
                        ExpectedDate = c.ValidTo,
                        OverdueDays  = Math.Max(overdueDays, 0)
                    });
                }

                return notifications;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Notification] Lỗi lấy danh sách thông báo");
                return new List<NotificationDTO>();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // ADMIN BROADCAST EMAIL
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gửi email hàng loạt từ Admin đến danh sách người nhận.
        /// Gửi tuần tự để tránh bị spam filter.
        /// onProgress(current, total) để UI cập nhật progress bar theo thời gian thực.
        /// </summary>
        public async Task<(int Sent, int Failed, List<string> FailedEmails)> SendBroadcastEmailAsync(
            IEnumerable<string> recipientEmails,
            string subject,
            string plainTextBody,
            Action<int, int>? onProgress = null)
        {
            var emails     = recipientEmails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList()
                             ?? new List<string>();
            int sent       = 0, failed = 0;
            var failedList = new List<string>();
            string html    = BuildBroadcastEmailBody(plainTextBody);

            Log.Information("[BroadcastEmail] Bắt đầu gửi tới {Total} người nhận | Chủ đề: {Subject}",
                emails.Count, subject);

            for (int i = 0; i < emails.Count; i++)
            {
                bool ok = await EmailSmtpHelper.SendEmailAsync(emails[i], subject, html);
                if (ok)
                {
                    sent++;
                    Log.Information("[BroadcastEmail] [{Current}/{Total}] ✓ {Email}", i + 1, emails.Count, emails[i]);
                }
                else
                {
                    failed++;
                    failedList.Add(emails[i]);
                    Log.Warning("[BroadcastEmail] [{Current}/{Total}] ✗ Thất bại: {Email}", i + 1, emails.Count, emails[i]);
                }

                onProgress?.Invoke(i + 1, emails.Count);

                // Delay nhỏ giữa các lần gửi để tránh Gmail rate-limit (nếu còn email tiếp theo)
                if (i < emails.Count - 1)
                    await Task.Delay(400);
            }

            Log.Information("[BroadcastEmail] Hoàn tất: ✓ Gửi={S}, ✗ Thất bại={F}", sent, failed);
            return (sent, failed, failedList);
        }

        private static string BuildBroadcastEmailBody(string plainText)
        {
            // Tách từng dòng → encode HTML riêng → nối bằng <br/>
            // Cách này đảm bảo xử lý đúng \r\n, \r, \n bất kể .NET version hay RichTextBox behavior
            var lines = (plainText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                .Select(System.Net.WebUtility.HtmlEncode);
            string escaped = string.Join("<br/>", lines);

            return $@"<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='UTF-8'><meta name='viewport' content='width=device-width,initial-scale=1'></head>
<body style='margin:0;padding:0;background:#f4f6f4;font-family:Segoe UI,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f6f4;padding:32px 0;'>
    <tr><td align='center'>
      <table width='560' cellpadding='0' cellspacing='0'
             style='background:#fff;border-radius:12px;border:1px solid #dde8d9;'>
        <tr>
          <td style='background:#31572C;border-radius:12px 12px 0 0;padding:24px;text-align:center;'>
            <h1 style='margin:0;color:#ECF39E;font-size:22px;letter-spacing:2px;'>ECOVA</h1>
            <p style='margin:4px 0 0;color:#c8e6a0;font-size:12px;'>Hệ thống quan trắc môi trường</p>
          </td>
        </tr>
        <tr>
          <td style='padding:32px 36px;'>
            <p style='margin:0;font-size:15px;color:#333;line-height:1.7;'>{escaped}</p>
          </td>
        </tr>
        <tr>
          <td style='background:#f8f9f6;border-radius:0 0 12px 12px;padding:16px 36px;
                     border-top:1px solid #eee;text-align:center;'>
            <p style='margin:0;font-size:11px;color:#aaa;'>
              Email này được gửi từ hệ thống ECOVA. Vui lòng không trả lời.
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }
    }
}
