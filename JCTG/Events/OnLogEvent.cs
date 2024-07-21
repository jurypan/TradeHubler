using JCTG.Models;

namespace JCTG.Events
{
    public class OnLogEvent
    {
        public long ClientID { get; set; }
        public long? Magic { get; set; }
        public required Log Log { get; set; }
    }
}
