using JCTG.Models;

namespace JCTG.Events
{
    public class OnAccountInfoChangedEvent
    {
        public long ClientID { get; set; }
        public required AccountInfo AccountInfo { get; set; }
        public required Log Log { get; set; }
    }
}
