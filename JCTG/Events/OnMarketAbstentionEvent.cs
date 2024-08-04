using JCTG.Models;

namespace JCTG.Events
{
    public class OnMarketAbstentionEvent
    {
        public long ClientID { get; set; }
        public long SignalID { get; set; }
        public required string Symbol { get; set; }
        public required string OrderType { get; set; }
        public MarketAbstentionType Type { get; set; }
        public required Log Log { get; set; }
    }
}
