using JCTG.Models;

namespace JCTG.Events
{
    public class OnTradeEvent
    {
        public long ClientID { get; set; }
        public long TradeID { get; set; }
        public long? SignalID { get; set; }
        public required Trade Trade { get; set; }
        public required Log Log { get; set; }
    }
}
