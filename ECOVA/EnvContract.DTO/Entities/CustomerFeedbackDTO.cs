using System;

namespace EnvContract.DTO.Entities
{
    public class CustomerFeedbackDTO
    {
        public string FeedbackID { get; set; }
        public string CustomerID { get; set; }
        public int? ResponseSpeed { get; set; }
        public int? Frequency { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
