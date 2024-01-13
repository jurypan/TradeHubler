namespace JCTG.Entity
{
    public class MetatraderMessage
    {
        public long SignalID { get; set; }
        public StrategyType StrategyType { get; set; }
        public int AccountID { get; set; }
        public string OrderType { get; set; }
        public string Instrument { get; set; }
        public double Price { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public int Magic { get; set; }
        public double ATR5M { get; set; }
        public double ATR15M { get; set; }
        public double ATR1H { get; set; }
        public double ATRD { get; set; }
        public long? TicketId { get; set; }
    }
}
