namespace EnvContract.DTO.Requests
{
    public class UpdateResultRequest
    {
        public string ResultID { get; set; }
        public double NewValue { get; set; }
        public string Reason { get; set; } 
        public string UpdatedBy { get; set; }
    }
}