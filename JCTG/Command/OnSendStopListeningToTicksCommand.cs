namespace JCTG.Command
{
    public class OnSendStopListeningToTicksCommand
    {
        public long ClientID { get; set; }
        public int AccountID { get; set; }
        public required string Symbol { get; set; }
    }
}
