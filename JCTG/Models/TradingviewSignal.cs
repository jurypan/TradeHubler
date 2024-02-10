namespace JCTG.Models
{
    public class TradingviewSignal
    {

        // Required
        public long SignalID { get; set; }
        public StrategyType StrategyType { get; set; }
        public int AccountID { get; set; }
        public required string OrderType { get; set; }
        public required string Instrument { get; set; }
        public long Magic { get; set; }

        public TradingviewSignalMarketOrder? MarketOrder { get; set; }

        public TradingviewSignalPassiveOrder? PassiveOrder { get; set; }



    }

    public class TradingviewSignalMarketOrder // BUY or SELL
    {
        public decimal? Price { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
    }

    public class TradingviewSignalPassiveOrder // BUYSTOP or SELLSTOP
    {
        public string? EntryExpression { get; set; }
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
    }
}
