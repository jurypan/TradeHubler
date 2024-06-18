using JCTG.Models;

namespace JCTG.Events
{
    public class OnMetatraderMarketAbstentionEvent
    {
        public long ClientID { get; set; }
        public string Symbol { get; set; }
        public string OrderType { get; set; }
        public MarketAbstentionType Type { get; set; }
        public long Magic { get; set; }
        public required Log Log { get; set; }
    }
}
