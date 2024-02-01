using Newtonsoft.Json;

namespace JCTG.Client
{
    public class AccountInfo
    {
        public string Name { get; set; }
        public int Number { get; set; }
        public string Currency { get; set; }
        public int Leverage { get; set; }
        [JsonProperty("free_margin")]
        public double FreeMargin { get; set; }
        public double Balance { get; set; }
        public double Equity { get; set; }
        [JsonProperty("tmz")]
        public double TimezoneOffset { get; set; }
    }
}
