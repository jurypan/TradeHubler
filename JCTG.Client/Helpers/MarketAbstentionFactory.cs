using JCTG.Command;
using JCTG.Events;
using JCTG.Models;

namespace JCTG.Client
{
    public class MarketAbstentionFactory
    {
        public static async Task RiskShouldBeAtLeastXTimesTheSpreadAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic, int riskMinXTimesTheSpreadSetting, decimal price, decimal stoploss)
        {
            var logItem = new Dictionary<string, string>
                {
                    { "magic", magic.ToString() },
                    { "riskMinXTimesTheSpreadSetting", riskMinXTimesTheSpreadSetting.ToString() },
                    { "price", price.ToString() },
                    { "stoploss", stoploss.ToString() },
                    { "risk",  Math.Abs(price - stoploss).ToString() },
                };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("RiskShouldBeAtLeastXTimesTheSpread", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.RiskShouldBeAtLeastXTimesTheSpread);
        }

        public static async Task AmountOfLotSizeShouldBeSmallerThenMaxLotsizeAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic, decimal maxLotSizeSetting, decimal lotSize)
        {
            var logItem = new Dictionary<string, string>
                {
                    { "magic", magic.ToString() },
                    { "maxLotSizeSetting", maxLotSizeSetting.ToString() },
                    { "lotSize", lotSize.ToString() },
                };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("AmountOfLotSizeShouldBeSmallerThenMaxLotsize", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.AmountOfLotSizeShouldBeSmallerThenMaxLotsize);
        }

        public static async Task ExceptionCalculatingEntryPriceAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic, string entryExpression, DateTime? dateFromBar)
        {
            var logItem = new Dictionary<string, string>
                {
                    { "magic", magic.ToString() },
                    { "entryExpression", entryExpression },
                    { "dateFromBar", dateFromBar.HasValue ? dateFromBar.Value.ToString() : "?" },
                };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("ExceptionCalculatingEntryPrice", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingEntryPrice);
        }

        public static async Task ExceptionCalculatingTakeProfitPriceAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic, decimal takeprofit)
        {
            var logItem = new Dictionary<string, string>
                {
                    { "magic", magic.ToString() },
                    { "takeprofit", takeprofit.ToString() },
                };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("ExceptionCalculatingTakeProfitPrice", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingTakeProfitPrice);
        }

        public static async Task ExceptionCalculatingStopLossPriceAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic, decimal stoploss)
        {
            var logItem = new Dictionary<string, string>
                {
                    { "magic", magic.ToString() },
                    { "stoploss", stoploss.ToString() },
                };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("ExceptionCalculatingStopLossPrice", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingStopLossPrice);
        }

        public static async Task ExceptionCalculatingLotSizeAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic, decimal lotsize)
        {
            var logItem = new Dictionary<string, string>
                {
                    { "magic", magic.ToString() },
                    { "lotsize", lotsize.ToString() },
                };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("ExceptionCalculatingLotSize", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingLotSize);
        }

        public static async Task ExceptionCalculatingLotSizeAsync(long clientId, bool isDebug, OnSendManualOrderCommand cmd, long magic, decimal lotsize)
        {
            if(cmd.ClientInstruments.Count > 0)
            {
                var logItem = new Dictionary<string, string>
                {
                    { "magic", magic.ToString() },
                    { "lotsize", lotsize.ToString() },
                };

                await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("ExceptionCalculatingLotSize", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.ClientInstruments.First().Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingLotSize);
            }
        }

        public static async Task MarketWillBeClosedWithinXMinutesAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic, TimeSpan? closeAllTradesAtSetting, int? doNotOpenTradeXMinutesBeforeCloseSetting)
        {
            // Check if both parameters have values
            if (closeAllTradesAtSetting.HasValue && doNotOpenTradeXMinutesBeforeCloseSetting.HasValue)
            {
                var logItem = new Dictionary<string, string>
                {
                    { "magic", magic.ToString() },
                    { "closeAllTradesAtSetting", closeAllTradesAtSetting.Value.ToString() },
                    { "doNotOpenTradeXMinutesBeforeCloseSetting", doNotOpenTradeXMinutesBeforeCloseSetting.Value.ToString() },
                    { "timeOfDay", DateTime.UtcNow.TimeOfDay.ToString() },
                };

                await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("MarketWillBeClosedWithinXMinutes", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.MarketWillBeClosedWithinXMinutes);
            }
        }


        public static async Task CorrelatedPairFoundAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic, string tickerToOpen, string orderTypeToOpen, List<string> correlatedPairs, Dictionary<long, Order> openOrders)
        {
            var logItem = new Dictionary<string, string>
            {
                { "magic", magic.ToString() },
                { "tickerToOpen", tickerToOpen.ToString() },
                { "orderTypeToOpen", orderTypeToOpen.ToString() },
                { "correlatedPairs", string.Join("&", correlatedPairs) },
                { "openOrders",  string.Join("&", openOrders.Select(kvp => $"{kvp.Key}:{kvp.Value.ToString()}")) },
            };
            
            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("CorrelatedPairFound", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.CorrelatedPairFound);
        }

        public static async Task SpreadIsTooHighAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic, decimal maxSpreadSetting, decimal spread)
        {
            var logItem = new Dictionary<string, string>
            {
                { "magic", magic.ToString() },
                { "maxSpreadSetting", maxSpreadSetting.ToString() },
                { "spread", spread.ToString() },
            };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("SpreadIsTooHigh", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.SpreadIsTooHigh);
        }

        public static async Task NoMarketDataAvailableAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic)
        {
            var logItem = new Dictionary<string, string>
            {
                { "magic", magic.ToString() },
            };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("NoMarketDataAvailable", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.NoMarketDataAvailable);
        }

        public static async Task NoSubscriptionForThisPairAndStrategyAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic)
        {
            var logItem = new Dictionary<string, string>
            {
                { "magic", magic.ToString() },
            };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("NoSubscriptionForThisPairAndStrategy", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.NoSubscriptionForThisPairAndStrategy);
        }

        public static async Task EventIsOlderThen1HourAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic)
        {
            var logItem = new Dictionary<string, string>
            {
                { "magic", magic.ToString() },
            };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("EventIsOlderThen1Hour", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.NoAccountInfoAvailable);
        }

        public static async Task NoAccountInfoAvailableAsync(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic)
        {
            var logItem = new Dictionary<string, string>
            {
                { "magic", magic.ToString() },
            };

            await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("NoAccountInfoAvailable", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.Instrument, cmd.OrderType, MarketAbstentionType.NoAccountInfoAvailable);
        }

        public static async Task NoAccountInfoAvailableAsync(long clientId, bool isDebug, OnSendManualOrderCommand cmd, long magic)
        {
            if (cmd.ClientInstruments.Count > 0)
            {
                var logItem = new Dictionary<string, string>
                {
                    { "magic", magic.ToString() },
                };

                await LogAsync(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = cmd.ToQueryString(), Description = GetDescription("NoAccountInfoAvailable", logItem), Magic = Convert.ToInt32(magic) }, magic, cmd.ClientInstruments.First().Instrument, cmd.OrderType, MarketAbstentionType.NoAccountInfoAvailable);
            }
        }



        private static string GetDescription(string name, Dictionary<string, string> logMessages)
        {
            return string.Format($"{name} || {string.Join(",", logMessages.Select(kvp => $"{kvp.Key}={kvp.Value}"))}"); ;
        }

        private static async Task LogAsync(long clientId, Log log, long signalId, string symbol, string orderType, MarketAbstentionType type)
        {
            await HttpCall.OnMarketAbstentionEvent(new OnMarketAbstentionEvent()
            {
                ClientID = clientId,
                SignalID = signalId,
                Symbol = symbol,
                OrderType = orderType,
                Type = type,
                Log = log
            });
        }
    }
}
