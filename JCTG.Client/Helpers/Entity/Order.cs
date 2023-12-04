using Newtonsoft.Json;

namespace JCTG.Client
{
    public class Order
    {
        public string? Symbol { get; set; }
        public double Lots { get; set; }
        public string? Type { get; set; }
        [JsonProperty("open_price")]
        public double OpenPrice { get; set; }
        [JsonProperty("open_time")]
        public DateTime OpenTime { get; set; }
        [JsonProperty("sl")]
        public double StopLoss { get; set; }
        [JsonProperty("tp")]
        public double TakeProfit { get; set; }
        public double Pnl { get; set; }
        public double Swap { get; set; }
        public string Comment { get; set; }
        public int Magic { get; set; }
    }
}
