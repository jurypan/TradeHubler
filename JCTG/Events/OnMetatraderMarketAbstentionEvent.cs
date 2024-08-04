using JCTG.Models;

namespace JCTG.Events
{
    public class OnMetatraderMarketAbstentionEvent
    {
        public long ClientID { get; set; }
        public string Symbol { get; set; }
        public string OrderType { get; set; }
        public MarketAbstentionType Type { get; set; }
        public long SignalID { get; set; }
        public required Log Log { get; set; }
    }
}
