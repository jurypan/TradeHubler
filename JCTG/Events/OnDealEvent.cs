using JCTG.Models;

namespace JCTG.Events
{
    public class OnDealEvent
    {
        public long ClientID { get; set; }
        public long DealID { get; set; }
        public long? SignalID { get; set; }
        public required Deal Deal { get; set; }
        public required Log Log { get; set; }
    }
}
