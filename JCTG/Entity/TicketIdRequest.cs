namespace JCTG
{
    public class TicketIdRequest
    {
        public TicketIdRequest()
        {

        }

        public int AccountID { get; set; }
        public long ClientID { get; set; }
        public int Magic { get; set; }
        public string Symbol { get; set; }
        public string Type { get; set; }
        public DateTime OpenTime { get; set; }
        public double OpenPrice { get; set; }
        public StrategyType StrategyType { get; set; }
    }
}
