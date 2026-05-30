namespace EnvContract.DTO.Responses
{
    public class AiPredictionResponse
    {
        public string CustomerID { get; set; }
        public string CompanyName { get; set; }
        public double RenewalProbabilityScore { get; set; }
        public bool IsPollutionWarning { get; set; }
        public string PredictionDetails { get; set; }
    }
}