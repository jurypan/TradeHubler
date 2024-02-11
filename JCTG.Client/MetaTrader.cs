using JCTG.Events;
using JCTG.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using static JCTG.Client.Helpers;

namespace JCTG.Client
{
    public class Metatrader : IDisposable
    {

        private readonly TerminalConfig? _appConfig;
        private readonly List<MetatraderApi> _apis;
        private readonly List<DailyTaskScheduler> _timing;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // Initial count 1, maximum count 1
        private readonly ConcurrentDictionary<long, List<Log>> _buffers = new ConcurrentDictionary<long, List<Log>>();
        private Timer _timer;

        public Metatrader(TerminalConfig terminalConfig)
        {
            // Init APP Config + API
            _appConfig = terminalConfig;
            _apis = [];
            _timing = new List<DailyTaskScheduler>();
            _timer = new Timer(async _ => await FlushLogsToFileAsync(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            // Foreach broker, init the API
            foreach (var api in _appConfig.Brokers)
            {
                // Init API
                var _api = new MetatraderApi(api.MetaTraderDirPath, api.ClientId, terminalConfig.SleepDelay, terminalConfig.MaxRetryCommandSeconds, terminalConfig.LoadOrdersFromFile);

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
                    var broker = _appConfig?.Brokers.FirstOrDefault(f => f.ClientId == _api.ClientId && f.IsEnable);

                    // do null reference checks
                    if (_api != null && broker != null && broker.Pairs.Count != 0)
                    {
                        // Load logs into memory
                        await LoadLogFromFileAsync(_api.ClientId);

                        // Init the events
                        _api.OnOrderCreateEvent += OnOrderCreateEvent;
                        _api.OnOrderUpdateEvent += OnOrderUpdateEvent;
                        _api.OnOrderCloseEvent += OnOrderCloseEvent;
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
                // Get web socket _client
                var azurePubSub = Program.Service?.GetService<AzurePubSubClient>();

                // Do null reference check
                if (azurePubSub != null)
                {
                    // When message receive
                    azurePubSub.OnReceivingTradingviewSignalEvent += async (tradingviewAlert) =>
                    {
                        if (tradingviewAlert != null && tradingviewAlert.SignalID > 0 && tradingviewAlert.AccountID == _appConfig.AccountId)
                        {
                            // Iterate through the api's
                            foreach (var api in _apis)
                            {
                                // Get the right pair back from the local database
                                var pair = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInTradingView.Equals(tradingviewAlert.Instrument) && f.StrategyNr == tradingviewAlert.StrategyType);

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
                                                            && tradingviewAlert.OrderType == "BUY"
                                                            && tradingviewAlert.MarketOrder != null
                                                            && tradingviewAlert.MarketOrder.Price.HasValue
                                                            && tradingviewAlert.MarketOrder.StopLoss.HasValue
                                                            && tradingviewAlert.MarketOrder.TakeProfit.HasValue
                                                )
                                                {
                                                    // Calculate SL Price
                                                    var slPrice = RiskCalculator.SLForLong(
                                                            mtPrice: metadataTick.Ask,
                                                            mtSpread: spread,
                                                            mtDigits: metadataTick.Digits,
                                                            signalPrice: tradingviewAlert.MarketOrder.Price.Value,
                                                            signalSL: tradingviewAlert.MarketOrder.StopLoss.Value,
                                                            spreadExecType: pair.SpreadSL,
                                                            pairSlMultiplier: pair.SLMultiplier
                                                            );

                                                    // Send to logs
                                                    if (_appConfig.Debug)
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                        var description = string.Format($"SLForLong: Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits},SignalPrice={tradingviewAlert.MarketOrder.Price},SignalSL={tradingviewAlert.MarketOrder.StopLoss}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                    }

                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, metadataTick.Ask, slPrice, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk);

                                                    // Send to logs
                                                    if (_appConfig.Debug)
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                        var description = string.Format($"LotSize: StartBalance={startbalance},Balance={api.AccountInfo.Balance},Risk={pair.Risk},AskPrice={metadataTick.Ask},SlPrice={slPrice},TickValue={metadataTick.TickValue},TickSize={metadataTick.TickSize},LotStep={metadataTick.LotStep},MinLotSize={metadataTick.MinLotSize},MaxLotSize={metadataTick.MaxLotSize},DynRisk={dynRisk}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
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
                                                                        signalPrice: tradingviewAlert.MarketOrder.Price.Value,
                                                                        signalTP: tradingviewAlert.MarketOrder.TakeProfit.Value,
                                                                        spreadExecType: pair.SpreadTP
                                                                        );

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                                    var description = string.Format($"TPForLong: Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits},SignalPrice={tradingviewAlert.MarketOrder.Price},SignalTP={tradingviewAlert.MarketOrder.TakeProfit}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
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
                                                                var comment = string.Format($"{tradingviewAlert.SignalID}/{Math.Round(tradingviewAlert.MarketOrder.Price.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(tradingviewAlert.MarketOrder.StopLoss.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");
                                                                var orderType = OrderType.Buy;
                                                                if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask < tradingviewAlert.MarketOrder.Price.Value)
                                                                    orderType = OrderType.BuyStop;
                                                                else if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask > tradingviewAlert.MarketOrder.Price.Value)
                                                                    orderType = OrderType.BuyLimit;
                                                                api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, slPrice, tpPrice, (int)tradingviewAlert.Magic, comment);

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                                    var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price=,SL={slPrice},TP={tpPrice},Magic={tradingviewAlert.Magic},Comment={comment}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Spread is too high in regards to the risk" }, tradingviewAlert.SignalID);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Max lot size exceeded" }, tradingviewAlert.SignalID);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the stop loss price" }, tradingviewAlert.SignalID);
                                                    }
                                                }

                                                // BUY STOP
                                                else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                            && tradingviewAlert.OrderType == "BUYSTOP"
                                                            && tradingviewAlert.PassiveOrder != null
                                                            && tradingviewAlert.PassiveOrder.EntryExpression != null
                                                            && tradingviewAlert.PassiveOrder.Risk.HasValue
                                                            && tradingviewAlert.PassiveOrder.RiskRewardRatio.HasValue
                                                )
                                                {

                                                    // Get the entry price
                                                    var price = await DynamicEvaluator.EvaluateExpressionAsync(tradingviewAlert.PassiveOrder.EntryExpression, api.HistoricData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList());

                                                    // Add the spread options
                                                    if (pair.SpreadEntry.HasValue)
                                                    {
                                                        if (pair.SpreadEntry.Value == SpreadExecType.Add)
                                                            price += spread;
                                                        else if (pair.SpreadEntry.Value == SpreadExecType.Subtract)
                                                            price -= spread;
                                                    }

                                                    // Get the Stop Loss price
                                                    var sl = price - (tradingviewAlert.PassiveOrder.Risk.Value * Convert.ToDecimal(pair.SLMultiplier));

                                                    // Add the spread options
                                                    if (pair.SpreadSL.HasValue)
                                                    {
                                                        if (pair.SpreadSL.Value == SpreadExecType.Add)
                                                            sl += spread;
                                                        else if (pair.SpreadSL.Value == SpreadExecType.Subtract)
                                                            sl -= spread;
                                                    }

                                                    // Get the Take Profit Price
                                                    var tp = price + (tradingviewAlert.PassiveOrder.Risk.Value * tradingviewAlert.PassiveOrder.RiskRewardRatio.Value);

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
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                        var description = string.Format($"Price: Price={price},Spread={spread},EntryExpression={tradingviewAlert.PassiveOrder.EntryExpression}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);

                                                        description = string.Format($"SL: Price={price},Spread={spread},Risk={tradingviewAlert.PassiveOrder.Risk.Value},SLMultiplier={pair.SLMultiplier}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);

                                                        description = string.Format($"TP: Price={price},Spread={spread},Risk={tradingviewAlert.PassiveOrder.Risk.Value},RiskRewardRatio={tradingviewAlert.PassiveOrder.RiskRewardRatio.Value}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                    }

                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, price, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk); ;

                                                    // Send to logs
                                                    if (_appConfig.Debug)
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                        var description = string.Format($"LotSize: StartBalance={startbalance},Balance={api.AccountInfo.Balance},Risk={pair.Risk},AskPrice={metadataTick.Ask},SlPrice={sl},TickValue={metadataTick.TickValue},TickSize={metadataTick.TickSize},LotStep={metadataTick.LotStep},MinLotSize={metadataTick.MinLotSize},MaxLotSize={metadataTick.MaxLotSize},DynRisk={dynRisk}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                    }

                                                    // do 0.0 check
                                                    if (lotSize > 0.0M && price > 0.0M && sl > 0.0M && tp > 0.0M)
                                                    {
                                                        // Do lot size check
                                                        if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                        {
                                                            // Do check if risk is x times the spread
                                                            if (spread * pair.RiskMinXTimesTheSpread < tradingviewAlert.PassiveOrder.Risk.Value)
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
                                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                                            var description = string.Format($"CancelStopOrLimitOrderWhenNewSignal: Symbol={pair.TickerInMetatrader}");
                                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                                        }
                                                                    }
                                                                }

                                                                // Open order
                                                                var comment = string.Format($"{tradingviewAlert.SignalID}/{price}/{sl}/{(int)pair.StrategyNr}/{spread}");
                                                                var orderType = OrderType.BuyStop;
                                                                if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask > price)
                                                                    orderType = OrderType.BuyLimit;
                                                                else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Ask > price)
                                                                    orderType = OrderType.Buy;
                                                                api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, price, sl, tp, (int)tradingviewAlert.Magic, comment);

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                                    var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price={price},SL={sl},TP={tp},Magic={tradingviewAlert.Magic},Comment={comment}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Spread is too high in regards to the risk" }, tradingviewAlert.SignalID);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Max lot size exceeded" }, tradingviewAlert.SignalID);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Can't find entry candle {tradingviewAlert.PassiveOrder.EntryExpression}" }, tradingviewAlert.SignalID);
                                                    }
                                                }

                                                // SELL
                                                else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                                    && tradingviewAlert.OrderType == "SELL"
                                                                    && tradingviewAlert.MarketOrder != null
                                                                    && tradingviewAlert.MarketOrder.Price.HasValue
                                                                    && tradingviewAlert.MarketOrder.StopLoss.HasValue
                                                                    && tradingviewAlert.MarketOrder.TakeProfit.HasValue
                                                                    )
                                                {
                                                    // Calculate SL Price
                                                    var slPrice = RiskCalculator.SLForShort(
                                                            mtPrice: metadataTick.Ask,
                                                            mtSpread: spread,
                                                            mtDigits: metadataTick.Digits,
                                                            signalPrice: tradingviewAlert.MarketOrder.Price.Value,
                                                            signalSL: tradingviewAlert.MarketOrder.StopLoss.Value,
                                                            spreadExecType: pair.SpreadSL,
                                                            pairSlMultiplier: pair.SLMultiplier
                                                            );

                                                    // Send to logs
                                                    if (_appConfig.Debug)
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                        var description = string.Format($"SLForShort: Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits},SignalPrice={tradingviewAlert.MarketOrder.Price},SignalSL={tradingviewAlert.MarketOrder.StopLoss}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                    }

                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, metadataTick.Ask, slPrice, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk);

                                                    // Send to logs
                                                    if (_appConfig.Debug)
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                        var description = string.Format($"LotSize: StartBalance={startbalance},Balance={api.AccountInfo.Balance},Risk={pair.Risk},AskPrice={metadataTick.Ask},SlPrice={slPrice},TickValue={metadataTick.TickValue},TickSize={metadataTick.TickSize},LotStep={metadataTick.LotStep},MinLotSize={metadataTick.MinLotSize},MaxLotSize={metadataTick.MaxLotSize},DynRisk={dynRisk}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
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
                                                                        signalPrice: tradingviewAlert.MarketOrder.Price.Value,
                                                                        signalTP: tradingviewAlert.MarketOrder.TakeProfit.Value,
                                                                        spreadExecType: pair.SpreadTP
                                                                        );

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                                    var description = string.Format($"TPForShort: Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits},SignalPrice={tradingviewAlert.MarketOrder.Price},SignalTP={tradingviewAlert.MarketOrder.TakeProfit}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
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
                                                                var comment = string.Format($"{tradingviewAlert.SignalID}/{Math.Round(tradingviewAlert.MarketOrder.Price.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(tradingviewAlert.MarketOrder.StopLoss.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");

                                                                // Passive / Active
                                                                var orderType = OrderType.Sell;
                                                                if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask > tradingviewAlert.MarketOrder.Price.Value)
                                                                    orderType = OrderType.SellStop;
                                                                else if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask < tradingviewAlert.MarketOrder.Price.Value)
                                                                    orderType = OrderType.SellLimit;

                                                                // Execute order
                                                                api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, slPrice, tpPrice, (int)tradingviewAlert.Magic, comment);

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                                    var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price=,SL={slPrice},TP={tpPrice},Magic={tradingviewAlert.Magic},Comment={comment}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "The spread is too high in regards to the risk" }, tradingviewAlert.SignalID);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Max lot size exceeded" }, tradingviewAlert.SignalID);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},Price={tradingviewAlert.MarketOrder.Price},TP={tradingviewAlert.MarketOrder.TakeProfit},SL={tradingviewAlert.MarketOrder.StopLoss}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the stop loss price" }, tradingviewAlert.SignalID);
                                                    }
                                                }

                                                // SELL STOP
                                                else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                            && tradingviewAlert.OrderType == "SELLSTOP"
                                                            && tradingviewAlert.PassiveOrder != null
                                                            && tradingviewAlert.PassiveOrder.EntryExpression != null
                                                            && tradingviewAlert.PassiveOrder.Risk.HasValue
                                                            && tradingviewAlert.PassiveOrder.RiskRewardRatio.HasValue
                                                )
                                                {

                                                    // Get the entry price
                                                    var price = await DynamicEvaluator.EvaluateExpressionAsync(tradingviewAlert.PassiveOrder.EntryExpression, api.HistoricData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList());

                                                    // Add the spread options
                                                    if (pair.SpreadEntry.HasValue)
                                                    {
                                                        if (pair.SpreadEntry.Value == SpreadExecType.Add)
                                                            price -= spread;
                                                        else if (pair.SpreadEntry.Value == SpreadExecType.Subtract)
                                                            price += spread;
                                                    }

                                                    // Get the Stop Loss price
                                                    var sl = price + (tradingviewAlert.PassiveOrder.Risk.Value * Convert.ToDecimal(pair.SLMultiplier));

                                                    // Add the spread options
                                                    if (pair.SpreadSL.HasValue)
                                                    {
                                                        if (pair.SpreadSL.Value == SpreadExecType.Add)
                                                            sl -= spread;
                                                        else if (pair.SpreadSL.Value == SpreadExecType.Subtract)
                                                            sl += spread;
                                                    }

                                                    // Get the Take Profit Price
                                                    var tp = price - (tradingviewAlert.PassiveOrder.Risk.Value * tradingviewAlert.PassiveOrder.RiskRewardRatio.Value);
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
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                        var description = string.Format($"Price: Price={price},Spread={spread},EntryExpression={tradingviewAlert.PassiveOrder.EntryExpression}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);

                                                        description = string.Format($"SL: Price={price},Spread={spread},Risk={tradingviewAlert.PassiveOrder.Risk.Value},SLMultiplier={pair.SLMultiplier}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);

                                                        description = string.Format($"TP: Price={price},Spread={spread},Risk={tradingviewAlert.PassiveOrder.Risk.Value},RiskRewardRatio={tradingviewAlert.PassiveOrder.RiskRewardRatio.Value}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                    }

                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, price, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk); ;

                                                    // Send to logs
                                                    if (_appConfig.Debug)
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                        var description = string.Format($"LotSize: StartBalance={startbalance},Balance={api.AccountInfo.Balance},Risk={pair.Risk},AskPrice={metadataTick.Ask},SlPrice={sl},TickValue={metadataTick.TickValue},TickSize={metadataTick.TickSize},LotStep={metadataTick.LotStep},MinLotSize={metadataTick.MinLotSize},MaxLotSize={metadataTick.MaxLotSize},DynRisk={dynRisk}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                    }

                                                    // do 0.0 check
                                                    if (lotSize > 0.0M && price > 0.0M && sl > 0.0M && tp > 0.0M)
                                                    {
                                                        // Do lot size check
                                                        if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                        {
                                                            // Do check if risk is x times the spread
                                                            if (spread * pair.RiskMinXTimesTheSpread < tradingviewAlert.PassiveOrder.Risk.Value)
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
                                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                                            var description = string.Format($"CancelStopOrLimitOrderWhenNewSignal: Symbol={pair.TickerInMetatrader}");
                                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                                        }
                                                                    }
                                                                }

                                                                // Setup the order
                                                                var comment = string.Format($"{tradingviewAlert.SignalID}/{price}/{sl}/{(int)pair.StrategyNr}/{spread}");
                                                                var orderType = OrderType.SellStop;
                                                                if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask < price)
                                                                    orderType = OrderType.SellLimit;
                                                                else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Ask < price)
                                                                    orderType = OrderType.Sell;

                                                                // Execute order
                                                                api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, price, sl, tp, (int)tradingviewAlert.Magic, comment);

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                                    var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price={price},SL={sl},TP={tp},Magic={tradingviewAlert.Magic},Comment={comment}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Spread is too high in regards to the risk" }, tradingviewAlert.SignalID);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Max lot size exceeded" }, tradingviewAlert.SignalID);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType},EntryExpr={tradingviewAlert.PassiveOrder.EntryExpression},Risk={tradingviewAlert.PassiveOrder.Risk},RR={tradingviewAlert.PassiveOrder.RiskRewardRatio}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Can't find entry candle {tradingviewAlert.PassiveOrder.EntryExpression}" }, tradingviewAlert.SignalID);
                                                    }
                                                }

                                                // MODIFYSLTOBE
                                                else if (tradingviewAlert.OrderType == "MODIFYSLTOBE" && tradingviewAlert.Magic > 0)
                                                {
                                                    // Check if the ticket still exist as open order
                                                    var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == tradingviewAlert.Magic);

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
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                                            var description = string.Format($"SL: OpenPrice={ticketId.Value.OpenPrice},Spread={spread},Offset={offset},Digits={metadataTick.Digits}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
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
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                                            var description = string.Format($"ModifyOrder: TicketId={ticketId.Key},Lots={ticketId.Value.Lots},SL={sl},TP={ticketId.Value.TakeProfit}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
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

                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Unable to find trade" }, tradingviewAlert.SignalID);
                                                    }
                                                }

                                                // Close trade
                                                else if (tradingviewAlert.OrderType == "CLOSE" && tradingviewAlert.Magic > 0)
                                                {
                                                    // Check if the ticket still exist as open order
                                                    var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == tradingviewAlert.Magic);

                                                    // Null reference check
                                                    if (ticketId.Key > 0)
                                                    {
                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- SEND CLOSE ORDER TO METATRADER ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Ticker      : " + pair.TickerInMetatrader);
                                                        Print("Order       : CLOSE ORDER");
                                                        Print("Magic       : " + tradingviewAlert.Magic);
                                                        Print("Ticket id   : " + ticketId.Key);
                                                        Print("------------------------------------------------");

                                                        // Modify order
                                                        api.CloseOrder(ticketId.Key, decimal.ToDouble(ticketId.Value.Lots));

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                                            var description = string.Format($"CloseOrder: TicketId={ticketId.Key},Lots={ticketId.Value.Lots}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
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

                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Unable to find trade" }, tradingviewAlert.SignalID);
                                                    }
                                                }

                                                // Close trade
                                                else if (tradingviewAlert.OrderType == "CLOSEALL")
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
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                                                var description = string.Format($"CloseOrder: TicketId={order.Key},Lots={order.Value.Lots}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description }, tradingviewAlert.SignalID);
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

                                                    var message = string.Format($"Symbol={tradingviewAlert.Instrument},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Spread is too high. Current spread is {spread} and expect max {pair.MaxSpread}" }, tradingviewAlert.SignalID);
                                                }
                                                else
                                                {
                                                    // Send signal to log file
                                                    await SendSignalToJournalFileAsync(api.ClientId, tradingviewAlert);
                                                }
                                            }
                                            else
                                            {
                                                var message = string.Format($"Symbol={tradingviewAlert.Instrument},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No market data available for this metatrader. Bid or ask price is 0.0" }, tradingviewAlert.SignalID);
                                            }
                                        }
                                        else
                                        {
                                            var message = string.Format($"Symbol={tradingviewAlert.Instrument},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = "No subscription for this pair and strategy" });
                                        }
                                    }
                                    else
                                    {
                                        var message = string.Format($"Symbol={tradingviewAlert.Instrument},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No market data available for this metatrader" });
                                    }
                                }
                                else
                                {
                                    var message = string.Format($"Symbol={tradingviewAlert.Instrument},Type={tradingviewAlert.OrderType},Magic={tradingviewAlert.Magic},StrategyType={tradingviewAlert.StrategyType}");
                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No account info available for this metatrader" });
                                }
                            }
                        }
                        else
                        {
                            await LogAsync(0, new Log() { Time = DateTime.UtcNow, Type = "ERROR", ErrorType = "Message received from the server that is null" });
                        }
                    };

                    // Start the web socket
                    await azurePubSub.ListeningToServerAsync();
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
                        long signalId = 0;
                        if (components != null && components.Length == 5)
                        {
                            _ = long.TryParse(components[0], out signalId);
                            _ = Enum.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyNr);
                        }

                        if (strategyNr == strategyType && signalId > 0)
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

                            // Send log to files
                            var message = string.Format($"Symbol={order.Value.Symbol},Ticket={order.Key},Lots={order.Value.Lots},Type={order.Value.Type},Magic={order.Value.Magic},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={order.Value.StopLoss},Comment={order.Value.Comment}");
                            var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = $"Close all trades at {DateTime.UtcNow}", Message = message };

                            // Send log to files
                            Task.Run(async () =>
                            {
                                // Send log to file
                                await LogAsync(clientId, log, signalId);
                            });
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
                        long signalId = 0;
                        var offset = 0.0M;
                        var signalEntryPrice = 0.0M;
                        var signalStopLoss = 0.0M;
                        var spread = 0.0M;
                        StrategyType strategyType = StrategyType.None;
                        if (components != null && components.Length == 5)
                        {
                            _ = long.TryParse(components[0], out signalId);
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

                                                var message = string.Format($"Symbol={order.Value.Symbol},Ticket={order.Key},Lots={order.Value.Lots},Type={order.Value.Type},Magic={order.Value.Magic},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={order.Value.StopLoss},Comment={order.Value.Comment}");
                                                var log = new Log() { Time = DateTime.UtcNow, Type = "Auto move SL to BE", Message = message };

                                                // Send log to BE
                                                Task.Run(async () => 
                                                {
                                                    // Send log to file
                                                    await LogAsync(clientId, log, signalId);
                                                });
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
                                                var message = string.Format($"Symbol={order.Value.Symbol},Ticket={order.Key},Lots={order.Value.Lots},Type={order.Value.Type},Magic={order.Value.Magic},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={order.Value.StopLoss},Comment={order.Value.Comment}");
                                                var log = new Log() { Time = DateTime.UtcNow, Type = "Auto move SL to BE", Message = message };

                                                // Send log to BE
                                                Task.Run(async () =>
                                                {
                                                    // Send log to file
                                                    await LogAsync(clientId, log, signalId);
                                                });
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


                // Send log to files
                Task.Run(async () =>
                {
                    // Send log to files
                    await LogAsync(clientId, log); //signalID TODO
                });
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

                // Send log to files
                var message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss},Comment={order.Comment}");
                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Create order", Message = message };

                // Get the signal id from the comment field
                string[] components = order.Comment != null ? order.Comment.Split('/') : [];
                long signalId = 0;
                if (components != null && components.Length == 5)
                    _ = long.TryParse(components[0], out signalId);


                Task.Run(async () =>
                {
                    // Send the tradejournal to Azure PubSub server
                    await new AzurePubSubServer().SendOnOrderCreateEventAsync(new OnOrderCreateEvent()
                    {
                        ClientID = clientId,
                        SignalID = signalId,
                        Order = order,
                        Log = log
                    });

                    // Send log to files
                    await LogAsync(clientId, log);

                    // Send log to the journal
                    await SendLogToJournalFileAsync(clientId, signalId, log);

                    // Send order to the journal
                    await SendOrderToJournalFileAsync(clientId, signalId, order);
                });
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

                // Send log to files
                var message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss},Comment={order.Comment}");
                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Update order", Message = message };

                // Get the signal id from the comment field
                string[] components = order.Comment != null ? order.Comment.Split('/') : [];
                long signalId = 0;
                if (components != null && components.Length == 5)
                    _ = long.TryParse(components[0], out signalId);


                Task.Run(async () =>
                {
                    // Send the tradejournal to Azure PubSub server
                    await new AzurePubSubServer().SendOnOrderUpdateEventAsync(new OnOrderUpdateEvent()
                    {
                        ClientID = clientId,
                        SignalID = signalId,
                        Order = order,
                        Log = log
                    });

                    // Send log to files
                    await LogAsync(clientId, log);

                    // Send log to the journal
                    await SendLogToJournalFileAsync(clientId, signalId, log);

                    // Send order to the journal
                    await SendOrderToJournalFileAsync(clientId, signalId, order);
                });
            }
        }

        private void OnOrderCloseEvent(long clientId, long ticketId, Order order)
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

                // Send log to files
                var message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss},Comment={order.Comment}");
                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Close order", Message = message };

                // Get the signal id from the comment field
                string[] components = order.Comment != null ? order.Comment.Split('/') : [];
                long signalId = 0;
                if (components != null && components.Length == 5)
                    _ = long.TryParse(components[0], out signalId);

                Task.Run(async () =>
                {
                    // Send the tradejournal to Azure PubSub server
                    await new AzurePubSubServer().SendOnOrderCloseEventAsync(new OnOrderCloseEvent()
                    {
                        ClientID = clientId,
                        SignalID = signalId,
                        Order = order,
                        Log = log
                    });

                    // Send logs to file
                    await LogAsync(clientId, log);

                    // Send log to the journal
                    await SendLogToJournalFileAsync(clientId, signalId, log);

                    // Send order to the journal
                    await SendOrderToJournalFileAsync(clientId, signalId, order, true);
                });
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


        private async Task LogAsync(long clientId, Log log, long? signalId = null)
        {
            // Do null reference check
            if (_appConfig != null && _apis != null && (_apis.Count(f => f.ClientId == clientId) == 1 || clientId == 0) && _appConfig.DropLogsInFile)
            {
                // Send the tradejournal to Azure PubSub server
                await new AzurePubSubServer().SendOnLogEventAsync(new OnLogEvent()
                {
                    ClientID = clientId,
                    SignalID = signalId,
                    Log = log
                });

                // Buffer log
                var logs = _buffers.GetOrAdd(clientId, []);
                lock (logs) // Ensure thread-safety for list modification
                {
                    // Add log to the list
                    logs.Add(log);
                }
            }
        }

        private async Task SendOrderToJournalFileAsync(long clientId, long signalId, Order order, bool isTradeClosed = false)
        {
            // Ensure dependencies are not null
            if (_appConfig == null || _apis == null || !_apis.Any(f => f.ClientId == clientId))
                return;

            string fileName = $"Tradejournal-{clientId}.json";
            List<TradeJournal> journals;

            // Attempt to enter the semaphore (wait if necessary)
            await _semaphore.WaitAsync();
            try
            {
                // Check if the file exists and load its content
                if (File.Exists(fileName))
                {
                    string fileContent = await File.ReadAllTextAsync(fileName);
                    journals = JsonConvert.DeserializeObject<List<TradeJournal>>(fileContent) ?? [];

                    // Make sure the last item is on top
                    journals = [.. journals.OrderByDescending(f => f.DateCreated)];
                }
                else
                {
                    journals = [];
                }

                // Add the new signal to the journal
                var journal = journals.FirstOrDefault(f => f.Signal.SignalID == signalId);

                // Do null reference check
                if (journal != null)
                {
                    // Add to journal
                    journal.Order = order;
                    journal.IsTradeClosed = isTradeClosed;

                    // Serialize and write the journal back to the file
                    string serializedContent = JsonConvert.SerializeObject(journals);
                    await TryWriteFileAsync(fileName, serializedContent);
                }


            }
            finally
            {
                // Release the semaphore to allow another operation to proceed
                _semaphore.Release();
            }

        }

        private async Task SendLogToJournalFileAsync(long clientId, long signalId, Log log)
        {
            // Ensure dependencies are not null
            if (_appConfig == null || _apis == null || !_apis.Any(f => f.ClientId == clientId))
                return;

            string fileName = $"Tradejournal-{clientId}.json";
            List<TradeJournal> journals;

            // Attempt to enter the semaphore (wait if necessary)
            await _semaphore.WaitAsync();
            try
            {
                // Check if the file exists and load its content
                if (File.Exists(fileName))
                {
                    string fileContent = await File.ReadAllTextAsync(fileName);
                    journals = JsonConvert.DeserializeObject<List<TradeJournal>>(fileContent) ?? [];

                    // Make sure the last item is on top
                    journals = [.. journals.OrderByDescending(f => f.DateCreated)];
                }
                else
                {
                    journals = [];
                }

                // Add the new signal to the journal
                var journal = journals.FirstOrDefault(f => f.Signal.SignalID == signalId);

                // Do null reference check
                if (journal != null)
                {
                    // Add the log
                    journal.Logs.Add(log);

                    // Serialize and write the journal back to the file
                    string serializedContent = JsonConvert.SerializeObject(journals);
                    await TryWriteFileAsync(fileName, serializedContent);
                }


            }
            finally
            {
                // Release the semaphore to allow another operation to proceed
                _semaphore.Release();
            }
        }

        private async Task SendSignalToJournalFileAsync(long clientId, OnReceivingTradingviewSignalEvent signal)
        {
            // Ensure dependencies are not null
            if (_appConfig == null || _apis == null || !_apis.Any(f => f.ClientId == clientId))
                return;

            string fileName = $"Tradejournal-{clientId}.json";
            List<TradeJournal> journals;

            // Attempt to enter the semaphore (wait if necessary)
            await _semaphore.WaitAsync();
            try
            {
                // Check if the file exists and load its content
                if (File.Exists(fileName))
                {
                    string fileContent = await File.ReadAllTextAsync(fileName);
                    journals = JsonConvert.DeserializeObject<List<TradeJournal>>(fileContent) ?? [];

                    // Make sure the last item is on top
                    journals = [.. journals.OrderByDescending(f => f.DateCreated)];
                }
                else
                {
                    journals = [];
                }

                // Add the new signal to the journal
                var journal = new TradeJournal { Signal = signal };
                journals.Add(journal);

                // Serialize and write the journal back to the file
                string serializedContent = JsonConvert.SerializeObject(journals);
                await TryWriteFileAsync(fileName, serializedContent);
            }
            finally
            {
                // Release the semaphore to allow another operation to proceed
                _semaphore.Release();
            }
        }

        private async Task FlushLogsToFileAsync()
        {
            foreach (var clientId in _buffers.Keys.ToList()) // ToList to avoid collection modification issues
            {
                List<Log> logsToWrite = [];
                bool logsAvailable = false;

                // Attempt to safely retrieve and clear the buffer for the current clientId
                if (_buffers.TryGetValue(clientId, out var logs))
                {
                    lock (logs) // Ensure thread-safety for list modification
                    {
                        if (logs.Count > 0)
                        {
                            logsToWrite = new List<Log>(logs);
                            logsAvailable = true;
                        }
                    }
                }

                if (logsAvailable)
                {
                    // Filename
                    string fileName = $"Log-{clientId}.json";

                    // Wait until it's safe to enter
                    await _semaphore.WaitAsync();

                    try
                    {
                        // Perform log processing only if there are logs to write
                        // Filter logs to keep only the last month's logs
                        logsToWrite = logsToWrite.Where(log => log.Time >= DateTime.UtcNow.AddMonths(-1)).ToList();

                        // Make sure the last item is on top
                        logsToWrite.Sort((x, y) => y.Time.CompareTo(x.Time));

                        // Write file back
                        await TryWriteFileAsync(fileName, JsonConvert.SerializeObject(logsToWrite));
                    }
                    catch (Exception ex)
                    {
                        // Consider logging the exception or handling it as needed
                        Console.WriteLine($"Error writing logs for clientId {clientId}: {ex.Message}");
                    }
                    finally
                    {
                        // Always release the semaphore
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
