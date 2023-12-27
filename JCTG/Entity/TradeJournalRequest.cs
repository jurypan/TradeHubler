namespace JCTG
{
    public class TradeJournalRequest
    {
        public TradeJournalRequest()
        {

        }

        public int AccountID { get; set; }
        public long ClientID { get; set; }

        public int Magic { get; set; }

        public long TicketId { get; set; }

        public string Symbol { get; set; }

        public double Lots { get; set; }

        public string Type { get; set; }

        public DateTime OpenTime { get; set; }

        public double OpenPrice { get; set; }
        public double CurrentPrice { get; set; }
        public double Spread { get; set; }

        public double SL { get; set; }

        public double TP { get; set; }

        public double Pnl { get; set; }

        public double Commission { get; set; }

        public double Swap { get; set; }

        public StrategyType StrategyType { get; set; }

        public string Timeframe { get; set; }

        public string? Comment { get; set; }

        public double Risk { get; set; }
    }
}
