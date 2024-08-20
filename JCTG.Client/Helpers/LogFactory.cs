using JCTG.Command;
using JCTG.Events;
using JCTG.Models;
using Microsoft.CodeAnalysis;
using System.Data;

namespace JCTG.Client
{
    public class LogFactory
    {
        public static void CalculateEntryBidPrice(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, Dictionary<string, string> logMessages)
        {
            if (isDebug)
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CalculateEntryBidPrice", logMessages), Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
        }

        public static void CalculateStoploss(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, Dictionary<string, string> logMessages)
        {
            if (isDebug)
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CalculateStoploss", logMessages), Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
        }

        public static void CalculateStoploss(long clientId, bool isDebug, Order cmd, Dictionary<string, string> logMessages)
        {
            if (isDebug)
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CalculateStoploss", logMessages), Magic = cmd.Magic }, cmd.Magic);
        }

        public static void CalculateTakeProfit(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, Dictionary<string, string> logMessages)
        {
            if (isDebug)
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CalculateTakeProfit", logMessages), Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
        }

        public static void CalculateTakeProfit(long clientId, bool isDebug, OnSendManualOrderCommand cmd, Dictionary<string, string> logMessages)
        {
            if (isDebug)
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CalculateTakeProfit", logMessages), Magic = cmd.Magic }, cmd.Magic);
        }

        public static void CalculateLotSize(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, Dictionary<string, string> logMessages)
        {
            if (isDebug)
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CalculateTakeProfit", logMessages), Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
        }

        public static void CalculateLotSize(long clientId, bool isDebug, OnSendManualOrderCommand cmd, Dictionary<string, string> logMessages)
        {
            if (isDebug)
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CalculateTakeProfit", logMessages), Magic = cmd.Magic }, cmd.Magic);
        }

        public static void CloseOrderCommand(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long ticketId, double lots = 0, int magic = -1)
        {
            if (isDebug)
            {
                var logItem = new Dictionary<string, string>
                    {
                        { "ticketId", ticketId.ToString() },
                        { "lots", lots.ToString() },
                        { "magic", magic.ToString() },
                    };
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CloseOrderCommand", logItem), Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
            }
        }




        public static void ExecuteOrderCommand(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, string symbol, OrderType orderType, decimal lots, decimal price, decimal stopLoss, decimal takeProfit, int magic = 0, string? comment = null, long expiration = 0)
        {
            if (isDebug)
            {
                var logItem = new Dictionary<string, string>
                    {
                        { "symbol", symbol },
                        { "orderType", orderType.GetDescription() },
                        { "lots", lots.ToString() },
                        { "price", price.ToString() },
                        { "stopLoss", stopLoss.ToString() },
                        { "takeProfit", takeProfit.ToString() },
                        { "magic", magic.ToString() },
                        { "comment", comment == null ? string.Empty : comment.ToString() },
                        { "expiration", expiration.ToString() }
                    };
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("ExecuteOrderCommand", logItem), Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
            }
        }

        public static void ExecuteOrderCommand(long clientId, bool isDebug, OnSendManualOrderCommand cmd, string symbol, OrderType orderType, decimal lots, decimal price, decimal stopLoss, decimal takeProfit, int magic = 0, string? comment = null, long expiration = 0)
        {
            if (isDebug)
            {
                var logItem = new Dictionary<string, string>
                    {
                        { "symbol", symbol },
                        { "orderType", orderType.GetDescription() },
                        { "lots", lots.ToString() },
                        { "price", price.ToString() },
                        { "stopLoss", stopLoss.ToString() },
                        { "takeProfit", takeProfit.ToString() },
                        { "magic", magic.ToString() },
                        { "comment", comment == null ? string.Empty : comment.ToString() },
                        { "expiration", expiration.ToString() }
                    };
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("ExecuteOrderCommand", logItem), Magic = cmd.Magic }, cmd.Magic);
            }
        }

        public static void ModifyOrderCommand(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long ticketId, decimal lots, decimal price, decimal stopLoss, decimal takeProfit, int magic = -1, long expiration = 0)
        {
            if (isDebug)
            {
                var logItem = new Dictionary<string, string>
                    {
                        { "ticketId", ticketId.ToString() },
                        { "lots", lots.ToString() },
                        { "price", price.ToString() },
                        { "stopLoss", stopLoss.ToString() },
                        { "takeProfit", takeProfit.ToString() },
                        { "magic", magic.ToString() },
                        { "expiration", expiration.ToString() }
                    };
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("ModifyOrderCommand", logItem), Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
            }
        }

        public static void UnableToFindOrder(long clientId, bool isDebug, OnSendTradingviewSignalCommand cmd, long magic)
        {
            if (isDebug)
            {
                var logItem = new Dictionary<string, string>
                    {
                        { "magic", magic.ToString() },
                    };
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("UnableToFindOrder", logItem), Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
            }
        }

        public static void UnableToLinkCommandToAccount(long clientId, bool isDebug)
        {
            if (isDebug)
            {
                HttpCallOnLogEvent(clientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = string.Empty, Description = string.Empty, Magic = 0 }, 0);
            }
        }



        public static void CloseOrderByScheduler(long clientId, bool isDebug, Order cmd, long ticketId, double lots, int magic)
        {
            if (isDebug)
            {
                var logItem = new Dictionary<string, string>
                    {
                        { "ticketId", ticketId.ToString() },
                        { "lots", lots.ToString() },
                        { "magic", magic.ToString() },
                    };

                var log = new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CloseOrderByScheduler", logItem), Magic = magic };

                // Send log to files
                Task.Run(async () =>
                {
                    // Send the event to server
                    await HttpCall.OnItsTimeToCloseTheOrderEvent(new OnItsTimeToCloseTheOrderEvent()
                    {
                        ClientID = clientId,
                        SignalID = magic,
                        Log = log
                    });
                });
            }
        }

        public static void ModifyOrderByAutoMoveSLtoBE(long clientId, bool isDebug, Order cmd, long ticketId, decimal lots, decimal price, decimal stopLoss, decimal takeProfit, int magic = -1, long expiration = 0)
        {
            if (isDebug)
            {
                var logItem = new Dictionary<string, string>
                    {
                        { "ticketId", ticketId.ToString() },
                        { "lots", lots.ToString() },
                        { "price", price.ToString() },
                        { "stopLoss", stopLoss.ToString() },
                        { "takeProfit", takeProfit.ToString() },
                        { "magic", magic.ToString() },
                        { "expiration", expiration.ToString() },
                    };

                var log = new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("ModifyOrderByAutoMoveSLtoBE", logItem), Magic = magic };

                // Send log to files
                Task.Run(async () =>
                {
                    // Send the event to server
                    await HttpCall.OnOrderAutoMoveSlToBeEvent(new OnOrderAutoMoveSlToBeEvent()
                    {
                        ClientID = clientId,
                        SignalID = magic,
                        StopLossPrice = stopLoss,
                        Log = log
                    });
                });
            }
        }






        public static void CreatedAnOrderEvent(long clientId, bool isDebug, Order cmd, long ticketId, int magic)
        {
            var logItem = new Dictionary<string, string>
                {
                    { "ticketId", ticketId.ToString() },
                    { "magic", magic.ToString() },
                };

            var log = new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CreatedAnOrderEvent", logItem), Magic = magic };

            // Send log to files
            Task.Run(async () =>
            {
                // Send the event to server
                await HttpCall.OnOrderCreatedEvent(new OnOrderCreatedEvent()
                {
                    ClientID = clientId,
                    SignalID = magic,
                    Order = cmd,
                    Log = log
                });

            });
        }

        public static void UpdatedAnOrderEvent(long clientId, bool isDebug, Order cmd, long ticketId, int magic)
        {

            var logItem = new Dictionary<string, string>
                {
                    { "ticketId", ticketId.ToString() },
                    { "magic", magic.ToString() },
                };

            var log = new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("UpdatedAnOrderEvent", logItem), Magic = magic };

            // Send log to files
            Task.Run(async () =>
            {
                // Send the event to server
                await HttpCall.OnOrderUpdatedEvent(new OnOrderUpdatedEvent()
                {
                    ClientID = clientId,
                    SignalID = magic,
                    Order = cmd,
                    Log = log
                });

            });
        }

        public static void ClosedAnOrderEvent(long clientId, bool isDebug, Order cmd, long ticketId, decimal closePrice, int magic, decimal rewardratio)
        {

            var logItem = new Dictionary<string, string>
                {
                    { "ticketId", ticketId.ToString() },
                    { "magic", magic.ToString() },
                };

            var log = new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("ClosedAnOrderEvent", logItem), Magic = magic };

            // Send log to files
            Task.Run(async () =>
            {
                // Send the event to server
                await HttpCall.OnOrderClosedEvent(new OnOrderClosedEvent()
                {
                    ClientID = clientId,
                    SignalID = magic,
                    ClosePrice = closePrice,
                    Order = cmd,
                    Log = log,
                    RewardRatio = rewardratio
                });

            });
        }

        public static void ErrorExecuteOrderEvent(long clientId, bool isDebug, Log log, string symbol, string orderType, int magic)
        {

            // Send log to files
            Task.Run(async () =>
            {
                // Send the tradejournal to Azure PubSub server
                await HttpCall.OnMarketAbstentionEvent(new OnMetatraderMarketAbstentionEvent()
                {
                    ClientID = clientId,
                    Symbol = symbol,
                    OrderType = orderType,
                    Type = MarketAbstentionType.MetatraderOpenOrderError,
                    SignalID = magic,
                    Log = log
                });

            });
        }

        public static void CreatedADealEvent(long clientId, bool isDebug, Deal cmd, long tradeId, int magic, double? accountBalance, double? accountEquity, decimal askPrice, decimal spread, decimal costSpread)
        {

            var logItem = new Dictionary<string, string>
                {
                    { "tradeId", tradeId.ToString() },
                    { "magic", magic.ToString() },
                };

            var log = new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("CreatedADealEvent", logItem), Magic = magic };

            // Send log to files
            Task.Run(async () =>
            {
                // Send the tradejournal to Azure PubSub server
                await HttpCall.OnDealCreatedEvent(new OnDealCreatedEvent()
                {
                    ClientID = clientId,
                    MtDealID = tradeId,
                    Deal = cmd,
                    Log = log,
                    AccountBalance = accountBalance,
                    AccountEquity = accountEquity,
                    Price = decimal.ToDouble(askPrice),
                    Spread = Convert.ToDouble(spread),
                    SpreadCost = Convert.ToDouble(costSpread),
                });

            });
        }

        public static void AccountInfoChangedEvent(long clientId, bool isDebug, AccountInfo cmd)
        {

            var logItem = new Dictionary<string, string>
            {

            };

            var log = new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = cmd.ToQueryString(), Description = GetDescription("AccountInfoChangedEvent", logItem) };

            // Send log to files
            Task.Run(async () =>
            {
                // Send the event to server
                await HttpCall.OnAccountInfoChangedEvent(new OnAccountInfoChangedEvent()
                {
                    ClientID = clientId,
                    AccountInfo = cmd,
                    Log = log,
                });
            });
        }











        private static string GetDescription(string name, Dictionary<string, string> logMessages)
        {
            return string.Format($"{name} || {string.Join(",", logMessages.Select(kvp => $"{kvp.Key}={kvp.Value}"))}"); ;
        }

        private static void HttpCallOnLogEvent(long clientId, Log log, long magic)
        {
            Task.Run(async () =>
                {
                    await HttpCall.OnLogEvent(new OnLogEvent
                    {
                        ClientID = clientId,
                        Magic = magic,
                        Log = log
                    });
                }
            );
        }
    }
}
