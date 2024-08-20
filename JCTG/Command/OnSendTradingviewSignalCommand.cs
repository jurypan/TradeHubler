using System.Web;

namespace JCTG.Command
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

        public static OnSendTradingviewSignalCommand CloseAll(int accountId, long signalId, string instrument, long strategyId, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "CLOSEALL",
                StrategyID = strategyId,
            };
        }

        public static OnSendTradingviewSignalCommand MoveSLtoBE(int accountId, long signalId, string instrument, long strategyId, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "MOVESLTOBE",
                StrategyID = strategyId
            };
        }

        public static OnSendTradingviewSignalCommand Cancel(int accountId, long signalId, string instrument, long strategyId, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "CANCEL",
                StrategyID = strategyId,
            };
        }

        public static OnSendTradingviewSignalCommand Buy(int accountId, long signalId, string instrument, long strategyId, double risk, double riskToRewardRatio, string? stopLossExpression = null, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "BUY",
                StrategyID = strategyId,
                MarketOrder = new OnReceivingMarketOrder()
                {
                    Risk = Convert.ToDecimal(risk),
                    RiskRewardRatio = Convert.ToDecimal(riskToRewardRatio),
                    StopLossExpression = stopLossExpression,
                },
            };
        }

        public static OnSendTradingviewSignalCommand BuyStop(int accountId, long signalId, string instrument, long strategyId, double risk, double riskToRewardRatio, string entryExpression, string? stopLossExpression = null, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "BUYSTOP",
                StrategyID = strategyId,
                PassiveOrder = new OnReceivingPassiveOrder()
                {
                    EntryExpression = entryExpression,
                    Risk = Convert.ToDecimal(risk),
                    RiskRewardRatio = Convert.ToDecimal(riskToRewardRatio),
                    StopLossExpression = stopLossExpression,
                }
            };
        }

        public static OnSendTradingviewSignalCommand BuyLimit(int accountId, long signalId, string instrument, long strategyId, double risk, double riskToRewardRatio, string entryExpression, string? stopLossExpression = null, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "BUYLIMIT",
                StrategyID = strategyId,
                PassiveOrder = new OnReceivingPassiveOrder()
                {
                    EntryExpression = entryExpression,
                    Risk = Convert.ToDecimal(risk),
                    RiskRewardRatio = Convert.ToDecimal(riskToRewardRatio),
                    StopLossExpression = stopLossExpression,
                }
            };
        }

        public static OnSendTradingviewSignalCommand SellStop(int accountId, long signalId, string instrument, long strategyId, double risk, double riskToRewardRatio, string entryExpression, string? stopLossExpression = null, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "SELLSTOP",
                StrategyID = strategyId,
                PassiveOrder = new OnReceivingPassiveOrder()
                {
                    EntryExpression = entryExpression,
                    Risk = Convert.ToDecimal(risk),
                    RiskRewardRatio = Convert.ToDecimal(riskToRewardRatio),
                    StopLossExpression = stopLossExpression,
                }
            };
        }

        public static OnSendTradingviewSignalCommand SellLimit(int accountId, long signalId, string instrument, long strategyId, double risk, double riskToRewardRatio, string entryExpression, string? stopLossExpression = null, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "SELLLIMIT",
                StrategyID = strategyId,
                PassiveOrder = new OnReceivingPassiveOrder()
                {
                    EntryExpression = entryExpression,
                    Risk = Convert.ToDecimal(risk),
                    RiskRewardRatio = Convert.ToDecimal(riskToRewardRatio),
                    StopLossExpression = stopLossExpression,
                }
            };
        }

        public static OnSendTradingviewSignalCommand Sell(int accountId, long signalId, string instrument, long strategyId, double risk, double riskToRewardRatio, string? stopLossExpression = null, List<long>? clientIds = null)
        {
            return new OnSendTradingviewSignalCommand()
            {
                SignalID = signalId,
                AccountID = accountId,
                Instrument = instrument,
                ClientIDs = clientIds,
                OrderType = "SELL",
                StrategyID = strategyId,
                MarketOrder = new OnReceivingMarketOrder()
                {
                    Risk = Convert.ToDecimal(risk),
                    RiskRewardRatio = Convert.ToDecimal(riskToRewardRatio),
                    StopLossExpression = stopLossExpression,
                },
            };
        }

        public string ToQueryString()
        {
            var queryParameters = new List<string>
            {
                $"SignalID={SignalID}",
                $"StrategyID={StrategyID}",
                $"AccountID={AccountID}",
                $"OrderType={HttpUtility.UrlEncode(OrderType)}",
                $"Instrument={HttpUtility.UrlEncode(Instrument)}"
            };

            if (ClientIDs != null && ClientIDs.Count != 0)
            {
                queryParameters.Add($"ClientIDs={string.Join(",", ClientIDs)}");
            }

            if (MarketOrder != null)
            {
                if (MarketOrder.Risk.HasValue)
                    queryParameters.Add($"MarketOrder.Risk={MarketOrder.Risk.Value}");

                if (MarketOrder.RiskRewardRatio.HasValue)
                    queryParameters.Add($"MarketOrder.RiskRewardRatio={MarketOrder.RiskRewardRatio.Value}");

                if (!string.IsNullOrEmpty(MarketOrder.StopLossExpression))
                    queryParameters.Add($"MarketOrder.StopLossExpression={HttpUtility.UrlEncode(MarketOrder.StopLossExpression)}");
            }

            if (PassiveOrder != null)
            {
                if (!string.IsNullOrEmpty(PassiveOrder.EntryExpression))
                    queryParameters.Add($"PassiveOrder.EntryExpression={HttpUtility.UrlEncode(PassiveOrder.EntryExpression)}");

                if (PassiveOrder.Risk.HasValue)
                    queryParameters.Add($"PassiveOrder.Risk={PassiveOrder.Risk.Value}");

                if (PassiveOrder.RiskRewardRatio.HasValue)
                    queryParameters.Add($"PassiveOrder.RiskRewardRatio={PassiveOrder.RiskRewardRatio.Value}");

                if (!string.IsNullOrEmpty(PassiveOrder.StopLossExpression))
                    queryParameters.Add($"PassiveOrder.StopLossExpression={HttpUtility.UrlEncode(PassiveOrder.StopLossExpression)}");
            }

            return string.Join(",", queryParameters);
        }
    }

    public class OnReceivingMarketOrder // BUY or SELL
    {
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
        public string? StopLossExpression { get; set; }
    }

    public class OnReceivingPassiveOrder // BUYSTOP or SELLSTOP
    {
        public string? EntryExpression { get; set; }
        public string? StopLossExpression { get; set; }
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }
    }

   
}
