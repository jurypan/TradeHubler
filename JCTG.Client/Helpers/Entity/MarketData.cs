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
        [JsonProperty("point_size")]
        public double PointSize { get; set; }

        [JsonProperty("atr_M5_100")]
        public double ATR5M { get; set; }
        [JsonProperty("atr_M15_50")]
        public double ATR15M { get; set; }
        [JsonProperty("atr_H1_25")]
        public double ATR1H { get; set; }
        [JsonProperty("atr_D_14")]
        public double ATRD { get; set; }
    }
}
