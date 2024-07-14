using JCTG.Models;

namespace JCTG.Command
{
    public class OnSendTradingviewSignalCommand
    {

        // Required
        public long SignalID { get; set; }
        public StrategyType StrategyType { get; set; }
        public int AccountID { get; set; }
        public List<long>? ClientIDs { get; set; }
        public required string OrderType { get; set; }
        public required string Instrument { get; set; }
        public long Magic { get; set; }

        public OnReceivingMarketOrder? MarketOrder { get; set; }

        public OnReceivingPassiveOrder? PassiveOrder { get; set; }

    }

    public class OnReceivingMarketOrder // BUY or SELL
    {
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
    }

    public class OnReceivingPassiveOrder // BUYSTOP or SELLSTOP
    {
        public string? EntryExpression { get; set; }
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
    }
}
