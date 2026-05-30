using System;

namespace EnvContract.DTO.Entities
{
    public class OrderDTO
    {
        public string OrderID { get; set; }
        public string ContractID { get; set; }
        public string OrderName { get; set; }
        public DateTime? OrderDate { get; set; }
        public DateTime? Deadline { get; set; }
        public string FinalReportPath { get; set; }
        public int? IsApproved { get; set; }
        public int? Status { get; set; }
        public string CreatedBy { get; set; }
        public string EnvironmentType { get; set; }  // N'Không khí', N'Nước thải', N'Đất'
    }
}
