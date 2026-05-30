namespace EnvContract.DTO.Entities
{
    public class TestResultDTO
    {
        public string ResultID { get; set; }      // PK. Mã kết quả
        public string SampleID { get; set; }      // FK. Mã mẫu quan trắc
        public string ParamID { get; set; }       // FK. Mã thông số (pH, BOD...)
        public double ResultValue { get; set; }   // Giá trị đo được (NOT NULL)
        public bool IsWarning { get; set; }       // Cờ cảnh báo ĐỎ (Default: false)
        public string TesterID { get; set; }      // FK. Nhân viên Phòng Lab nhập kết quả
        public DateTime EnteredAt { get; set; }   // Thời gian nhập kết quả
    }
}