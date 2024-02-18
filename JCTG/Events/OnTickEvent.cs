using JCTG.Models;

namespace JCTG.Events
{
    public class OnTickEvent
    {
        public int AccountID { get; set; }
        public long ClientID { get; set; }
        public required MarketData MarketData { get; set; }
    }
}
