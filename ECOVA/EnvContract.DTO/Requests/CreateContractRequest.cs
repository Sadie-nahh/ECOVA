using EnvContract.DTO.Entities;

namespace EnvContract.DTO.Requests
{
    public class CreateContractRequest
    {
        public ContractDto Contract { get; set; }
        public string SourcePdfPath { get; set; } 
    }
}