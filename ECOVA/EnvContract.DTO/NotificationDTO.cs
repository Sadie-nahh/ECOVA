using System;

namespace EnvContract.DTO
{
    public class NotificationDTO
    {
        public string Title        { get; set; } = string.Empty;
        public string Code         { get; set; } = string.Empty;
        public DateTime SignedDate { get; set; }
        public DateTime ExpectedDate { get; set; }
        public int OverdueDays     { get; set; }
    }
}
