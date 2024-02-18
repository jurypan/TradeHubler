namespace JCTG.Command
{
    public class OnSendGetHistoricalBarDataCommand
    {
        public long ClientID { get; set; }
        public int AccountID { get; set; }
        public DateTime StartDate { get; set; }
        public required string Symbol { get; set; }
        public required string Timeframe { get; set; }
    }
}
