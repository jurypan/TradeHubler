﻿namespace JCTG.Command
{
    public class OnSendTradingviewSignalCommand
    {

        // Required
        public long SignalID { get; set; }
        public long StrategyID { get; set; }
        public int AccountID { get; set; }
        public List<long>? ClientIDs { get; set; }
        public required string OrderType { get; set; }
        public required string Instrument { get; set; }

        public OnReceivingMarketOrder? MarketOrder { get; set; }

        public OnReceivingPassiveOrder? PassiveOrder { get; set; }

        public static OnSendTradingviewSignalCommand Close(int accountId, long signalId, string instrument, long strategyId, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "CLOSE",
                StrategyID = strategyId,
            };
        }
    }

    public class OnReceivingMarketOrder // BUY or SELL
    {
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
    }

    public class OnReceivingPassiveOrder // BUYSTOP or SELLSTOP
    {
        public string? EntryExpression { get; set; }
        public string? StopLossExpression { get; set; }
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
    }

   
}
