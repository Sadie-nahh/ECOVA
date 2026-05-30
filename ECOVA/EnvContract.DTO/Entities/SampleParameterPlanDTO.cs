namespace EnvContract.DTO.Entities
{
    /// <summary>
    /// DTO cho mỗi hàng thông số trong bảng Khu vực lấy mẫu (Phòng Kế Hoạch).
    /// Chứa thông tin thông số + phòng ban phụ trách + ngưỡng QCVN.
    /// </summary>
    public class SampleParameterPlanDTO
    {
        public string ParamID { get; set; }
        public string RegulationID { get; set; } // Bổ sung để lưu chuẩn chính xác
        public string ParamName { get; set; }
        public string Unit { get; set; }
        /// <summary>
        /// "Hiện trường" hoặc "Thí nghiệm" — suy ra từ TestParameters.IsField
        /// </summary>
        public string Department { get; set; }
        /// <summary>
        /// Chuỗi hiển thị ngưỡng QCVN, ví dụ: "0 - 14" hoặc "≤ 100"
        /// </summary>
        public string QcvnLimit { get; set; }
    }
}
