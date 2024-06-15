using JCTG.Models;
using System.Reflection;
using System.Text;

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

        public OnReceivingTradingviewSignalEventMarketOrder? MarketOrder { get; set; }

        public OnReceivingTradingviewSignalEventPassiveOrder? PassiveOrder { get; set; }

    }

    public class OnReceivingTradingviewSignalEventMarketOrder // BUY or SELL
    {
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
    }

    public class OnReceivingTradingviewSignalEventPassiveOrder // BUYSTOP or SELLSTOP
    {
        public string? EntryExpression { get; set; }
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
    }
}
