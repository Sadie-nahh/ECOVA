using System;

namespace EnvContract.DTO.Entities
{
    public class TestParameterDTO
    {
        public string ParamID { get; set; }
        public string ParamName { get; set; }
        public string Unit { get; set; }
        public string TestMethod { get; set; }
        public int IsField { get; set; }
        public decimal? Price { get; set; }
    }
}
