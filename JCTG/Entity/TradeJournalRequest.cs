namespace JCTG
{
    public class TradeJournalRequest
    {
        public TradeJournalRequest()
        {
            this.Symbol = string.Empty;
            this.Type = string.Empty;
            this.Timeframe = string.Empty;
        }

        public int AccountID { get; set; }
        public long ClientID { get; set; }

        public int Magic { get; set; }

        public long TicketId { get; set; }

        public string Symbol { get; set; }

        public decimal Lots { get; set; }

        public string Type { get; set; }

        public DateTime OpenTime { get; set; }

        public decimal OpenPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal Spread { get; set; }

        public decimal SL { get; set; }

        public decimal TP { get; set; }

        public double Pnl { get; set; }

        public double Commission { get; set; }

        public double Swap { get; set; }

        public StrategyType StrategyType { get; set; }

        public string Timeframe { get; set; }

        public string? Comment { get; set; }

        public decimal Risk { get; set; }
        public bool IsTradeClosed { get; set; }
    }
}
