namespace JCTG.Models
{
    public class OnOrderCreateEvent
    {
        public long ClientID { get; set; }
        public long SignalID { get; set; }
        public required Order Order { get; set; }
        public required Log Log { get; set; }
    }
}
