namespace EnvContract.Common.Constants
{
    /// <summary>
    /// Trạng thái hợp đồng (Contracts.Status)
    /// </summary>
    public enum ContractStatus
    {
        Active    = 0,   // Đang hiệu lực
        Pending   = 1,   // Đang xử lý
        Expiring  = 2,   // Sắp hết hạn
        Expired   = 3,   // Đã hết hạn
        Cancelled = 4    // Đã hủy
    }

    /// <summary>
    /// Trạng thái đợt quan trắc (Orders.Status)
    /// </summary>
    public enum OrderStatus
    {
        New        = 0,   // Mới tạo / Chưa lập kế hoạch
        Planned    = 1,   // Đã lập kế hoạch
        Completed  = 2    // Hoàn thành
    }

    /// <summary>
    /// Trạng thái mẫu (Samples.Status)
    /// </summary>
    public enum SampleStatus
    {
        New        = 0,   // Mới lấy / Chờ lấy mẫu
        Analyzing  = 1,   // Đang phân tích
        Cancelled  = 2,   // Đã hủy
        Completed  = 3    // Hoàn thành
    }

    /// <summary>
    /// Trạng thái duyệt (Orders.IsApproved)
    /// </summary>
    public enum ApprovalStatus
    {
        Pending  = 0,   // Chờ duyệt
        Approved = 1    // Đã duyệt
    }

    /// <summary>
    /// Trạng thái gửi email (EmailQueue.Status)
    /// </summary>
    public enum EmailStatus
    {
        Pending = 0,
        Sent    = 1,
        Failed  = 2
    }

    /// <summary>
    /// Constants dùng cho AI/ML prediction trong AiIntegrationService.
    /// Tập trung tại đây để tránh magic numbers rải rác và dễ điều chỉnh.
    /// </summary>
    public static class AiConstants
    {
        /// <summary>Chia totalContractValue (VND) về đơn vị triệu đồng trước khi đưa vào model.</summary>
        public const float ContractValueDivisor     = 1_000_000f;

        /// <summary>Giá trị hợp đồng tối đa (triệu đồng) được chuẩn hóa vào model (cap).</summary>
        public const float ContractValueCap         = 200f;

        /// <summary>
        /// Hệ số nhân ResponseTime: DB seed lưu đơn vị giờ (1-7h), CSV training phân phối 12-300h.
        /// Nhân 15 để mapping DB values vào phân phối training phù hợp.
        /// </summary>
        public const float ResponseTimeMultiplier   = 15f;

        /// <summary>ResponseTime mặc định (giờ) khi không có dữ liệu phản hồi thực tế.</summary>
        public const float DefaultResponseTimeHours = 72f;

        /// <summary>Ngưỡng số vi phạm để kích hoạt IsPollutionWarning = true.</summary>
        public const float ViolationWarningThreshold = 3f;
    }
}
