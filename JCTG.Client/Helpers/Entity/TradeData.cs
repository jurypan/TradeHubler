using Newtonsoft.Json;

namespace JCTG.Client
{
    public class TradeData
    {
        [JsonProperty("magic")]
        public int Magic { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("lots")]
        public double Lots { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("open_time")]
        public DateTimeOffset OpenTime { get; set; }

        [JsonProperty("close_time")]
        public DateTimeOffset CloseTime { get; set; }

        [JsonProperty("open_price")]
        public double OpenPrice { get; set; }

        [JsonProperty("close_price")]
        public double ClosePrice { get; set; }

        [JsonProperty("SL")]
        public double SL { get; set; }

        [JsonProperty("TP")]
        public double TP { get; set; }

        [JsonProperty("pnl")]
        public double Pnl { get; set; }

        [JsonProperty("commission")]
        public double Commission { get; set; }

        [JsonProperty("swap")]
        public double Swap { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; }
    }
}
