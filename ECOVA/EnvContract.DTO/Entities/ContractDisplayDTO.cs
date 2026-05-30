namespace EnvContract.DTO.Entities
{
    /// <summary>
    /// DTO hiển thị hợp đồng trong danh sách Phòng Kế Hoạch.
    /// JOIN Contracts + Customers để lấy tên khách hàng.
    /// </summary>
    public class ContractDisplayDTO
    {
        public string ContractId { get; set; }
        public string CustomerName { get; set; }
        /// <summary>
        /// Ngày lấy mẫu gần nhất (lấy từ bảng Orders hoặc Samples).
        /// Nullable nếu chưa có đơn hàng nào.
        /// </summary>
        public DateTime? SampleDate { get; set; }
    }
}
