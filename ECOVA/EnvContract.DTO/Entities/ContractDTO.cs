using System;

namespace EnvContract.DTO.Entities
{
    public class ContractDto
    {
        public string ContractId { get; set; }
        public string CustomerId { get; set; }
        public DateTime SignedDate { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public string ContractFilePath { get; set; }
        public int Status { get; set; } 
        public string CreatedBy { get; set; }
        public decimal? TotalContractValue { get; set; }
        public string IndustryType { get; set; }
        public int? RenewalLabel { get; set; }
    }
}