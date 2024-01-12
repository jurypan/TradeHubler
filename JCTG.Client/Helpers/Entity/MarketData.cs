using Newtonsoft.Json;

namespace JCTG.Client
{
    public class MarketData
    {
        public double Ask { get; set; }
        public double Bid { get; set; }
        [JsonProperty("tick_value")]
        public double TickValue { get; set; }
        [JsonProperty("min_lot_size")]
        public double MinLotSize { get; set; }
        [JsonProperty("max_lot_size")]
        public double MaxLotSize { get; set; }
        [JsonProperty("volume_step")]
        public double LotStep { get; set; }
        [JsonProperty("tick_size")]
        public double TickSize { get; set; }
        [JsonProperty("digits")]
        public int Digits { get; set; }

        [JsonProperty("atr_M5")]
        public double ATR5M { get; set; }
        [JsonProperty("atr_M15")]
        public double ATR15M { get; set; }
        [JsonProperty("atr_H1")]
        public double ATR1H { get; set; }
        [JsonProperty("atr_D")]
        public double ATRD { get; set; }
        [JsonProperty("magic")]
        public int Magic { get; set; }
    }
}
