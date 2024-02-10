using JCTG.Models;

namespace JCTG.Events
{
    public class OnItsTimeToCloseTheOrderEvent
    {
        public long ClientID { get; set; }
        public long SignalID { get; set; }
        public required Log Log { get; set; }
    }
}
