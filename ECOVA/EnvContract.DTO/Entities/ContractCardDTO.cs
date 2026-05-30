using System;

namespace EnvContract.DTO.Entities
{
    /// <summary>
    /// DTO hiển thị card hợp đồng trong Phòng Kinh Doanh.
    /// JOIN Contracts + Customers + CustomerFeedbacks để lấy đầy đủ thông tin.
    /// </summary>
    public class ContractCardDTO
    {
        public string ContractId { get; set; }
        public string CompanyName { get; set; }
        public string Representative { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime SignedDate { get; set; }
        public DateTime ValidTo { get; set; }
        public string Address { get; set; }
        public int Status { get; set; }
        public string CustomerId { get; set; }
        public int RenewalScore { get; set; }            // AI score: 0-100%
        public decimal TotalContractValue { get; set; }  // Giá trị hợp đồng (VNĐ)
        public string IndustryType { get; set; }         // Ngành nghề (Manufacturing, Textile...)

        // ── Dữ liệu thực từ CustomerFeedbacks (dùng cho AI prediction) ──────────
        /// <summary>Thời gian phản hồi trung bình (giờ). ISNULL → 72 nếu chưa có feedback.</summary>
        public float ResponseTime { get; set; }
        /// <summary>Số lần vi phạm trước đó. ISNULL → 0 nếu chưa có feedback.</summary>
        public float PreviousViolations { get; set; }
    }
}
