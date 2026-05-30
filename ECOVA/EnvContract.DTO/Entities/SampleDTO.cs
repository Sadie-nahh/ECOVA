using System;

namespace EnvContract.DTO.Entities
{
    public class SampleDTO
    {
        public string SampleID { get; set; }
        public string OrderID { get; set; }
        public string RegulationID { get; set; }
        public string Barcode { get; set; }
        public string SamplingLocation { get; set; }
        public DateTime? SamplingTime { get; set; }
        public double? FieldTemperature { get; set; }
        public double? FieldHumidity { get; set; }
        public string WeatherCondition { get; set; }
        public string FieldImage { get; set; }
        public bool IsWarning { get; set; }
        public string SamplerID { get; set; }
        public int? Status { get; set; }
    }
}
