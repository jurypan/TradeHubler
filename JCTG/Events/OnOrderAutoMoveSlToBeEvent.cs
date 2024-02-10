using JCTG.Models;

namespace JCTG.Events
{
    public class OnOrderAutoMoveSlToBeEvent
    {
        public long ClientID { get; set; }
        public long SignalID { get; set; }
        public required Log Log { get; set; }
    }
}
