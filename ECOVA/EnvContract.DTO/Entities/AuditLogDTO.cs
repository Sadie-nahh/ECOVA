using System;

namespace EnvContract.DTO.Entities
{
    public class AuditLogDTO
    {
        public string HistoryID { get; set; }
        public string ResultID { get; set; }
        public double? OldValue { get; set; }
        public double? NewValue { get; set; }
        public string ChangedBy { get; set; }
        public DateTime? ChangedAt { get; set; }
    }
}