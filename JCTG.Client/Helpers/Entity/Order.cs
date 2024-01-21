using Newtonsoft.Json;

namespace JCTG.Client
{
    public class Order
    {
        public string? Symbol { get; set; }

        public decimal Lots { get; set; }

        public string? Type { get; set; }

        [JsonProperty("open_price")]
        public decimal OpenPrice { get; set; }


        [JsonProperty("open_time")]
        public DateTime OpenTime { get; set; }


        [JsonProperty("sl")]
        public decimal StopLoss { get; set; }

        [JsonProperty("tp")]
        public decimal TakeProfit { get; set; }

        public double Pnl { get; set; }

        public double Commission { get; set; }

        public double Swap { get; set; }

        public string? Comment { get; set; }

        public int Magic { get; set; }
    }
}
