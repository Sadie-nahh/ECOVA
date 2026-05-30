using System;

namespace EnvContract.DTO.Entities
{
    public class RegulationLimitDTO
    {
        public string LimitID { get; set; }
        public string RegulationID { get; set; }
        public string ParamID { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
    }
}
