namespace EnvContract.DTO.Entities
{
    /// <summary>
    /// DTO chứa thông tin chi tiết về thông số và ngưỡng giới hạn theo QCVN.
    /// Dùng cho logic kiểm tra vi phạm ngưỡng (Module 5).
    /// </summary>
    public class QcvnParameterDTO
    {
        // --- Khóa và Liên kết ---
        public string LimitID { get; set; }           // PK. Mã định danh của ngưỡng giới hạn
        public string RegulationID { get; set; }      // FK. Mã quy chuẩn QCVN áp dụng
        public string ParamID { get; set; }            // FK. Mã thông số

        // --- Thông tin chi tiết thông số ---
        public string ParamName { get; set; }         // Tên thông số (VD: pH, BOD5, DO...)
        public string Unit { get; set; }              // Đơn vị tính (VD: mg/L, °C)
        public string EnvironmentType { get; set; }   // Loại môi trường (VD: Nước mặt, Nước thải, Đất)

        // --- Ngưỡng giới hạn (Dùng để kiểm tra logic Báo Đỏ) ---
        public double? MinValue { get; set; }         // Giá trị tối thiểu cho phép (Nullable)
        public double? MaxValue { get; set; }         // Giá trị tối đa cho phép (Dùng để BÁO ĐỎ)
    }
}