namespace JCTG
{
    public class MetatraderResponse
    {
        public MetatraderResponse() 
        {
            Action = "NONE";
            TickerInMetatrader = string.Empty;
            TickerInTradingview = string.Empty;
        }
        public int AccountId { get; set; }
        public long ClientId { get; set; }
        public required string Action { get; set; }
        public required string TickerInMetatrader { get; set; }
        public required string TickerInTradingview { get; set; }
        public double TakeProfit { get; set; }
        public double StopLoss { get; set; }
        public StrategyType StrategyType { get; set; }
        public int Magic { get; set; }

    }
}
