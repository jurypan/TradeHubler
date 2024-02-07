﻿using Azure;
using JCTG.Entity;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using Websocket.Client;
using static JCTG.Client.Helpers;

namespace JCTG.Client
{
    public class Metatrader : IDisposable
    {

        private readonly AppConfig? _appConfig;
        private readonly List<MetatraderApi> _apis;
        private readonly List<DailyTaskScheduler> _timing;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // Initial count 1, maximum count 1
        private readonly ConcurrentDictionary<long, List<Log>> _buffers = new ConcurrentDictionary<long, List<Log>>();
        private Timer _timer;

        public Metatrader(AppConfig appConfig)
        {
            // Init APP Config + API
            _appConfig = appConfig;
            _apis = [];
            _timing = new List<DailyTaskScheduler>();
            _timer = new Timer(async _ => await FlushLogsToFile(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            // Foreach broker, init the API
            foreach (var api in _appConfig.Brokers)
            {
                // Init API
                var _api = new MetatraderApi(api.MetaTraderDirPath, api.ClientId, appConfig.SleepDelay, appConfig.MaxRetryCommandSeconds, appConfig.LoadOrdersFromFile);

                // Add to the list
                _apis.Add(_api);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task ListToTheClientsAsync()
        {
            // Check if app config is not null
            if (_appConfig != null)
            {
                // Start the system
                _ = Parallel.ForEach(_apis, async _api =>
                {
                    // Get the broker from the local database
                    var broker = _appConfig?.Brokers.FirstOrDefault(f => f.ClientId == _api.ClientId);

                    // do null reference checks
                    if (_api != null && broker != null && broker.Pairs.Count != 0)
                    {
                        // Load logs into memory
                        await LoadLogFromFileAsync(_api.ClientId);

                        // Init the events
                        _api.OnOrderCreateEvent += OnOrderCreateEvent;
                        _api.OnOrderUpdateEvent += OnOrderUpdateEvent;
                        _api.OnOrderRemoveEvent += OnOrderRemoveEvent;
                        _api.OnLogEvent += OnLogEvent;
                        _api.OnCandleCloseEvent += OnCandleCloseEvent;
                        _api.OnTickEvent += OnTickEvent;

                        // Start the API
                        await _api.StartAsync();

                        // Subscribe foreach pair
                        _api.SubscribeForTicks(broker.Pairs.Select(f => f.TickerInMetatrader).ToList());
                        _api.SubscribeForBarData(broker.Pairs.Select(p => new KeyValuePair<string, string>(p.TickerInMetatrader, p.Timeframe)).ToList());
                        _api.GetHistoricData(broker.Pairs);

                        // Init close trades on a particular time
                        foreach (var pair in broker.Pairs.Where(f => f.CloseAllTradesAt.HasValue))
                        {
                            if (pair.CloseAllTradesAt != null)
                            {
                                var timing = new DailyTaskScheduler(_api.ClientId, pair.TickerInMetatrader, pair.CloseAllTradesAt.Value, pair.StrategyNr);
                                timing.OnTimeEvent += OnItsTimeToCloseTradeEvent;
                                _timing.Add(timing);
                            }
                        }
                    }
                });
            }

            await Task.FromResult(0);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task ListenToTheServerAsync()
        {
            // Do null reference checks
            if (_appConfig != null && _apis != null)
            {
                // Get web socket client
                var client = Program.Service?.GetService<WebsocketClient>();

                // Do null reference check
                if (client != null)
                {
                    // Disable the auto disconnect and reconnect because the sample would like the client to stay online even no data comes in
                    client.ReconnectTimeout = null;

                    // Enable the message receive
                    _ = client.MessageReceived.Subscribe(async msg =>
                    {
                        // Do null reference check
                        if (msg != null && msg.Text != null)
                        {
                            // Add to the log
                            SendLogToFile(0, new Log() { Time = DateTime.UtcNow, Type = "INFO", Message = msg.Text });

                            // Get response
                            var response = JsonConvert.DeserializeObject<MetatraderMessage>(msg.Text);

                            // Do null reference check
                            if (response != null)
                            {
                                // Iterate through the api's
                                foreach (var api in _apis)
                                {
                                    // Get the right pair back from the local database
                                    var pair = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInTradingView.Equals(response.Instrument) && f.StrategyNr == response.StrategyType);

                                    // Start balance
                                    var startbalance = _appConfig.Brokers.First(f => f.ClientId == api.ClientId).StartBalance;

                                    // Get dynamic risk
                                    var dynRisk = new List<Risk>(_appConfig.Brokers
                                                                              .Where(f => f.ClientId == api.ClientId)
                                                                              .SelectMany(f => f.Risk ?? []));

                                    // If this broker is listening to this signal and the account size is greater then zero
                                    if (api.AccountInfo != null)
                                    {
                                        if (api.AccountInfo.Balance > 0 && api.MarketData != null)
                                        {
                                            if (pair != null)
                                            {
                                                // Get the metadata tick
                                                var metadataTick = api.MarketData.FirstOrDefault(f => f.Key == pair.TickerInMetatrader).Value;

                                                if (metadataTick != null && metadataTick.Ask > 0 && metadataTick.Bid > 0 && metadataTick.Digits > 0)
                                                {
                                                    // Calculate spread
                                                    var spread = Math.Round(Math.Abs(metadataTick.Ask - metadataTick.Bid), metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                    // BUY
                                                    if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                                && response.OrderType == "BUY"
                                                                && response.MarketOrder != null
                                                                && response.MarketOrder.Price.HasValue
                                                                && response.MarketOrder.StopLoss.HasValue
                                                                && response.MarketOrder.TakeProfit.HasValue
                                                    )
                                                    {
                                                        // Calculate SL Price
                                                        var slPrice = RiskCalculator.SLForLong(
                                                                mtPrice: metadataTick.Ask,
                                                                mtSpread: spread,
                                                                mtDigits: metadataTick.Digits,
                                                                signalPrice: response.MarketOrder.Price.Value,
                                                                signalSL: response.MarketOrder.StopLoss.Value,
                                                                spreadExecType: pair.SpreadSL,
                                                                pairSlMultiplier: pair.SLMultiplier
                                                                );

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                            var description = string.Format($"SLForLong: Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits},SignalPrice={response.MarketOrder.Price},SignalSL={response.MarketOrder.StopLoss}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                        }

                                                        // Calculate the lot size
                                                        var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, metadataTick.Ask, slPrice, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk);

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                            var description = string.Format($"LotSize: StartBalance={startbalance},Balance={api.AccountInfo.Balance},Risk={pair.Risk},AskPrice={metadataTick.Ask},SlPrice={slPrice},TickValue={metadataTick.TickValue},TickSize={metadataTick.TickSize},LotStep={metadataTick.LotStep},MinLotSize={metadataTick.MinLotSize},MaxLotSize={metadataTick.MaxLotSize},DynRisk={dynRisk}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                        }

                                                        // do 0.0 check
                                                        if (lotSize > 0.0M && slPrice > 0.0M)
                                                        {
                                                            // Do lot size check
                                                            if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                            {
                                                                // Do check if risk is x times the spread
                                                                if (spread * pair.RiskMinXTimesTheSpread < metadataTick.Ask - slPrice)
                                                                {
                                                                    // Calculate TP Price
                                                                    var tpPrice = RiskCalculator.TPForLong(
                                                                            mtPrice: metadataTick.Ask,
                                                                            mtSpread: spread,
                                                                            mtDigits: metadataTick.Digits,
                                                                            signalPrice: response.MarketOrder.Price.Value,
                                                                            signalTP: response.MarketOrder.TakeProfit.Value,
                                                                            spreadExecType: pair.SpreadTP
                                                                            );

                                                                    // Send to logs
                                                                    if (_appConfig.Debug)
                                                                    {
                                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                        var description = string.Format($"TPForLong: Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits},SignalPrice={response.MarketOrder.Price},SignalTP={response.MarketOrder.TakeProfit}");
                                                                        SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                                    }

                                                                    // Print on the screen
                                                                    Print(Environment.NewLine);
                                                                    Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                                    Print("Date        : " + DateTime.UtcNow);
                                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                                    Print("Order       : BUY MARKET ORDER");
                                                                    Print("------------------------------------------------");

                                                                    // Open order
                                                                    var comment = string.Format($"{response.SignalID}/{Math.Round(response.MarketOrder.Price.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(response.MarketOrder.StopLoss.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");
                                                                    var orderType = OrderType.Buy;
                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask < response.MarketOrder.Price.Value)
                                                                        orderType = OrderType.BuyStop;
                                                                    else if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask > response.MarketOrder.Price.Value)
                                                                        orderType = OrderType.BuyLimit;
                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, slPrice, tpPrice, (int)response.Magic, comment);

                                                                    // Send to logs
                                                                    if (_appConfig.Debug)
                                                                    {
                                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                        var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price=,SL={slPrice},TP={tpPrice},Magic={response.Magic},Comment={comment}");
                                                                        SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                                    SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Spread is too high in regards to the risk" });
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Max lot size exceeded" });
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the stop loss price" });
                                                        }
                                                    }

                                                    // BUY STOP
                                                    else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                                && response.OrderType == "BUYSTOP"
                                                                && response.PassiveOrder != null
                                                                && response.PassiveOrder.EntryExpression != null
                                                                && response.PassiveOrder.Risk.HasValue
                                                                && response.PassiveOrder.RiskRewardRatio.HasValue
                                                    )
                                                    {

                                                        // Get the entry price
                                                        var price = await DynamicEvaluator.EvaluateExpressionAsync(response.PassiveOrder.EntryExpression, api.HistoricData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList());

                                                        // Add the spread options
                                                        if (pair.SpreadEntry.HasValue)
                                                        {
                                                            if (pair.SpreadEntry.Value == SpreadExecType.Add)
                                                                price += spread;
                                                            else if (pair.SpreadEntry.Value == SpreadExecType.Subtract)
                                                                price -= spread;
                                                        }

                                                        // Get the Stop Loss price
                                                        var sl = price - (response.PassiveOrder.Risk.Value * Convert.ToDecimal(pair.SLMultiplier));

                                                        // Add the spread options
                                                        if (pair.SpreadSL.HasValue)
                                                        {
                                                            if (pair.SpreadSL.Value == SpreadExecType.Add)
                                                                sl += spread;
                                                            else if (pair.SpreadSL.Value == SpreadExecType.Subtract)
                                                                sl -= spread;
                                                        }

                                                        // Get the Take Profit Price
                                                        var tp = price + (response.PassiveOrder.Risk.Value * response.PassiveOrder.RiskRewardRatio.Value);

                                                        // Add the spread options
                                                        if (pair.SpreadTP.HasValue)
                                                        {
                                                            if (pair.SpreadTP.Value == SpreadExecType.Add)
                                                                tp += spread;
                                                            else if (pair.SpreadTP.Value == SpreadExecType.Subtract)
                                                                tp -= spread;
                                                        }

                                                        // Round
                                                        price = Math.Round(price, metadataTick.Digits, MidpointRounding.AwayFromZero);
                                                        sl = Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero);
                                                        tp = Math.Round(tp, metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                            var description = string.Format($"Price: Price={price},Spread={spread},EntryExpression={response.PassiveOrder.EntryExpression}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });

                                                            description = string.Format($"SL: Price={price},Spread={spread},Risk={response.PassiveOrder.Risk.Value},SLMultiplier={pair.SLMultiplier}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });

                                                            description = string.Format($"TP: Price={price},Spread={spread},Risk={response.PassiveOrder.Risk.Value},RiskRewardRatio={response.PassiveOrder.RiskRewardRatio.Value}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                        }

                                                        // Calculate the lot size
                                                        var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, price, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk); ;

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                            var description = string.Format($"LotSize: StartBalance={startbalance},Balance={api.AccountInfo.Balance},Risk={pair.Risk},AskPrice={metadataTick.Ask},SlPrice={sl},TickValue={metadataTick.TickValue},TickSize={metadataTick.TickSize},LotStep={metadataTick.LotStep},MinLotSize={metadataTick.MinLotSize},MaxLotSize={metadataTick.MaxLotSize},DynRisk={dynRisk}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                        }

                                                        // do 0.0 check
                                                        if (lotSize > 0.0M && price > 0.0M && sl > 0.0M && tp > 0.0M)
                                                        {
                                                            // Do lot size check
                                                            if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                            {
                                                                // Do check if risk is x times the spread
                                                                if (spread * pair.RiskMinXTimesTheSpread < response.PassiveOrder.Risk.Value)
                                                                {
                                                                    // Print on the screen
                                                                    Print(Environment.NewLine);
                                                                    Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                                    Print("Date        : " + DateTime.UtcNow);
                                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                                    if (metadataTick.Ask > price)
                                                                        Print("Order       : BUY LIMIT ORDER");
                                                                    else
                                                                        Print("Order       : BUY STOP ORDER");
                                                                    Print("------------------------------------------------");

                                                                    // Cancel open buy or limit orders
                                                                    if (pair.CancelStopOrLimitOrderWhenNewSignal)
                                                                    {
                                                                        foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol == pair.TickerInMetatrader
                                                                                                                        && f.Value.Type != null
                                                                                                                        && (f.Value.Type.Equals("buystop") || f.Value.Type.Equals("buylimit"))
                                                                                                                        ))
                                                                        {
                                                                            api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                                            // Send to logs
                                                                            if (_appConfig.Debug)
                                                                            {
                                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                                var description = string.Format($"CancelStopOrLimitOrderWhenNewSignal: Symbol={pair.TickerInMetatrader}");
                                                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                                            }
                                                                        }
                                                                    }

                                                                    // Open order
                                                                    var comment = string.Format($"{response.SignalID}/{price}/{sl}/{(int)pair.StrategyNr}/{spread}");
                                                                    var orderType = OrderType.BuyStop;
                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask > price)
                                                                        orderType = OrderType.BuyLimit;
                                                                    else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Ask > price)
                                                                        orderType = OrderType.Buy;
                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, price, sl, tp, (int)response.Magic, comment);

                                                                    // Send to logs
                                                                    if (_appConfig.Debug)
                                                                    {
                                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                        var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price={price},SL={sl},TP={tp},Magic={response.Magic},Comment={comment}");
                                                                        SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                    SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Spread is too high in regards to the risk" });
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Max lot size exceeded" });
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Can't find entry candle {response.PassiveOrder.EntryExpression}" });
                                                        }
                                                    }

                                                    // SELL
                                                    else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                                        && response.OrderType == "SELL"
                                                                        && response.MarketOrder != null
                                                                        && response.MarketOrder.Price.HasValue
                                                                        && response.MarketOrder.StopLoss.HasValue
                                                                        && response.MarketOrder.TakeProfit.HasValue
                                                                        )
                                                    {
                                                        // Calculate SL Price
                                                        var slPrice = RiskCalculator.SLForShort(
                                                                mtPrice: metadataTick.Ask,
                                                                mtSpread: spread,
                                                                mtDigits: metadataTick.Digits,
                                                                signalPrice: response.MarketOrder.Price.Value,
                                                                signalSL: response.MarketOrder.StopLoss.Value,
                                                                spreadExecType: pair.SpreadSL,
                                                                pairSlMultiplier: pair.SLMultiplier
                                                                );

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                            var description = string.Format($"SLForShort: Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits},SignalPrice={response.MarketOrder.Price},SignalSL={response.MarketOrder.StopLoss}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                        }

                                                        // Calculate the lot size
                                                        var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, metadataTick.Ask, slPrice, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk);

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                            var description = string.Format($"LotSize: StartBalance={startbalance},Balance={api.AccountInfo.Balance},Risk={pair.Risk},AskPrice={metadataTick.Ask},SlPrice={slPrice},TickValue={metadataTick.TickValue},TickSize={metadataTick.TickSize},LotStep={metadataTick.LotStep},MinLotSize={metadataTick.MinLotSize},MaxLotSize={metadataTick.MaxLotSize},DynRisk={dynRisk}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                        }

                                                        // do 0.0 check
                                                        if (lotSize > 0.0M && slPrice > 0.0M)
                                                        {
                                                            // Do lot size check
                                                            if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                            {
                                                                // Do check if risk is x times the spread
                                                                if (spread * pair.RiskMinXTimesTheSpread < slPrice - metadataTick.Ask)
                                                                {
                                                                    // Calculate TP Price
                                                                    var tpPrice = RiskCalculator.TPForShort(
                                                                            mtPrice: metadataTick.Ask,
                                                                            mtSpread: spread,
                                                                            mtDigits: metadataTick.Digits,
                                                                            signalPrice: response.MarketOrder.Price.Value,
                                                                            signalTP: response.MarketOrder.TakeProfit.Value,
                                                                            spreadExecType: pair.SpreadTP
                                                                            );

                                                                    // Send to logs
                                                                    if (_appConfig.Debug)
                                                                    {
                                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                                        var description = string.Format($"TPForShort: Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits},SignalPrice={response.MarketOrder.Price},SignalTP={response.MarketOrder.TakeProfit}");
                                                                        SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                                    }

                                                                    // Print on the screen
                                                                    Print(Environment.NewLine);
                                                                    Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                                    Print("Date        : " + DateTime.UtcNow);
                                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                                    Print("Order       : SELL MARKET ORDER");
                                                                    Print("------------------------------------------------");


                                                                    // Open order
                                                                    var comment = string.Format($"{response.SignalID}/{Math.Round(response.MarketOrder.Price.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(response.MarketOrder.StopLoss.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");

                                                                    // Passive / Active
                                                                    var orderType = OrderType.Sell;
                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask > response.MarketOrder.Price.Value)
                                                                        orderType = OrderType.SellStop;
                                                                    else if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask < response.MarketOrder.Price.Value)
                                                                        orderType = OrderType.SellLimit;

                                                                    // Execute order
                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, slPrice, tpPrice, (int)response.Magic, comment);

                                                                    // Send to logs
                                                                    if (_appConfig.Debug)
                                                                    {
                                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                                        var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price=,SL={slPrice},TP={tpPrice},Magic={response.Magic},Comment={comment}");
                                                                        SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                                    SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "The spread is too high in regards to the risk" });
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Max lot size exceeded" });
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},Price={response.MarketOrder.Price},TP={response.MarketOrder.TakeProfit},SL={response.MarketOrder.StopLoss}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the stop loss price" });
                                                        }
                                                    }

                                                    // SELL STOP
                                                    else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                                && response.OrderType == "SELLSTOP"
                                                                && response.PassiveOrder != null
                                                                && response.PassiveOrder.EntryExpression != null
                                                                && response.PassiveOrder.Risk.HasValue
                                                                && response.PassiveOrder.RiskRewardRatio.HasValue
                                                    )
                                                    {

                                                        // Get the entry price
                                                        var price = await DynamicEvaluator.EvaluateExpressionAsync(response.PassiveOrder.EntryExpression, api.HistoricData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList());

                                                        // Add the spread options
                                                        if (pair.SpreadEntry.HasValue)
                                                        {
                                                            if (pair.SpreadEntry.Value == SpreadExecType.Add)
                                                                price -= spread;
                                                            else if (pair.SpreadEntry.Value == SpreadExecType.Subtract)
                                                                price += spread;
                                                        }

                                                        // Get the Stop Loss price
                                                        var sl = price + (response.PassiveOrder.Risk.Value * Convert.ToDecimal(pair.SLMultiplier));

                                                        // Add the spread options
                                                        if (pair.SpreadSL.HasValue)
                                                        {
                                                            if (pair.SpreadSL.Value == SpreadExecType.Add)
                                                                sl -= spread;
                                                            else if (pair.SpreadSL.Value == SpreadExecType.Subtract)
                                                                sl += spread;
                                                        }

                                                        // Get the Take Profit Price
                                                        var tp = price - (response.PassiveOrder.Risk.Value * response.PassiveOrder.RiskRewardRatio.Value);
                                                        if (pair.SpreadTP.HasValue)
                                                        {
                                                            if (pair.SpreadTP.Value == SpreadExecType.Add)
                                                                tp -= spread;
                                                            else if (pair.SpreadTP.Value == SpreadExecType.Subtract)
                                                                tp += spread;
                                                        }

                                                        // Round
                                                        price = Math.Round(price, metadataTick.Digits, MidpointRounding.AwayFromZero);
                                                        sl = Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero);
                                                        tp = Math.Round(tp, metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                            var description = string.Format($"Price: Price={price},Spread={spread},EntryExpression={response.PassiveOrder.EntryExpression}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });

                                                            description = string.Format($"SL: Price={price},Spread={spread},Risk={response.PassiveOrder.Risk.Value},SLMultiplier={pair.SLMultiplier}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });

                                                            description = string.Format($"TP: Price={price},Spread={spread},Risk={response.PassiveOrder.Risk.Value},RiskRewardRatio={response.PassiveOrder.RiskRewardRatio.Value}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                        }

                                                        // Calculate the lot size
                                                        var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, price, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk); ;

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                            var description = string.Format($"LotSize: StartBalance={startbalance},Balance={api.AccountInfo.Balance},Risk={pair.Risk},AskPrice={metadataTick.Ask},SlPrice={sl},TickValue={metadataTick.TickValue},TickSize={metadataTick.TickSize},LotStep={metadataTick.LotStep},MinLotSize={metadataTick.MinLotSize},MaxLotSize={metadataTick.MaxLotSize},DynRisk={dynRisk}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                        }

                                                        // do 0.0 check
                                                        if (lotSize > 0.0M && price > 0.0M && sl > 0.0M && tp > 0.0M)
                                                        {
                                                            // Do lot size check
                                                            if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                            {
                                                                // Do check if risk is x times the spread
                                                                if (spread * pair.RiskMinXTimesTheSpread < response.PassiveOrder.Risk.Value)
                                                                {
                                                                    // Print on the screen
                                                                    Print(Environment.NewLine);
                                                                    Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                                    Print("Date        : " + DateTime.UtcNow);
                                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                                    if (metadataTick.Ask < price)
                                                                        Print("Order       : SELL LIMIT ORDER");
                                                                    else
                                                                        Print("Order       : SELL STOP ORDER");
                                                                    Print("------------------------------------------------");

                                                                    // Cancel open buy or limit orders
                                                                    if (pair.CancelStopOrLimitOrderWhenNewSignal)
                                                                    {
                                                                        foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol == pair.TickerInMetatrader
                                                                                                                        && f.Value.Type != null
                                                                                                                        && (f.Value.Type.Equals("sellstop") || f.Value.Type.Equals("selllimit"))
                                                                                                                        ))
                                                                        {
                                                                            api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                                            // Send to logs
                                                                            if (_appConfig.Debug)
                                                                            {
                                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                                var description = string.Format($"CancelStopOrLimitOrderWhenNewSignal: Symbol={pair.TickerInMetatrader}");
                                                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                                            }
                                                                        }
                                                                    }

                                                                    // Setup the order
                                                                    var comment = string.Format($"{response.SignalID}/{price}/{sl}/{(int)pair.StrategyNr}/{spread}");
                                                                    var orderType = OrderType.SellStop;
                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask < price)
                                                                        orderType = OrderType.SellLimit;
                                                                    else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Ask < price)
                                                                        orderType = OrderType.Sell;

                                                                    // Execute order
                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, price, sl, tp, (int)response.Magic, comment);

                                                                    // Send to logs
                                                                    if (_appConfig.Debug)
                                                                    {
                                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                        var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price={price},SL={sl},TP={tp},Magic={response.Magic},Comment={comment}");
                                                                        SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                    SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Spread is too high in regards to the risk" });
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Max lot size exceeded" });
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType},EntryExpr={response.PassiveOrder.EntryExpression},Risk={response.PassiveOrder.Risk},RR={response.PassiveOrder.RiskRewardRatio}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Can't find entry candle {response.PassiveOrder.EntryExpression}" });
                                                        }
                                                    }

                                                    // MODIFYSLTOBE
                                                    else if (response.OrderType == "MODIFYSLTOBE" && response.Magic > 0)
                                                    {
                                                        // Check if the ticket still exist as open order
                                                        var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == response.Magic);

                                                        // Null reference check
                                                        if (ticketId.Key > 0 && ticketId.Value.Type != null)
                                                        {
                                                            // Get the strategy number from the comment field
                                                            string[] components = ticketId.Value.Comment != null ? ticketId.Value.Comment.Split('/') : [];
                                                            var offset = 0.0M;
                                                            if (components != null && components.Length == 4)
                                                            {
                                                                _ = decimal.TryParse(components[1], out decimal signalEntryPrice);

                                                                // LONG
                                                                // Signal Price : 1.2
                                                                // Open Price : 1.3
                                                                offset = signalEntryPrice - ticketId.Value.OpenPrice;
                                                            }

                                                            // Init variable
                                                            var sl = 0.0M;

                                                            if (ticketId.Value.Type.Equals("SELL", StringComparison.CurrentCultureIgnoreCase))
                                                            {
                                                                sl = ticketId.Value.OpenPrice + offset;

                                                                // Add the spread options
                                                                if (pair.SpreadSL.HasValue)
                                                                {
                                                                    if (pair.SpreadSL.Value == SpreadExecType.Add)
                                                                        sl -= spread;
                                                                    else if (pair.SpreadSL.Value == SpreadExecType.Subtract)
                                                                        sl += spread;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                // If type is SELL, the SL should be set as BE minus Spread
                                                                sl = ticketId.Value.OpenPrice + offset;

                                                                // Add the spread options
                                                                if (pair.SpreadSL.HasValue)
                                                                {
                                                                    if (pair.SpreadSL.Value == SpreadExecType.Add)
                                                                        sl += spread;
                                                                    else if (pair.SpreadSL.Value == SpreadExecType.Subtract)
                                                                        sl -= spread;
                                                                }
                                                            }

                                                            // Round
                                                            sl = Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                                                var description = string.Format($"SL: OpenPrice={ticketId.Value.OpenPrice},Spread={spread},Offset={offset},Digits={metadataTick.Digits}");
                                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                            }

                                                            // Print on the screen
                                                            Print(Environment.NewLine);
                                                            Print("--------- SEND MODIFY SL TO BE ORDER TO METATRADER ---------");
                                                            Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                            Print("Ticker      : " + pair.TickerInMetatrader);
                                                            Print("Order       : MODIFY SL TO BE ORDER");
                                                            Print("------------------------------------------------");

                                                            // Modify order
                                                            api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, sl, ticketId.Value.TakeProfit);

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                                                var description = string.Format($"ModifyOrder: TicketId={ticketId.Key},Lots={ticketId.Value.Lots},SL={sl},TP={ticketId.Value.TakeProfit}");
                                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // Print on the screen
                                                            Print(Environment.NewLine);
                                                            Print("--------- UNABLE TO FIND TRADE ---------");
                                                            Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                            Print("Ticker      : " + pair.TickerInMetatrader);
                                                            Print("Order       : MODIFY SL TO BE ORDER");
                                                            Print("------------------------------------------------");

                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Unable to find trade" });
                                                        }
                                                    }

                                                    // Close trade
                                                    else if (response.OrderType == "CLOSE" && response.Magic > 0)
                                                    {
                                                        // Check if the ticket still exist as open order
                                                        var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == response.Magic);

                                                        // Null reference check
                                                        if (ticketId.Key > 0)
                                                        {
                                                            // Print on the screen
                                                            Print(Environment.NewLine);
                                                            Print("--------- SEND CLOSE ORDER TO METATRADER ---------");
                                                            Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                            Print("Ticker      : " + pair.TickerInMetatrader);
                                                            Print("Order       : CLOSE ORDER");
                                                            Print("Magic       : " + response.Magic);
                                                            Print("Ticket id   : " + ticketId.Key);
                                                            Print("------------------------------------------------");

                                                            // Modify order
                                                            api.CloseOrder(ticketId.Key, decimal.ToDouble(ticketId.Value.Lots));

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                                                var description = string.Format($"CloseOrder: TicketId={ticketId.Key},Lots={ticketId.Value.Lots}");
                                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // Print on the screen
                                                            Print(Environment.NewLine);
                                                            Print("--------- UNABLE TO FIND TRADE ---------");
                                                            Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                            Print("Ticker      : " + pair.TickerInMetatrader);
                                                            Print("Order       : CLOSE ORDER");
                                                            Print("------------------------------------------------");

                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Unable to find trade" });
                                                        }
                                                    }

                                                    // Close trade
                                                    else if (response.OrderType == "CLOSEALL")
                                                    {
                                                        // Null reference check
                                                        foreach (var order in api.OpenOrders)
                                                        {
                                                            // Get the strategy number from the comment field
                                                            string[] components = order.Value.Comment != null ? order.Value.Comment.Split('/') : [];
                                                            StrategyType strategyType = StrategyType.None;
                                                            if (components != null && components.Length == 5)
                                                            {
                                                                _ = Enum.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyType);
                                                            }

                                                            if (strategyType == pair.StrategyNr)
                                                            {
                                                                // Print on the screen
                                                                Print(Environment.NewLine);
                                                                Print("--------- SEND CLOSE ORDER TO METATRADER ---------");
                                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                                Print("Ticker      : " + pair.TickerInMetatrader);
                                                                Print("Order       : CLOSE ORDER");
                                                                Print("Magic       : " + order.Value.Magic);
                                                                Print("Ticket id   : " + order.Key);
                                                                Print("------------------------------------------------");

                                                                // Modify order
                                                                api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                                                    var description = string.Format($"CloseOrder: TicketId={order.Key},Lots={order.Value.Lots}");
                                                                    SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description });
                                                                }
                                                            }
                                                        }
                                                    }

                                                    if (pair.MaxSpread > 0 && spread > pair.MaxSpread)
                                                    {
                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- !!!! SPREAD TOO HIGH !!!! ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Ticker      : " + pair.TickerInMetatrader);
                                                        Print("------------------------------------------------");
                                                    }
                                                }
                                                else
                                                {
                                                    var message = string.Format($"Symbol={response.Instrument},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                                    SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No market data available for this metatrader. Bid or ask price is 0.0" });
                                                }
                                            }
                                            else
                                            {
                                                var message = string.Format($"Symbol={response.Instrument},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                                SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = "No subscription for this pair and strategy" });
                                            }
                                        }
                                        else
                                        {
                                            var message = string.Format($"Symbol={response.Instrument},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                            SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No market data available for this metatrader" });
                                        }
                                    }
                                    else
                                    {
                                        var message = string.Format($"Symbol={response.Instrument},Type={response.OrderType},Magic={response.Magic},StrategyType={response.StrategyType}");
                                        SendLogToFile(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No account info available for this metatrader" });
                                    }
                                }
                            }
                            else
                            {
                                SendLogToFile(0, new Log() { Time = DateTime.UtcNow, Type = "ERROR", ErrorType = "Response received from the server that is null" });
                            }
                        }
                        else
                        {
                            SendLogToFile(0, new Log() { Time = DateTime.UtcNow, Type = "ERROR", ErrorType = "Message received from the server that is null" });
                        }
                    });

                    // Start the web socket
                    await client.Start();
                }
            }
        }


        private void OnItsTimeToCloseTradeEvent(long clientId, string symbol, StrategyType strategyType)
        {
            // Check if app config is not null
            if (_appConfig != null && _apis != null)
            {
                var api = _apis.FirstOrDefault(f => f.ClientId == clientId);

                // Do null reference check
                if (api != null)
                {
                    // Get the order$
                    foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol != null && f.Value.Symbol.Equals(symbol)))
                    {
                        // Get the strategy number from the comment field
                        string[] components = order.Value.Comment != null ? order.Value.Comment.Split('/') : [];
                        StrategyType strategyNr = StrategyType.None;
                        if (components != null && components.Length == 5)
                        {
                            _ = Enum.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyNr);
                        }

                        if (strategyNr == strategyType)
                        {
                            // Print on the screen
                            Print(Environment.NewLine);
                            Print("--------- SEND CLOSE ORDER TO METATRADER ---------");
                            Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                            Print("Ticker      : " + order.Value.Symbol);
                            Print("Order       : CLOSE ORDER");
                            Print("Magic       : " + order.Value.Magic);
                            Print("Ticket id   : " + order.Key);
                            Print("------------------------------------------------");

                            // Modify order
                            api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));
                        }
                    }
                }
            }
        }

        private void OnCandleCloseEvent(long clientId, string symbol, string timeFrame, DateTime time, decimal open, decimal high, decimal low, decimal close, int tickVolume)
        {
            // Make sure we have the instrument name right
            var instrument = symbol.Replace("_" + timeFrame.ToUpper(), string.Empty);

            // Check if app config is not null
            if (_appConfig != null && _apis != null)
            {
                // Start the system
                foreach (var api in _apis.Where(f => f.ClientId == clientId && f.OpenOrders != null && f.OpenOrders.Count > 0))
                {
                    // Clone the open order
                    foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol != null && f.Value.Symbol.Equals(instrument)).ToDictionary(entry => entry.Key, entry => entry.Value))
                    {

                        // Get the strategy number from the comment field
                        string[] components = order.Value.Comment != null ? order.Value.Comment.Split('/') : [];
                        var offset = 0.0M;
                        var signalEntryPrice = 0.0M;
                        var signalStopLoss = 0.0M;
                        var spread = 0.0M;
                        StrategyType strategyType = StrategyType.None;
                        if (components != null && components.Length == 5)
                        {
                            _ = decimal.TryParse(components[1], out signalEntryPrice);
                            _ = decimal.TryParse(components[2], out signalStopLoss);
                            _ = decimal.TryParse(components[4], out spread);
                            _ = Enum.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyType);

                            // LONG
                            // Signal Price : 1.2
                            // Open Price : 1.3
                            offset = signalEntryPrice - order.Value.OpenPrice;
                        }

                        // Get the right pair back from the local database
                        var pair = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(order.Value.Symbol) && f.StrategyNr == strategyType && f.Timeframe.Equals(timeFrame));

                        // If this broker is listening to this signal and the account size is greater then zero
                        if (pair != null && signalEntryPrice > 0 && api.MarketData != null && signalStopLoss > 0 && strategyType != StrategyType.None)
                        {
                            // When the risk setting is enabled
                            if (pair.Risk > 0)
                            {
                                // Calculate the risk
                                var risk = Math.Abs(signalEntryPrice - signalStopLoss);

                                // Add to the trading journal
                                var marketdata = api.MarketData.FirstOrDefault(f => f.Key == order.Value.Symbol);

                                // Do null reference check
                                if (!string.IsNullOrEmpty(marketdata.Key))
                                {
                                    // If the order is a buy
                                    if (order.Value.Type?.ToUpper() == "BUY")
                                    {
                                        // If the current ASK signalEntryPrice is greater then x times the risk
                                        if (close >= (order.Value.OpenPrice + (Convert.ToDecimal(pair.SLtoBEafterR) * risk) + offset))
                                        {
                                            // Set SL to BE
                                            var slPrice = order.Value.OpenPrice - spread + offset;

                                            // Round
                                            slPrice = Math.Round(slPrice, marketdata.Value.Digits, MidpointRounding.AwayFromZero);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != slPrice)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("------!!! AUTO !!! ------- SEND MODIFY SL TO BE ORDER TO METATRADER ------!!! AUTO !!! -------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                Print("Ticker      : " + pair.TickerInMetatrader);
                                                Print("Order       : MODIFY SL TO BE ORDER");
                                                Print("Lot Size    : " + order.Value.Lots);
                                                Print("Ask         : " + order.Value.OpenPrice);
                                                Print("Stop Loss   : " + slPrice);
                                                Print("Take Profit : " + order.Value.TakeProfit);
                                                Print("Magic       : " + order.Value.Magic);
                                                Print("Strategy    : " + pair.StrategyNr);
                                                Print("Ticket id   : " + order.Key);
                                                Print("------!!! AUTO !!! -------!!! AUTO !!! -------!!! AUTO !!! --------!!! AUTO !!! -------");

                                                // Modify order
                                                api.ModifyOrder(order.Key, order.Value.Lots, 0, slPrice, order.Value.TakeProfit);

                                                var message = string.Format($"Symbol={order.Value.Symbol},Ticket={order.Key},Lots={order.Value.Lots},Type={order.Value.Type},Magic={order.Value.Magic},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={order.Value.StopLoss}");
                                                Task.Run(async () => SendLogToFile(clientId, new Log() { Time = DateTime.UtcNow, Type = "Auto move SL to BE", Message = message }));
                                            }
                                        }
                                    }
                                    else if (order.Value.Type?.ToUpper() == "SELL")
                                    {
                                        // If the current BID signalEntryPrice is smaller then x times the risk
                                        if (close <= (order.Value.OpenPrice - (Convert.ToDecimal(pair.SLtoBEafterR) * risk)) + offset)
                                        {
                                            // Set SL to BE
                                            var slPrice = order.Value.OpenPrice + spread + offset;

                                            // Round
                                            slPrice = Math.Round(slPrice, marketdata.Value.Digits, MidpointRounding.AwayFromZero);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != slPrice)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("------!!! AUTO !!! ------- SEND MODIFY SL TO BE ORDER TO METATRADER ------!!! AUTO !!! -------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                Print("Ticker      : " + pair.TickerInMetatrader);
                                                Print("Order       : MODIFY SL TO BE ORDER");
                                                Print("Lot Size    : " + order.Value.Lots);
                                                Print("Ask         : " + order.Value.OpenPrice);
                                                Print("Stop Loss   : " + slPrice);
                                                Print("Take Profit : " + order.Value.TakeProfit);
                                                Print("Magic       : " + order.Value.Magic);
                                                Print("Strategy    : " + pair.StrategyNr);
                                                Print("Ticket id   : " + order.Key);
                                                Print("------!!! AUTO !!! -------!!! AUTO !!! -------!!! AUTO !!! --------!!! AUTO !!! -------");

                                                // Modify order
                                                api.ModifyOrder(order.Key, order.Value.Lots, 0, slPrice, order.Value.TakeProfit);

                                                // Send log to files
                                                var message = string.Format($"Symbol={order.Value.Symbol},Ticket={order.Key},Lots={order.Value.Lots},Type={order.Value.Type},Magic={order.Value.Magic},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={order.Value.StopLoss}");
                                                Task.Run(async () => SendLogToFile(clientId, new Log() { Time = DateTime.UtcNow, Type = "Auto move SL to BE", Message = message }));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnTickEvent(long clientId, string symbol, decimal bid, decimal ask, decimal tickValue)
        {
            foreach (var api in _apis)
            {
            }
        }

        private void OnLogEvent(long clientId, long id, Log log)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                Print(Environment.NewLine);
                Print("------------------- LOG ------------------------");
                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == clientId).Name);
                Print("Time      : " + log.Time);
                Print("Type      : " + log.Type);
                if (!string.IsNullOrEmpty(log.Message))
                    Print("Object   : " + log.Message);
                if (!string.IsNullOrEmpty(log.ErrorType))
                    Print("Error Type : " + log.ErrorType);
                if (!string.IsNullOrEmpty(log.Description))
                    Print("Description: " + log.Description);
                Print("------------------------------------------------");


                // Send to the server
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = log.Message,
                    ErrorType = log.ErrorType,
                    Type = string.Format($"MT - {log.Type}"),
                });

                // Send log to files
                Task.Run(async () => SendLogToFile(clientId, log));
            }
        }

        private void OnOrderCreateEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                Print(Environment.NewLine);
                Print("-------------- CREATE ORDER ---------------------");
                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == clientId).Name);
                Print("Time      : " + DateTime.UtcNow);
                Print("Symbol    : " + order.Symbol);
                Print("Ticket    : " + ticketId);
                Print("Lot       : " + order.Lots);
                Print("Type      : " + order.Type);
                Print("Magic     : " + order.Magic);
                Print("------------------------------------------------");

                // Send log to the server
                var message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss}");
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = message,
                    Type = "MT - CREATE ORDER",
                });

                // Send to tradingjournal
                SendOrderToBackend(clientId, ticketId, order, false);

                // Send log to files
                Task.Run(async () => SendLogToFile(clientId, new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Create order", Message = message }));
            }
        }

        private void OnOrderUpdateEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                Print(Environment.NewLine);
                Print("-------------- UPDATE ORDER ---------------------");
                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == clientId).Name);
                Print("Time      : " + DateTime.UtcNow);
                Print("Symbol    : " + order.Symbol);
                Print("Ticket    : " + ticketId);
                Print("Lot       : " + order.Lots);
                Print("SL        : " + order.StopLoss);
                Print("TP        : " + order.TakeProfit);
                Print("Magic     : " + order.Magic);
                Print("------------------------------------------------");

                // Send to the server
                var message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss}");
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = message,
                    Type = "MT - UDPATE ORDER",
                });

                // Send to tradingjournal
                SendOrderToBackend(clientId, ticketId, order, false);

                // Send log to files
                Task.Run(async () => SendLogToFile(clientId, new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Update order", Message = message }));
            }
        }

        private void OnOrderRemoveEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                Print(Environment.NewLine);
                Print("-------------- CLOSED ORDER ---------------------");
                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == clientId).Name);
                Print("Time      : " + DateTime.UtcNow);
                Print("Symbol    : " + order.Symbol);
                Print("Ticket    : " + ticketId);
                Print("Lot       : " + order.Lots);
                Print("SL        : " + order.StopLoss);
                Print("TP        : " + order.TakeProfit);
                Print("Magic     : " + order.Magic);
                Print("------------------------------------------------");

                // Send to the server
                var message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss}");
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = message,
                    Type = "MT - CLOSE ORDER",
                });

                // Send to tradingjournal
                SendOrderToBackend(clientId, ticketId, order, true);

                // Send log to files
                Task.Run(async () => SendLogToFile(clientId, new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Close order", Message = message }));
            }
        }

        private void SendOrderToBackend(long clientId, long ticketId, Order order, bool isTradeClosed)
        {
            if (_appConfig != null && _apis != null && _apis.Count(f => f.ClientId == clientId && f.MarketData != null) == 1)
            {
                // Add to the trading journal
                var marketdata = _apis.First(f => f.ClientId == clientId).MarketData.FirstOrDefault(f => f.Key == order.Symbol);

                // Get setup from appconfig
                var pair = _appConfig.Brokers.Where(f => f.ClientId == clientId).SelectMany(f => f.Pairs).Where(f => f.TickerInMetatrader == order.Symbol).FirstOrDefault();

                // Do null reference check
                if (pair != null && !string.IsNullOrEmpty(marketdata.Key))
                {
                    // Init object
                    new AzureFunctionApiClient().SendTradeJournal(new TradeJournalRequest()
                    {
                        AccountID = _appConfig.AccountId,
                        ClientID = clientId,
                        Comment = order.Comment,
                        Commission = order.Commission,
                        CurrentPrice = marketdata.Value.Ask,
                        Lots = order.Lots,
                        Magic = order.Magic,
                        OpenPrice = order.OpenPrice,
                        OpenTime = order.OpenTime,
                        Pnl = order.Pnl,
                        SL = order.StopLoss,
                        StrategyType = pair.StrategyNr,
                        Spread = Math.Round(Math.Abs(marketdata.Value.Bid - marketdata.Value.Ask), 4, MidpointRounding.AwayFromZero),
                        Swap = order.Swap,
                        Symbol = order.Symbol ?? "NONE",
                        TicketId = ticketId,
                        Timeframe = pair.Timeframe,
                        TP = order.TakeProfit,
                        Type = order.Type != null ? order.Type.ToUpper() : "NONE",
                        Risk = pair.Risk,
                        IsTradeClosed = isTradeClosed,
                    });
                }
            }
        }


        public async Task LoadLogFromFileAsync(long clientId)
        {
            string fileName = $"Log-{clientId}.json";
            await _semaphore.WaitAsync();
            try
            {
                if (File.Exists(fileName))
                {
                    var json = await File.ReadAllTextAsync(fileName);
                    var logsFromFile = JsonConvert.DeserializeObject<List<Log>>(json) ?? new List<Log>();

                    var logs = _buffers.GetOrAdd(clientId, new List<Log>());
                    lock (logs) // Ensure thread-safety
                    {
                        // Assuming we want to replace the current buffer with the file contents
                        logs.Clear();
                        logs.AddRange(logsFromFile);

                        // Make sure the last item is on top
                        logs = logs?.OrderByDescending(f => f.Time).ToList();
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void SendLogToFile(long clientId, Log log)
        {
            // Do null reference check
            if (_appConfig != null && _apis != null && _apis.Count(f => f.ClientId == clientId) == 1 && _appConfig.DropLogsInFile)
            {
                // Buffer log
                var logs = _buffers.GetOrAdd(clientId, []);
                lock (logs) // Ensure thread-safety for list modification
                {
                    logs.Add(log);
                }
            }
        }

        private async Task FlushLogsToFile()
        {
            foreach (var clientId in _buffers.Keys)
            {
                List<Log> logsToWrite;
                // Safely retrieve and clear the buffer for the current clientId
                if (_buffers.TryGetValue(clientId, out var logs))
                {
                    lock (logs) // Ensure thread-safety for list modification
                    {
                        logsToWrite = new List<Log>(logs);
                    }

                    // Follow similar logic for writing logs to file
                    // Filename
                    string fileName = $"Log-{clientId}.json";

                    // Wait until it's safe to enter
                    await _semaphore.WaitAsync();

                    try
                    {
                        // Filter logs to keep only the last month's logs
                        logsToWrite = logsToWrite.Where(f => f.Time >= DateTime.UtcNow.AddMonths(-1)).ToList();

                        // Make sure the last item is on top
                        logsToWrite = logsToWrite.OrderByDescending(f => f.Time).ToList();

                        // Write file back
                        await TryWriteFileAsync(fileName, JsonConvert.SerializeObject(logsToWrite));
                    }
                    finally
                    {
                        // Release the lock
                        _semaphore.Release();
                    }
                }
            }
        }

        // Ensure to call this method to properly dispose of the timer when the LogManager is no longer needed
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
