namespace JCTG.Entity
{
    public class MetatraderMessage
    {
        public long SignalID { get; set; }
        public StrategyType StrategyType { get; set; }
        public int AccountID { get; set; }
        public string OrderType { get; set; }
        public string Instrument { get; set; }
        public decimal Price { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public long Magic { get; set; }
    }
}
