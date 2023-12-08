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
    }
}
