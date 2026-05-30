namespace EnvContract.DTO.Responses
{
    public class ContractDashboardResponse
    {
        public int TotalContracts { get; set; }
        public int OnTimeCount { get; set; }
        public int LateCount { get; set; }
        public double OnTimeRate { get; set; } 
    }
}