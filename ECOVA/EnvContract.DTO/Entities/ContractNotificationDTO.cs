using System;

namespace EnvContract.DTO.Entities
{
    /// <summary>
    /// DTO dùng cho query thông báo hợp đồng sắp hết hạn / đã quá hạn.
    /// Map từ JOIN Contracts + Customers.
    /// </summary>
    public class ContractNotificationDTO
    {
        public string ContractId    { get; set; } = "";
        public string CompanyName   { get; set; } = "";
        public string ContactEmail  { get; set; } = "";
        public DateTime SignedDate  { get; set; }
        public DateTime ValidTo     { get; set; }
        public int Status           { get; set; }
    }
}
