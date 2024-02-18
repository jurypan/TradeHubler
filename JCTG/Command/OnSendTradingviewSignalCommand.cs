using JCTG.Models;

namespace JCTG.Command
{
    public class OnSendTradingviewSignalCommand
    {

        // Required
        public long SignalID { get; set; }
        public StrategyType StrategyType { get; set; }
        public int AccountID { get; set; }
        public required string OrderType { get; set; }
        public required string Instrument { get; set; }
        public long Magic { get; set; }

        public OnReceivingTradingviewSignalEventMarketOrder? MarketOrder { get; set; }

        public OnReceivingTradingviewSignalEventPassiveOrder? PassiveOrder { get; set; }



    }

    public class OnReceivingTradingviewSignalEventMarketOrder // BUY or SELL
    {
        public decimal? Price { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
    }

    public class OnReceivingTradingviewSignalEventPassiveOrder // BUYSTOP or SELLSTOP
    {
        public string? EntryExpression { get; set; }
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
    }
}
