namespace JCTG.Models
{
    public class OnLogEvent
    {
        public long ClientID { get; set; }
        public required Log Log { get; set; }
    }
}
