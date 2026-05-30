using EnvContract.DTO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    public interface INotificationService
    {
        Task RunDailyDeadlineCheckAsync();
        Task<List<NotificationDTO>> GetNotifications();

        /// <summary>
        /// Gửi email hàng loạt từ Admin đến danh sách người nhận chỉ định.
        /// onProgress(current, total) — callback cập nhật UI progress bar.
        /// Trả về (Sent, Failed, FailedEmails).
        /// </summary>
        Task<(int Sent, int Failed, List<string> FailedEmails)> SendBroadcastEmailAsync(
            IEnumerable<string> recipientEmails,
            string subject,
            string plainTextBody,
            Action<int, int>? onProgress = null);
    }
}
