using JCTG.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace JCTG.Client
{
    public class Metatrader
    {

        private TerminalConfig? _appConfig;
        private readonly List<MetatraderApi> _apis;
        private readonly List<RecurringCloseTradeScheduler> _schedulers;


        public Metatrader(TerminalConfig terminalConfig)
        {
            // Init APP Config + API
            _appConfig = terminalConfig;
            _apis = [];
            _schedulers = new List<RecurringCloseTradeScheduler>();

            // Foreach broker, init the API
            foreach (var broker in _appConfig.Brokers.Where(f => f.IsEnable).ToList())
            {
                // Init API
                var _api = new MetatraderApi(broker.MetaTraderDirPath, broker.ClientId, terminalConfig.SleepDelay, terminalConfig.MaxRetryCommandSeconds, true);

                // Add to the list
                _apis.Add(_api);
            }

            // Update terminalConfig
            new AppConfigManager(_appConfig).OnTerminalConfigChange += async (config) =>
            {
                // Print on the screen
                Helpers.Print($"WARNING : {DateTime.UtcNow} / UPDATED CONFIGURATION");

                // Update property
                _appConfig = config;

                // Reboot client
                await StopListenToTheClientsAsync();
                await StartListenToTheClientsAsync();
            };
        }


        public async Task StartListenToTheClientsAsync()
        {
            // Check if app config is not null
            if (_appConfig != null)
            {
                // StartCheckTimeAndExecuteOnceDaily the system
                _ = Parallel.ForEach(_apis, async _api =>
                {
                    // Get the broker from the local database
                    var broker = _appConfig?.Brokers.FirstOrDefault(f => f.ClientId == _api.ClientId);

                    // do null reference checks
                    if (_api != null && broker != null && broker.Pairs.Count != 0)
                    {
                        // Init the events
                        _api.OnOrderCreateEvent += OnOrderCreatedEvent;
                        _api.OnOrderUpdateEvent += OnOrderUpdateEvent;
                        _api.OnOrderCloseEvent += OnOrderCloseEvent;
                        _api.OnLogEvent += OnMetatraderLogEvent;
                        _api.OnCandleCloseEvent += OnCandleCloseEvent;
                        _api.OnDealCreatedEvent += OnDealCreateEvent;
                        _api.OnTickEvent += OnTickEvent;
                        _api.OnAccountInfoChangedEvent += OnAccountInfoChangedEvent;
                        _api.OnHistoricBarDataEvent += OnHistoricBarDataEvent;

                        // StartCheckTimeAndExecuteOnceDaily the API
                        await _api.StartAsync();

                        // Subscribe foreach pair
                        _api.SubscribeForTicks(broker.Pairs.Select(f => f.TickerInMetatrader).ToList());
                        _api.SubscribeForBarData(broker.Pairs.Select(p => new KeyValuePair<string, string>(p.TickerInMetatrader, p.Timeframe)).ToList());
                        _api.GetHistoricData(broker.Pairs);

                        // Init close trades on a particular time
                        foreach (var pair in broker.Pairs)
                        {
                            var timingCloseAllTradesAt = new RecurringCloseTradeScheduler(_api.ClientId, pair.TickerInMetatrader, pair.StrategyID, false);
                            timingCloseAllTradesAt.OnCloseTradeEvent += OnItsTimeToCloseTradeEvent;

                            // Close the trade at a particular time on the day
                            if (pair.CloseAllTradesAt != null)
                            {
                                timingCloseAllTradesAt.Start(pair.CloseAllTradesAt.Value);
                            }

                            // Close the trade within x time of opening the trade
                            if (pair.CloseTradeWithinXBars != null)
                            {
                                // Don't do anythhing, start the sceduler when opening the trade
                            }

                            _schedulers.Add(timingCloseAllTradesAt);
                        }
                    }
                });
            }

            await Task.FromResult(0);
        }

        public async Task StopListenToTheClientsAsync()
        {
            // Check if app config is not null
            if (_appConfig != null)
            {
                // StartCheckTimeAndExecuteOnceDaily the system
                _ = Parallel.ForEach(_apis, async _api =>
                {
                    // Get the broker from the local database
                    var broker = _appConfig?.Brokers.FirstOrDefault(f => f.ClientId == _api.ClientId);

                    // do null reference checks
                    if (_api != null && broker != null && broker.Pairs.Count != 0)
                    {
                        // Init the events
                        _api.OnOrderCreateEvent -= OnOrderCreatedEvent;
                        _api.OnOrderUpdateEvent -= OnOrderUpdateEvent;
                        _api.OnOrderCloseEvent -= OnOrderCloseEvent;
                        _api.OnLogEvent -= OnMetatraderLogEvent;
                        _api.OnCandleCloseEvent -= OnCandleCloseEvent;
                        _api.OnDealCreatedEvent -= OnDealCreateEvent;
                        _api.OnTickEvent -= OnTickEvent;
                        _api.OnAccountInfoChangedEvent -= OnAccountInfoChangedEvent;
                        _api.OnHistoricBarDataEvent -= OnHistoricBarDataEvent;

                        // StartCheckTimeAndExecuteOnceDaily the API
                        await _api.StopAsync();

                        // Init close trades on a particular time
                        foreach (var timing in _schedulers)
                        {
                            timing.OnCloseTradeEvent -= OnItsTimeToCloseTradeEvent;
                        }
                    }
                });
            }

            await Task.FromResult(0);
        }

        public async Task StartListenToTheServerAsync()
        {
            // Do null reference checks
            if (_appConfig != null && _apis != null)
            {
                // Get web socket _client
                var azureQueue = Program.Service?.GetService<AzureQueueClient>();

                // Do null reference check
                if (azureQueue != null)
                {
                    // OnSendTradingviewSignalCommand
                    azureQueue.OnSendTradingviewSignalCommand += (cmd) =>
                    {
                        if (cmd != null && cmd.SignalID > 0 && cmd.AccountID == _appConfig.AccountId)
                        {
                            // Make the query
                            var query = _apis.Where(f => f.IsActive);
                            if (cmd.ClientIDs != null && cmd.ClientIDs.Count != 0)
                                query = query.Where(f => cmd.ClientIDs.Contains(f.ClientId));

                            // Iterate through the broker's
                            Parallel.ForEach(query, async api =>
                            {
                                // Print
                                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / NEW SIGNAL / {cmd.SignalID}");

                                // Get the right pair back from the local database
                                var pair = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInTradingView.Equals(cmd.Instrument) && f.StrategyID == cmd.StrategyID);

                                // StartCheckTimeAndExecuteOnceDaily balance
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

                                            // Do null reference checks
                                            if (metadataTick != null && metadataTick.Ask > 0 && metadataTick.Bid > 0 && metadataTick.Digits >= 0)
                                            {
                                                // Calculate spread
                                                var spread = Calculator.CalculateSpread(metadataTick.Ask, metadataTick.Bid, metadataTick.TickSize, metadataTick.Digits);

                                                // BUY
                                                if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                            && cmd.OrderType.Equals("BUY", StringComparison.CurrentCultureIgnoreCase)
                                                            && cmd.MarketOrder != null
                                                            && cmd.MarketOrder.Risk.HasValue
                                                            && cmd.MarketOrder.RiskRewardRatio.HasValue
                                                )
                                                {
                                                    // Do correlation check
                                                    if (CorrelatedPairs.IsNotCorrelated(pair.TickerInMetatrader, "BUY", pair.CorrelatedPairs, api.OpenOrders))
                                                    {
                                                        // Do do not open a deal x minutes before close
                                                        if (RecurringCloseTradeScheduler.CanOpenTrade(pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose))
                                                        {
                                                            // Init price
                                                            var price = metadataTick.Ask;

                                                            // Calculate SL Price
                                                            var sl = Calculator.StoplossForLong(
                                                                entryPrice: price,
                                                                askPrice: metadataTick.Ask,
                                                                risk: cmd.MarketOrder.Risk.Value,
                                                                slMultiplier: pair.SLMultiplier,
                                                                stopLossExpression: cmd.MarketOrder.StopLossExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                spreadExecType: pair.SpreadSL,
                                                                tickSize: metadataTick.TickSize,
                                                                out Dictionary<string, string> logMessagesSL);

                                                            // Send to logs
                                                            LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);

                                                            // Get the Take Profit Price
                                                            var tp = Calculator.TakeProfitForLong(
                                                                entryPrice: price,
                                                                risk: cmd.MarketOrder.Risk.Value,
                                                                slMultiplier: pair.SLMultiplier,
                                                                stopLossExpression: cmd.MarketOrder.StopLossExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                spreadExecType: pair.SpreadTP,
                                                                riskRewardRatio: cmd.MarketOrder.RiskRewardRatio.Value,
                                                                out Dictionary<string, string> logMessagesTP);

                                                            // Send to logs
                                                            LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                            // Calculate the lot size
                                                            var lotSize = Calculator.LotSize(
                                                                startBalance: startbalance,
                                                                accountBalance: api.AccountInfo.Balance,
                                                                riskPercent: pair.RiskLong,
                                                                entryPrice: price,
                                                                stopLossPrice: sl,
                                                                tickValue: metadataTick.TickValue,
                                                                tickSize: metadataTick.TickSize,
                                                                lotStep: metadataTick.LotStep,
                                                                minLotSizeAllowed: metadataTick.MinLotSize,
                                                                maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                                out Dictionary<string, string> logMessagesLOT,
                                                                riskData: dynRisk);

                                                            // Send to logs
                                                            LogFactory.CalculateLotSize(api.ClientId, _appConfig.Debug, cmd, logMessagesLOT);

                                                            // do 0.0 lotsize check
                                                            if (lotSize > 0.0M)
                                                            {
                                                                // Do 0.0 SL check
                                                                if (sl > 0.0M)
                                                                {
                                                                    // Do 0.0 TP check
                                                                    if (tp > 0.0M)
                                                                    {
                                                                        // Do lot size check
                                                                        if (pair.MaxLotSize <= 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                                        {
                                                                            // Do check if risk is x times the spread
                                                                            if (pair.RiskMinXTimesTheSpread <= 0 || (spread * pair.RiskMinXTimesTheSpread < Math.Abs(price - sl)))
                                                                            {
                                                                                // Print on the screen
                                                                                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / BUY COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                                                // Cancel open buy or limit orders
                                                                                if (pair.CancelStopOrLimitOrderWhenNewSignal)
                                                                                {
                                                                                    foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol == pair.TickerInMetatrader
                                                                                                                                    && f.Value.Type != null
                                                                                                                                    && (f.Value.Type.Equals("buystop") || f.Value.Type.Equals("buylimit") || f.Value.Type.Equals("buy"))
                                                                                                                                    ))
                                                                                    {
                                                                                        // Close the order
                                                                                        api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                                                        // Send to logs
                                                                                        LogFactory.CloseOrderCommand(api.ClientId, _appConfig.Debug, cmd, order.Key, decimal.ToDouble(order.Value.Lots), order.Value.Magic);
                                                                                    }
                                                                                }

                                                                                // Generate order type
                                                                                var orderType = OrderType.Buy;

                                                                                // Round
                                                                                price = Calculator.RoundToNearestTickSize(price, metadataTick.TickSize, metadataTick.Digits);
                                                                                sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                // Generate comment
                                                                                var comment = Calculator.GenerateComment(cmd.SignalID, price, sl, pair.StrategyID, spread);

                                                                                // Execute order
                                                                                api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, Convert.ToInt32(cmd.SignalID), comment);

                                                                                // Send to logs
                                                                                LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, Convert.ToInt32(cmd.SignalID), comment);
                                                                            }
                                                                            else
                                                                            {
                                                                                // Raise market abstention or error
                                                                                await MarketAbstentionFactory.RiskShouldBeAtLeastXTimesTheSpreadAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.RiskMinXTimesTheSpread, price, sl);
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            // Raise market abstention or error
                                                                            await MarketAbstentionFactory.AmountOfLotSizeShouldBeSmallerThenMaxLotsizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.MaxLotSize, lotSize);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        // Raise market abstention or error
                                                                        await MarketAbstentionFactory.ExceptionCalculatingTakeProfitPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, tp);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    // Raise market abstention or error
                                                                    await MarketAbstentionFactory.ExceptionCalculatingStopLossPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, sl);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                // Raise market abstention or error
                                                                await MarketAbstentionFactory.ExceptionCalculatingLotSizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, lotSize);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // Raise market abstention or error
                                                            await MarketAbstentionFactory.MarketWillBeClosedWithinXMinutesAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Raise market abstention or error
                                                        await MarketAbstentionFactory.CorrelatedPairFoundAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.TickerInMetatrader, "BUY", pair.CorrelatedPairs, api.OpenOrders);
                                                    }
                                                }

                                                // BUY STOP
                                                else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                            && cmd.OrderType.Equals("BUYSTOP", StringComparison.CurrentCultureIgnoreCase)
                                                            && cmd.PassiveOrder != null
                                                            && cmd.PassiveOrder.EntryExpression != null
                                                            && cmd.PassiveOrder.Risk.HasValue
                                                            && cmd.PassiveOrder.RiskRewardRatio.HasValue
                                                )
                                                {
                                                    // Do correlation check
                                                    if (CorrelatedPairs.IsNotCorrelated(pair.TickerInMetatrader, "BUY", pair.CorrelatedPairs, api.OpenOrders))
                                                    {
                                                        // Do do not open a deal x minutes before close
                                                        if (RecurringCloseTradeScheduler.CanOpenTrade(pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose))
                                                        {
                                                            // Get the entry entryPrice
                                                            var price = Calculator.EntryPriceForLong(
                                                                risk: cmd.PassiveOrder.Risk.Value,
                                                                entryExpression: cmd.PassiveOrder.EntryExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                spreadExecType: pair.SpreadEntry,
                                                                logMessages: out Dictionary<string, string> logMessagesENTRY);

                                                            // Send to logs
                                                            LogFactory.CalculateEntry(api.ClientId, _appConfig.Debug, cmd, logMessagesENTRY);

                                                            // Do 0.0 check
                                                            if (price.HasValue)
                                                            {
                                                                // Calculate SL Price
                                                                var sl = Calculator.StoplossForLong(
                                                                    entryPrice: price.Value,
                                                                    askPrice: metadataTick.Ask,
                                                                    risk: cmd.PassiveOrder.Risk.Value,
                                                                    slMultiplier: pair.SLMultiplier,
                                                                    stopLossExpression: cmd.PassiveOrder.StopLossExpression,
                                                                    bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                    spread: spread,
                                                                    spreadExecType: pair.SpreadSL,
                                                                    tickSize: metadataTick.TickSize,
                                                                    out Dictionary<string, string> logMessagesSL);

                                                                // Send to logs
                                                                LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);

                                                                // Get the Take Profit Price
                                                                var tp = Calculator.TakeProfitForLong(
                                                                    entryPrice: price.Value,
                                                                    risk: cmd.PassiveOrder.Risk.Value,
                                                                    slMultiplier: pair.SLMultiplier,
                                                                    stopLossExpression: cmd.PassiveOrder.StopLossExpression,
                                                                    bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                    spread: spread,
                                                                    spreadExecType: pair.SpreadTP,
                                                                    riskRewardRatio: cmd.PassiveOrder.RiskRewardRatio.Value,
                                                                    out Dictionary<string, string> logMessagesTP);

                                                                // Send to logs
                                                                LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                                // Calculate the lot size
                                                                var lotSize = Calculator.LotSize(
                                                                    startBalance: startbalance,
                                                                    accountBalance: api.AccountInfo.Balance,
                                                                    riskPercent: pair.RiskLong,
                                                                    entryPrice: price.Value,
                                                                    stopLossPrice: sl,
                                                                    tickValue: metadataTick.TickValue,
                                                                    tickSize: metadataTick.TickSize,
                                                                    lotStep: metadataTick.LotStep,
                                                                    minLotSizeAllowed: metadataTick.MinLotSize,
                                                                    maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                                    out Dictionary<string, string> logMessagesLOT,
                                                                    riskData: dynRisk);

                                                                // Send to logs
                                                                LogFactory.CalculateLotSize(api.ClientId, _appConfig.Debug, cmd, logMessagesLOT);

                                                                // do 0.0 check
                                                                if (lotSize > 0.0M)
                                                                {
                                                                    // Do 0.0 check
                                                                    if (sl > 0.0M)
                                                                    {
                                                                        // Do 0.0 check
                                                                        if (tp > 0.0M)
                                                                        {
                                                                            // Do lot size check
                                                                            if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                                            {
                                                                                // Do check if risk is x times the spread
                                                                                if (pair.RiskMinXTimesTheSpread == 0 || (spread * pair.RiskMinXTimesTheSpread < Math.Abs(price.Value - sl)))
                                                                                {
                                                                                    // Cancel open buy or limit orders
                                                                                    if (pair.CancelStopOrLimitOrderWhenNewSignal)
                                                                                    {
                                                                                        foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol == pair.TickerInMetatrader
                                                                                                                                        && f.Value.Type != null
                                                                                                                                        && (f.Value.Type.Equals("buystop") || f.Value.Type.Equals("buylimit") || f.Value.Type.Equals("buy"))
                                                                                                                                        ))
                                                                                        {
                                                                                            // Execute
                                                                                            api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                                                            // Send to logs
                                                                                            LogFactory.CloseOrderCommand(api.ClientId, _appConfig.Debug, cmd, order.Key, decimal.ToDouble(order.Value.Lots), order.Value.Magic);
                                                                                        }
                                                                                    }

                                                                                    // Generate order type
                                                                                    var orderType = OrderType.BuyStop;
                                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask <= price)
                                                                                        orderType = OrderType.BuyLimit;
                                                                                    else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Ask >= price)
                                                                                        orderType = OrderType.Buy;

                                                                                    // Round
                                                                                    price = Calculator.RoundToNearestTickSize(price.Value, metadataTick.TickSize, metadataTick.Digits);
                                                                                    sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                    tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                    // Generate comment
                                                                                    var comment = Calculator.GenerateComment(cmd.SignalID, price.Value, sl, pair.StrategyID, spread);

                                                                                    // Print on the screen
                                                                                    Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / {orderType.GetDescription().ToUpper()} COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                                                    // Execute order
                                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Buy ? 0 : price.Value, sl, tp, Convert.ToInt32(cmd.SignalID), comment);

                                                                                    // Send to logs
                                                                                    LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Buy ? 0 : price.Value, sl, tp, Convert.ToInt32(cmd.SignalID), comment);
                                                                                }
                                                                                else
                                                                                {
                                                                                    // Raise market abstention or error
                                                                                    await MarketAbstentionFactory.RiskShouldBeAtLeastXTimesTheSpreadAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.RiskMinXTimesTheSpread, price.Value, sl);
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                // Raise market abstention or error
                                                                                await MarketAbstentionFactory.AmountOfLotSizeShouldBeSmallerThenMaxLotsizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.MaxLotSize, lotSize);
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            // Raise market abstention or error
                                                                            await MarketAbstentionFactory.ExceptionCalculatingTakeProfitPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, tp);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        // Raise market abstention or error
                                                                        await MarketAbstentionFactory.ExceptionCalculatingStopLossPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, sl);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    // Raise market abstention or error
                                                                    await MarketAbstentionFactory.ExceptionCalculatingLotSizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, lotSize);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                // Raise market abstention or error
                                                                await MarketAbstentionFactory.ExceptionCalculatingEntryPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, cmd.PassiveOrder.EntryExpression, DynamicEvaluator.GetDateFromBarString(cmd.PassiveOrder.EntryExpression));
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // Raise market abstention or error
                                                            await MarketAbstentionFactory.MarketWillBeClosedWithinXMinutesAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Raise market abstention or error
                                                        await MarketAbstentionFactory.CorrelatedPairFoundAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.TickerInMetatrader, "BUY", pair.CorrelatedPairs, api.OpenOrders);
                                                    }
                                                }

                                                // SELL
                                                else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                                    && cmd.OrderType == "SELL"
                                                                    && cmd.MarketOrder != null
                                                                    && cmd.MarketOrder.Risk.HasValue
                                                                    && cmd.MarketOrder.RiskRewardRatio.HasValue
                                                                    )
                                                {
                                                    // Do correlation check
                                                    if (CorrelatedPairs.IsNotCorrelated(pair.TickerInMetatrader, "SELL", pair.CorrelatedPairs, api.OpenOrders))
                                                    {
                                                        // Do do not open a deal x minutes before close
                                                        if (RecurringCloseTradeScheduler.CanOpenTrade(pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose))
                                                        {
                                                            // Init
                                                            var price = metadataTick.Bid;

                                                            // Calculate SL Price
                                                            var sl = Calculator.StoplossForShort(
                                                                entryPrice: price,
                                                                bidPrice: metadataTick.Bid,
                                                                risk: cmd.MarketOrder.Risk.Value,
                                                                slMultiplier: pair.SLMultiplier,
                                                                stopLossExpression: cmd.MarketOrder.StopLossExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                spreadExecType: pair.SpreadSL,
                                                                tickSize: metadataTick.TickSize,
                                                                out Dictionary<string, string> logMessagesSL);

                                                            // Send to logs
                                                            LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);

                                                            // Get the Take Profit Price
                                                            var tp = Calculator.TakeProfitForShort(
                                                                entryPrice: price,
                                                                risk: cmd.MarketOrder.Risk.Value,
                                                                slMultiplier: pair.SLMultiplier,
                                                                stopLossExpression: cmd.MarketOrder.StopLossExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                spreadExecType: pair.SpreadTP,
                                                                riskRewardRatio: cmd.MarketOrder.RiskRewardRatio.Value,
                                                                out Dictionary<string, string> logMessagesTP);

                                                            // Send to logs
                                                            LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                            // Calculate the lot size
                                                            var lotSize = Calculator.LotSize(
                                                                startBalance: startbalance,
                                                                accountBalance: api.AccountInfo.Balance,
                                                                riskPercent: pair.RiskShort,
                                                                entryPrice: price,
                                                                stopLossPrice: sl,
                                                                tickValue: metadataTick.TickValue,
                                                                tickSize: metadataTick.TickSize,
                                                                lotStep: metadataTick.LotStep,
                                                                minLotSizeAllowed: metadataTick.MinLotSize,
                                                                maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                                out Dictionary<string, string> logMessagesLOT,
                                                                riskData: dynRisk);

                                                            // Send to logs
                                                            LogFactory.CalculateLotSize(api.ClientId, _appConfig.Debug, cmd, logMessagesLOT);

                                                            // do 0.0 check
                                                            if (lotSize > 0.0M)
                                                            {
                                                                // Do 0.0 check
                                                                if (sl > 0.0M)
                                                                {
                                                                    // Do 0.0 check
                                                                    if (tp > 0.0M)
                                                                    {
                                                                        // Do lot size check
                                                                        if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                                        {
                                                                            // Do check if risk is x times the spread
                                                                            if (pair.RiskMinXTimesTheSpread <= 0 || (spread * pair.RiskMinXTimesTheSpread < Math.Abs(sl - price)))
                                                                            {
                                                                                // Print on the screen
                                                                                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / SELL COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                                                // Cancel open buy or limit orders
                                                                                if (pair.CancelStopOrLimitOrderWhenNewSignal)
                                                                                {
                                                                                    foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol == pair.TickerInMetatrader
                                                                                                                                    && f.Value.Type != null
                                                                                                                                    && (f.Value.Type.Equals("sellstop") || f.Value.Type.Equals("selllimit") || f.Value.Type.Equals("sell"))
                                                                                                                                    ))
                                                                                    {
                                                                                        // Close the order
                                                                                        api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                                                        // Send to logs
                                                                                        LogFactory.CloseOrderCommand(api.ClientId, _appConfig.Debug, cmd, order.Key, decimal.ToDouble(order.Value.Lots), order.Value.Magic);
                                                                                    }
                                                                                }

                                                                                //  Generate order type
                                                                                var orderType = OrderType.Sell;

                                                                                // Round
                                                                                price = Calculator.RoundToNearestTickSize(price, metadataTick.TickSize, metadataTick.Digits);
                                                                                sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                // Generate comment
                                                                                var comment = Calculator.GenerateComment(cmd.SignalID, price, sl, pair.StrategyID, spread);

                                                                                // Execute order
                                                                                api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, Convert.ToInt32(cmd.SignalID), comment);

                                                                                // Send to logs
                                                                                LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, Convert.ToInt32(cmd.SignalID), comment);
                                                                            }
                                                                            else
                                                                            {
                                                                                // Raise market abstention or error
                                                                                await MarketAbstentionFactory.RiskShouldBeAtLeastXTimesTheSpreadAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.RiskMinXTimesTheSpread, price, sl);
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            // Raise market abstention or error
                                                                            await MarketAbstentionFactory.AmountOfLotSizeShouldBeSmallerThenMaxLotsizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.MaxLotSize, lotSize);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        // Raise market abstention or error
                                                                        await MarketAbstentionFactory.ExceptionCalculatingTakeProfitPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, tp);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    // Raise market abstention or error
                                                                    await MarketAbstentionFactory.ExceptionCalculatingStopLossPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, sl);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                // Raise market abstention or error
                                                                await MarketAbstentionFactory.ExceptionCalculatingLotSizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, lotSize);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // Raise market abstention or error
                                                            await MarketAbstentionFactory.MarketWillBeClosedWithinXMinutesAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Raise market abstention or error
                                                        await MarketAbstentionFactory.CorrelatedPairFoundAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.TickerInMetatrader, "SELL", pair.CorrelatedPairs, api.OpenOrders);
                                                    }
                                                }

                                                // SELL STOP
                                                else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                            && cmd.OrderType == "SELLSTOP"
                                                            && cmd.PassiveOrder != null
                                                            && cmd.PassiveOrder.EntryExpression != null
                                                            && cmd.PassiveOrder.Risk.HasValue
                                                            && cmd.PassiveOrder.RiskRewardRatio.HasValue
                                                )
                                                {
                                                    // Do correlation check
                                                    if (CorrelatedPairs.IsNotCorrelated(pair.TickerInMetatrader, "SELL", pair.CorrelatedPairs, api.OpenOrders))
                                                    {
                                                        // Do do not open a deal x minutes before close
                                                        if (RecurringCloseTradeScheduler.CanOpenTrade(pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose))
                                                        {
                                                            // Get the entry entryPrice
                                                            var price = Calculator.EntryPriceForShort(
                                                                risk: cmd.PassiveOrder.Risk.Value,
                                                                entryExpression: cmd.PassiveOrder.EntryExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                spreadExecType: pair.SpreadEntry,
                                                                logMessages: out Dictionary<string, string> logMessagesENTRY);

                                                            // Send to logs
                                                            LogFactory.CalculateEntry(api.ClientId, _appConfig.Debug, cmd, logMessagesENTRY);

                                                            // do 0.0 check
                                                            if (price.HasValue)
                                                            {
                                                                // Calculate SL Price
                                                                var sl = Calculator.StoplossForShort(
                                                                    entryPrice: price.Value,
                                                                    bidPrice: metadataTick.Bid,
                                                                    risk: cmd.PassiveOrder.Risk.Value,
                                                                    slMultiplier: pair.SLMultiplier,
                                                                    stopLossExpression: cmd.PassiveOrder.StopLossExpression,
                                                                    bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                    spread: spread,
                                                                    spreadExecType: pair.SpreadSL,
                                                                    tickSize: metadataTick.TickSize,
                                                                    out Dictionary<string, string> logMessagesSL);

                                                                // Send to logs
                                                                LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);

                                                                // Get the Take Profit Price
                                                                var tp = Calculator.TakeProfitForShort(
                                                                    entryPrice: price.Value,
                                                                    risk: cmd.PassiveOrder.Risk.Value,
                                                                    slMultiplier: pair.SLMultiplier,
                                                                    stopLossExpression: cmd.PassiveOrder.StopLossExpression,
                                                                    bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                    spread: spread,
                                                                    spreadExecType: pair.SpreadTP,
                                                                    riskRewardRatio: cmd.PassiveOrder.RiskRewardRatio.Value,
                                                                    out Dictionary<string, string> logMessagesTP);

                                                                // Send to logs
                                                                LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                                // Calculate the lot size
                                                                var lotSize = Calculator.LotSize(
                                                                    startBalance: startbalance,
                                                                    accountBalance: api.AccountInfo.Balance,
                                                                    riskPercent: pair.RiskShort,
                                                                    entryPrice: price.Value,
                                                                    stopLossPrice: sl,
                                                                    tickValue: metadataTick.TickValue,
                                                                    tickSize: metadataTick.TickSize,
                                                                    lotStep: metadataTick.LotStep,
                                                                    minLotSizeAllowed: metadataTick.MinLotSize,
                                                                    maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                                    out Dictionary<string, string> logMessagesLOT,
                                                                    riskData: dynRisk);

                                                                // Send to logs
                                                                LogFactory.CalculateLotSize(api.ClientId, _appConfig.Debug, cmd, logMessagesLOT);

                                                                // do 0.0 check
                                                                if (lotSize > 0.0M)
                                                                {
                                                                    // do 0.0 check
                                                                    if (sl > 0.0M)
                                                                    {
                                                                        // do 0.0 check
                                                                        if (tp > 0.0M)
                                                                        {
                                                                            // Do lot size check
                                                                            if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                                            {
                                                                                // Do check if risk is x times the spread
                                                                                if (pair.RiskMinXTimesTheSpread == 0 || (spread * pair.RiskMinXTimesTheSpread < Math.Abs(sl - price.Value)))
                                                                                {

                                                                                    // Cancel open buy or limit orders
                                                                                    if (pair.CancelStopOrLimitOrderWhenNewSignal)
                                                                                    {
                                                                                        foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol == pair.TickerInMetatrader
                                                                                                                                        && f.Value.Type != null
                                                                                                                                        && (f.Value.Type.Equals("sellstop") || f.Value.Type.Equals("selllimit") || f.Value.Type.Equals("sell"))
                                                                                                                                        ))
                                                                                        {
                                                                                            // Execute order
                                                                                            api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                                                            // Send to logs
                                                                                            LogFactory.CloseOrderCommand(api.ClientId, _appConfig.Debug, cmd, order.Key, decimal.ToDouble(order.Value.Lots), order.Value.Magic);
                                                                                        }
                                                                                    }

                                                                                    // Generate order type
                                                                                    var orderType = OrderType.SellStop;
                                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask < price.Value)
                                                                                        orderType = OrderType.SellLimit;
                                                                                    else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Ask <= price.Value)
                                                                                        orderType = OrderType.Sell;

                                                                                    // Round
                                                                                    price = Calculator.RoundToNearestTickSize(price.Value, metadataTick.TickSize, metadataTick.Digits);
                                                                                    sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                    tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                    // Generate comment
                                                                                    var comment = Calculator.GenerateComment(cmd.SignalID, price.Value, sl, pair.StrategyID, spread);

                                                                                    // Print on the screen
                                                                                    Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / {orderType.GetDescription().ToUpper()} COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                                                    // Execute order
                                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Sell ? 0 : price.Value, sl, tp, (int)cmd.SignalID, comment);

                                                                                    // Send to logs
                                                                                    LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Sell ? 0 : price.Value, sl, tp, (int)cmd.SignalID, comment);
                                                                                }
                                                                                else
                                                                                {
                                                                                    // Raise market abstention or error
                                                                                    await MarketAbstentionFactory.RiskShouldBeAtLeastXTimesTheSpreadAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.RiskMinXTimesTheSpread, price.Value, sl);
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                // Raise market abstention or error
                                                                                await MarketAbstentionFactory.AmountOfLotSizeShouldBeSmallerThenMaxLotsizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.MaxLotSize, lotSize);
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            // Raise market abstention or error
                                                                            await MarketAbstentionFactory.ExceptionCalculatingTakeProfitPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, tp);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        // Raise market abstention or error
                                                                        await MarketAbstentionFactory.ExceptionCalculatingStopLossPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, sl);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    // Raise market abstention or error
                                                                    await MarketAbstentionFactory.ExceptionCalculatingLotSizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, lotSize);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                // Raise market abstention or error
                                                                await MarketAbstentionFactory.ExceptionCalculatingEntryPriceAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, cmd.PassiveOrder.EntryExpression, DynamicEvaluator.GetDateFromBarString(cmd.PassiveOrder.EntryExpression));
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // Raise market abstention or error
                                                            await MarketAbstentionFactory.MarketWillBeClosedWithinXMinutesAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Raise market abstention or error
                                                        await MarketAbstentionFactory.CorrelatedPairFoundAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.TickerInMetatrader, "BUY", pair.CorrelatedPairs, api.OpenOrders);
                                                    }
                                                }

                                                // MOVESLTOBE
                                                else if (cmd.OrderType == "MOVESLTOBE" && cmd.SignalID > 0)
                                                {
                                                    // Check if the ticket still exist as open order
                                                    var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == cmd.SignalID);

                                                    // Null reference check
                                                    if (ticketId.Key > 0 && ticketId.Value.Type != null)
                                                    {
                                                        // Init variable
                                                        var sl = 0.0M;

                                                        if (ticketId.Value.Type.Equals("SELL", StringComparison.CurrentCultureIgnoreCase))
                                                        {
                                                            // Calculate SL Price
                                                            sl = Calculator.StoplossToBreakEvenForShort(
                                                                entryPrice: ticketId.Value.OpenPrice,
                                                                bidPrice: metadataTick.Bid,
                                                                spread: spread,
                                                                spreadExecType: pair.SpreadSLtoBE,
                                                                tickSize: metadataTick.TickSize,
                                                                out Dictionary<string, string> logMessagesSL);

                                                            // Send to logs
                                                            LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);
                                                        }
                                                        else
                                                        {
                                                            // Calculate SL Price
                                                            sl = Calculator.StoplossToBreakEvenForLong(
                                                                entryPrice: ticketId.Value.OpenPrice,
                                                                askPrice: metadataTick.Ask,
                                                                spread: spread,
                                                                spreadExecType: pair.SpreadSLtoBE,
                                                                tickSize: metadataTick.TickSize,
                                                                out Dictionary<string, string> logMessagesSL);

                                                            // Send to logs
                                                            LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);
                                                        }

                                                        // Round
                                                        sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);

                                                        // Print on the screen
                                                        Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / MODIFY SL TO BE COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                        // Modify order
                                                        api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, sl, ticketId.Value.TakeProfit, Convert.ToInt32(cmd.SignalID));

                                                        // Send to logs
                                                        LogFactory.ModifyOrderCommand(api.ClientId, _appConfig.Debug, cmd, ticketId.Key, ticketId.Value.Lots, 0, sl, ticketId.Value.TakeProfit, Convert.ToInt32(cmd.SignalID));

                                                    }
                                                    else
                                                    {
                                                        // Print on the screen
                                                        Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} {pair.TickerInMetatrader} / MODIFY SL TO BE / {cmd.SignalID} / UNABLE TO FIND TRADE");

                                                        // Send to logs
                                                        LogFactory.UnableToFindOrder(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID);
                                                    }
                                                }

                                                // Close or cancel order
                                                else if ((cmd.OrderType == "CLOSE" || cmd.OrderType == "CANCEL") && cmd.SignalID > 0)
                                                {
                                                    // Check if the ticket still exist as open order
                                                    var order = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == cmd.SignalID);

                                                    // Null reference check
                                                    if (order.Key > 0)
                                                    {
                                                        // Print on the screen
                                                        Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} {pair.TickerInMetatrader} / {cmd.OrderType} COMMAND / {cmd.SignalID}");

                                                        // Close order
                                                        api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                        // Send to logs
                                                        LogFactory.CloseOrderCommand(api.ClientId, _appConfig.Debug, cmd, order.Key, decimal.ToDouble(order.Value.Lots), order.Value.Magic);
                                                    }
                                                    else
                                                    {
                                                        // Print on the screen
                                                        Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / CLOSE COMMAND / {cmd.SignalID} / UNABLE TO FIND TRADE / {cmd.StrategyID}");

                                                        // Send to logs
                                                        LogFactory.UnableToFindOrder(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID);
                                                    }
                                                }

                                                // Close ALL
                                                else if (cmd.OrderType == "CLOSEALL")
                                                {
                                                    // Null reference check
                                                    foreach (var order in api.OpenOrders)
                                                    {
                                                        // Get the strategy number from the comment field
                                                        var strategyType = Calculator.GetStrategyIdFromComment(order.Value.Comment);

                                                        if (strategyType == pair.StrategyID)
                                                        {
                                                            // Print on the screen
                                                            Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / CLOSEALL COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                            // Modify order
                                                            api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                            // Send to logs
                                                            LogFactory.CloseOrderCommand(api.ClientId, _appConfig.Debug, cmd, order.Key, decimal.ToDouble(order.Value.Lots), order.Value.Magic);
                                                        }
                                                    }
                                                }

                                                if (pair.MaxSpread > 0 && spread >= pair.MaxSpread)
                                                {
                                                    // Print on the screen
                                                    Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader}  / {cmd.SignalID} /  SPREAD TOO HIGH / {cmd.StrategyID}");

                                                    // Raise market abstention or error
                                                    await MarketAbstentionFactory.SpreadIsTooHighAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.MaxSpread, spread);
                                                }
                                            }
                                            else
                                            {
                                                // Raise market abstention or error
                                                await MarketAbstentionFactory.NoMarketDataAvailableAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID);
                                            }
                                        }
                                        else
                                        {
                                            // Raise market abstention or error
                                            await MarketAbstentionFactory.NoSubscriptionForThisPairAndStrategyAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID);
                                        }
                                    }
                                    else
                                    {
                                        // Raise market abstention or error
                                        await MarketAbstentionFactory.NoMarketDataAvailableAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID);
                                    }
                                }
                                else
                                {
                                    // Raise market abstention or error
                                    await MarketAbstentionFactory.NoAccountInfoAvailableAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID);
                                }
                            });
                        }
                        else
                        {
                            // Raise market abstention or error
                            LogFactory.UnableToLinkCommandToAccount(0, _appConfig.Debug);
                        }
                    };

                    // OnSendManualOrderCommand
                    azureQueue.OnSendManualOrderCommand += (cmd) =>
                    {
                        if (cmd != null && cmd.AccountID == _appConfig.AccountId && cmd.ClientInstruments != null)
                        {
                            // Make the query
                            var query = _apis.Where(f => f.IsActive);
                            if (cmd.ClientInstruments != null && cmd.ClientInstruments.Count >= 1)
                                query = query.Where(f => cmd.ClientInstruments.Select(f => f.ClientID).Contains(f.ClientId));

                            // Iterate through the broker's
                            Parallel.ForEach(query, async api =>
                            {
                                // Print
                                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / MANUAL ORDER / {cmd.Magic}");

                                // Null reference check
                                if (cmd.ClientInstruments != null && cmd.ClientInstruments.Count != 0)
                                {
                                    // Pair
                                    var pair = cmd.ClientInstruments.First(f => f.ClientID == api.ClientId);

                                    // If this broker is listening to this signal and the account size is greater then zero
                                    if (api.AccountInfo != null)
                                    {
                                        // Get the metadata tick
                                        var metadataTick = api.MarketData.FirstOrDefault(f => f.Key == pair.Instrument).Value;

                                        // Get start balance
                                        var startbalance = _appConfig.Brokers.First(f => f.ClientId == api.ClientId).StartBalance;

                                        // Do we have all the data available?
                                        if (metadataTick != null && metadataTick.Ask > 0 && metadataTick.Bid > 0 && metadataTick.Digits >= 0)
                                        {
                                            // Calculate spread
                                            var spread = Calculator.CalculateSpread(metadataTick.Ask, metadataTick.Bid, metadataTick.TickSize, metadataTick.Digits);

                                            // BUY
                                            if (cmd.OrderType == "BUY" && cmd.MarketOrder != null)
                                            {
                                                // Init price
                                                var price = metadataTick.Ask;

                                                // Get the Stop Loss entryPrice
                                                var sl = Convert.ToDecimal(cmd.MarketOrder.StopLossPrice);

                                                // Generate risk
                                                var risk = price - Convert.ToDecimal(cmd.MarketOrder.StopLossPrice);

                                                // Get the Take Profit Price
                                                var tp = Calculator.TakeProfitForLong(
                                                    entryPrice: price,
                                                    risk: risk,
                                                    slMultiplier: 1,
                                                    stopLossExpression: null,
                                                    bars: [],
                                                    spread: spread,
                                                    spreadExecType: null,
                                                    riskRewardRatio: Convert.ToDecimal(cmd.MarketOrder.RiskRewardRatio),
                                                    out Dictionary<string, string> logMessagesTP);

                                                // Send to logs
                                                LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                // Calculate the lot size
                                                var lotSize = Calculator.LotSize(
                                                    startBalance: startbalance,
                                                    accountBalance: api.AccountInfo.Balance,
                                                    riskPercent: Convert.ToDecimal(cmd.ProcentRiskOfBalance),
                                                    entryPrice: price,
                                                    stopLossPrice: sl,
                                                    tickValue: metadataTick.TickValue,
                                                    tickSize: metadataTick.TickSize,
                                                    lotStep: metadataTick.LotStep,
                                                    minLotSizeAllowed: metadataTick.MinLotSize,
                                                    maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                    out Dictionary<string, string> logMessagesLOT,
                                                    riskData: null);

                                                // Send to logs
                                                LogFactory.CalculateLotSize(api.ClientId, _appConfig.Debug, cmd, logMessagesLOT);

                                                // do 0.0 lotsize check
                                                if (lotSize > 0.0M)
                                                {
                                                    // Do 0.0 SL check
                                                    if (sl > 0.0M && tp > 0.0M)
                                                    {
                                                        // Print on the screen
                                                        Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.Instrument} / BUY COMMAND / {cmd.Magic} / {cmd.StrategyID}");

                                                        // Generate order type
                                                        var orderType = OrderType.Buy;

                                                        // Round
                                                        price = Calculator.RoundToNearestTickSize(price, metadataTick.TickSize, metadataTick.Digits);
                                                        sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                        tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                        // Generate comment
                                                        var comment = Calculator.GenerateComment(cmd.Magic, price, sl, cmd.StrategyID, spread);

                                                        // Execute order
                                                        api.ExecuteOrder(pair.Instrument, orderType, lotSize, 0, sl, tp, cmd.Magic, comment);

                                                        // Send to logs
                                                        LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.Instrument, orderType, lotSize, 0, sl, tp, cmd.Magic, comment);
                                                    }
                                                }
                                                else
                                                {
                                                    // Raise market abstention or error
                                                    await MarketAbstentionFactory.ExceptionCalculatingLotSizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.Magic, lotSize);
                                                }
                                            }

                                            // SELL
                                            else if (cmd.OrderType == "SELL" && cmd.MarketOrder != null)
                                            {
                                                // Init price
                                                var price = metadataTick.Bid;

                                                // Get the Stop Loss entryPrice
                                                var sl = Convert.ToDecimal(cmd.MarketOrder.StopLossPrice);

                                                // Generate risk
                                                var risk = Math.Abs(Convert.ToDecimal(cmd.MarketOrder.StopLossPrice) - price);

                                                // Get the Take Profit Price
                                                var tp = Calculator.TakeProfitForShort(
                                                    entryPrice: price,
                                                    risk: risk,
                                                    slMultiplier: 1,
                                                    stopLossExpression: null,
                                                    bars: new List<BarData>(),
                                                    spread: spread,
                                                    spreadExecType: null,
                                                    riskRewardRatio: Convert.ToDecimal(cmd.MarketOrder.RiskRewardRatio),
                                                    out Dictionary<string, string> logMessagesTP);

                                                // Send to logs
                                                LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                // Calculate the lot size
                                                var lotSize = Calculator.LotSize(
                                                    startBalance: startbalance,
                                                    accountBalance: api.AccountInfo.Balance,
                                                    riskPercent: Convert.ToDecimal(cmd.ProcentRiskOfBalance),
                                                    entryPrice: price,
                                                    stopLossPrice: sl,
                                                    tickValue: metadataTick.TickValue,
                                                    tickSize: metadataTick.TickSize,
                                                    lotStep: metadataTick.LotStep,
                                                    minLotSizeAllowed: metadataTick.MinLotSize,
                                                    maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                    out Dictionary<string, string> logMessagesLOT,
                                                    riskData: null);

                                                // Send to logs
                                                LogFactory.CalculateLotSize(api.ClientId, _appConfig.Debug, cmd, logMessagesLOT);

                                                // do 0.0 lotsize check
                                                if (lotSize > 0.0M)
                                                {
                                                    // Do 0.0 SL check
                                                    if (sl > 0.0M && tp > 0.0M)
                                                    {
                                                        // Print on the screen
                                                        Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.Instrument} / SELL COMMAND / {cmd.Magic} / {cmd.StrategyID}");

                                                        // Generate order type
                                                        var orderType = OrderType.Sell;

                                                        // Round
                                                        price = Calculator.RoundToNearestTickSize(price, metadataTick.TickSize, metadataTick.Digits);
                                                        sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                        tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                        // Generate comment
                                                        var comment = Calculator.GenerateComment(cmd.Magic, price, sl, cmd.StrategyID, spread);

                                                        // Execute order
                                                        api.ExecuteOrder(pair.Instrument, orderType, lotSize, 0, sl, tp, cmd.Magic, comment);

                                                        // Send to logs
                                                        LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.Instrument, orderType, lotSize, 0, sl, tp, cmd.Magic, comment);
                                                    }
                                                }
                                                else
                                                {
                                                    // Raise market abstention or error
                                                    await MarketAbstentionFactory.ExceptionCalculatingLotSizeAsync(api.ClientId, _appConfig.Debug, cmd, cmd.Magic, lotSize);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Raise market abstention or error
                                        await MarketAbstentionFactory.NoAccountInfoAvailableAsync(api.ClientId, _appConfig.Debug, cmd, cmd.Magic);
                                    }
                                }
                            });
                        }
                        else
                        {
                            // Send to logs
                            LogFactory.UnableToLinkCommandToAccount(0, _appConfig.Debug);
                        }
                    };

                    // StartCheckTimeAndExecuteOnceDaily listening to the queue
                    await azureQueue.ListeningToServerAsync();
                }
            }
        }




        private void OnItsTimeToCloseTradeEvent(long clientID, string instrument, long strategyId)
        {
            // Check if app config is not null
            if (_appConfig != null && _apis != null)
            {
                var api = _apis.FirstOrDefault(f => f.ClientId == clientID);

                // Do null reference check
                if (api != null && api.IsActive && api.OpenOrders != null)
                {
                    // Get the order$
                    foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol != null && f.Value.Symbol.Equals(instrument)))
                    {
                        // Get the strategy number from the comment field
                        var signalID = Calculator.GetSignalIdFromComment(order.Value.Comment);
                        var strategyID = Calculator.GetStrategyIdFromComment(order.Value.Comment);

                        if (signalID.HasValue && strategyId == strategyID)
                        {
                            // Print on the screen
                            Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {order.Value.Symbol} / IT'S TIME TO CLOSE THE ORDER EVENT / {order.Value.Magic} / {strategyID}");

                            // Modify order
                            api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                            // Send to logs
                            LogFactory.CloseOrderByScheduler(clientID, _appConfig.Debug, order.Value, order.Key, decimal.ToDouble(order.Value.Lots), order.Value.Magic);
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
                // StartCheckTimeAndExecuteOnceDaily the system
                foreach (var api in _apis.Where(f => f.IsActive && f.ClientId == clientId && f.OpenOrders != null && f.OpenOrders.Count > 0))
                {
                    // Clone the open order
                    foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol != null && f.Value.Symbol.Equals(instrument)).ToDictionary(entry => entry.Key, entry => entry.Value))
                    {
                        // Get the strategy number from the comment field
                        string[] components = order.Value.Comment != null ? order.Value.Comment.Split('/') : [];
                        var signalId = Calculator.GetSignalIdFromComment(order.Value.Comment);
                        var signalEntryPrice = Calculator.GetEntryPriceFromComment(order.Value.Comment);
                        var signalStopLoss = Calculator.GetStoplossFromComment(order.Value.Comment);
                        var spread = Calculator.GetSpreadFromComment(order.Value.Comment);
                        var strategyID = Calculator.GetStrategyIdFromComment(order.Value.Comment);


                        // Get the right pair back from the local database
                        var pair = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(order.Value.Symbol) && f.StrategyID == strategyID && f.Timeframe.Equals(timeFrame));

                        // If this broker is listening to this signal and the account size is greater then zero
                        if (pair != null && signalEntryPrice > 0 && api.MarketData != null && signalEntryPrice.HasValue && signalStopLoss.HasValue && strategyID.HasValue && spread.HasValue)
                        {
                            // When the risk setting is enabled
                            if (pair.RiskLong > 0 && pair.RiskShort > 0 && pair.SLtoBEafterR > 0)
                            {
                                // Calculate the risk
                                var risk = Math.Abs(signalEntryPrice.Value - signalStopLoss.Value);

                                // Add to the trading journal
                                var metadataTick = api.MarketData.FirstOrDefault(f => f.Key == order.Value.Symbol);

                                // Do null reference check
                                if (!string.IsNullOrEmpty(metadataTick.Key))
                                {
                                    // If the order is a buy
                                    if (order.Value.Type?.ToUpper() == "BUY")
                                    {
                                        // If the current ASK signalEntryPrice is greater then x times the risk
                                        if (close >= (order.Value.OpenPrice + (Convert.ToDecimal(pair.SLtoBEafterR) * risk)))
                                        {
                                            // Calculate SL Price
                                            var sl = Calculator.StoplossToBreakEvenForLong(
                                                entryPrice: order.Value.OpenPrice,
                                                askPrice: metadataTick.Value.Ask,
                                                spread: spread.Value,
                                                spreadExecType: pair.SpreadSLtoBE,
                                                tickSize: metadataTick.Value.TickSize,
                                                out Dictionary<string, string> logMessagesSL);

                                            // Round
                                            sl = Calculator.RoundToNearestTickSize(sl, metadataTick.Value.TickSize, metadataTick.Value.Digits);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != sl)
                                            {
                                                // Send to logs
                                                LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, order.Value, logMessagesSL);

                                                // Print on the screen
                                                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {order.Value.Symbol} / AUTO - MODIFY SL TO BE / {order.Value.Magic} / {strategyID}");

                                                // Modify order
                                                api.ModifyOrder(order.Key, order.Value.Lots, 0, sl, order.Value.TakeProfit, order.Value.Magic);

                                                // Send to logs
                                                LogFactory.ModifyOrderByAutoMoveSLtoBE(clientId, _appConfig.Debug, order.Value, order.Key, order.Value.Lots, 0, sl, order.Value.TakeProfit, order.Value.Magic);
                                            }
                                        }
                                    }
                                    else if (order.Value.Type?.ToUpper() == "SELL")
                                    {
                                        // If the current BID signalEntryPrice is smaller then x times the risk
                                        if (close <= (order.Value.OpenPrice - (Convert.ToDecimal(pair.SLtoBEafterR) * risk)))
                                        {
                                            // Calculate SL Price
                                            var sl = Calculator.StoplossToBreakEvenForShort(
                                                entryPrice: order.Value.OpenPrice,
                                                bidPrice: metadataTick.Value.Bid,
                                                spread: spread.Value,
                                                spreadExecType: pair.SpreadSLtoBE,
                                                tickSize: metadataTick.Value.TickSize,
                                                out Dictionary<string, string> logMessagesSL);

                                            // Round
                                            sl = Calculator.RoundToNearestTickSize(sl, metadataTick.Value.TickSize, metadataTick.Value.Digits);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != sl)
                                            {
                                                // Send to logs
                                                LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, order.Value, logMessagesSL);

                                                // Print on the screen
                                                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {order.Value.Symbol} / AUTO - MODIFY SL TO BE / {order.Value.Magic} / {strategyID}");

                                                // Modify order
                                                api.ModifyOrder(order.Key, order.Value.Lots, 0, sl, order.Value.TakeProfit, order.Value.Magic);

                                                // Send to logs
                                                LogFactory.ModifyOrderByAutoMoveSLtoBE(clientId, _appConfig.Debug, order.Value, order.Key, order.Value.Lots, 0, sl, order.Value.TakeProfit, order.Value.Magic);
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

        private void OnHistoricBarDataEvent(long clientId, string symbol, string timeframe)
        {
            // Do null reference check
            if (_appConfig != null && _apis.Count(f => f.ClientId == clientId) == 1)
            {
                // Send log to files
                var message = string.Format($"HistoricBarData || Symbol={symbol},Timeframe={timeframe}");
                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Received historic data", Message = message };

                var historicDataForSymbol = _apis.First(f => f.ClientId == clientId).HistoricBarData
                    .Where(pair => pair.Key == symbol) // Filter for the specific instrument
                    .SelectMany(pair => pair.Value.BarData) // Use SelectMany to flatten the lists into a single list
                    .ToList(); // Convert to List<BarData>

            }
        }

        private void OnMetatraderLogEvent(long clientId, long id, Log log)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // When log have an error and have a magic id => market abstention
                if (log.Type?.ToUpper() == "ERROR" && log.ErrorType != null && log.ErrorType.Equals("OPEN_ORDER") && log.Magic.HasValue && log.Magic.Value > 0 && log.Description != null)
                {
                    Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / OPEN ORDER ERROR / {log.ErrorType}");

                    // Symbol
                    string pattern = @"order:\s(\w+),";
                    Match match = Regex.Match(log.Description, pattern);
                    var symbol = match.Success ? match.Groups[1].Value : "unknown";

                    // Order type
                    string[] orderTypes = { "buy", "sell", "buylimit", "selllimit", "buystop", "sellstop" };
                    pattern = $@"order:\s\w+,({string.Join("|", orderTypes)}),";
                    match = Regex.Match(log.Description, pattern);
                    var orderType = match.Success ? match.Groups[1].Value : "unknown";

                    // Send log to files
                    LogFactory.ErrorExecuteOrderEvent(clientId, _appConfig.Debug, log, symbol, orderType, log.Magic.HasValue ? log.Magic.Value : 0);
                }
                else
                {
                    Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / MT LOG / {log.Message}");
                }
            }
        }

        private void OnOrderCreatedEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Get the signal id from the comment field
                var signalID = Calculator.GetSignalIdFromComment(order.Comment);
                var strategyID = Calculator.GetStrategyIdFromComment(order.Comment);

                // Print on the screen
                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {order.Symbol} / CREATED ORDER EVENT / {order.Magic} / {strategyID}");

                // Send to logs
                LogFactory.CreatedAnOrderEvent(clientId, _appConfig.Debug, order, ticketId, order.Magic);
            }
        }

        private void OnOrderUpdateEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Get the signal id from the comment field
                var signalID = Calculator.GetSignalIdFromComment(order.Comment);
                var strategyID = Calculator.GetStrategyIdFromComment(order.Comment);

                // Print on the screen
                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {order.Symbol} / UPDATED ORDER EVENT / {order.Magic} / {strategyID}");

                // Send to logs
                LogFactory.UpdatedAnOrderEvent(clientId, _appConfig.Debug, order, ticketId, order.Magic);
            }
        }

        private void OnOrderCloseEvent(long clientId, long ticketId, Order order)
        {
            // Get pair
            var api = _apis.First(f => f.ClientId == clientId);

            var marketdata = api.MarketData?.FirstOrDefault(f => f.Key == order.Symbol).Value;

            // Do null reference check
            if (_appConfig != null && marketdata != null)
            {
                // Get the signal id from the comment field
                var signalID = Calculator.GetSignalIdFromComment(order.Comment);
                var strategyID = Calculator.GetStrategyIdFromComment(order.Comment);

                // Print on the screen
                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {order.Symbol} / CLOSED ORDER EVENT / {order.Magic} / {strategyID}");

                // Send to logs
                LogFactory.ClosedAnOrderEvent(clientId, _appConfig.Debug, order, ticketId, marketdata.Ask, order.Magic);
            }
        }

        private void OnDealCreateEvent(long clientId, long tradeId, Deal deal)
        {
            // Do null reference check
            if (_appConfig != null && _apis.Count(f => f.ClientId == clientId) == 1)
            {
                // Get api
                var api = _apis.First(f => f.ClientId == clientId);

                // Check if we need to start the scheduler
                var pair = _appConfig.Brokers.FirstOrDefault(f => f.ClientId == clientId && f.Pairs.Any(g => g.TickerInMetatrader == deal.Symbol && g.CloseTradeWithinXBars.HasValue))?
                                    .Pairs.FirstOrDefault(g => g.TickerInMetatrader == deal.Symbol && g.CloseTradeWithinXBars.HasValue);

                // check if pair is not null
                if (pair != null && pair.CloseTradeWithinXBars.HasValue)
                {
                    var scheduler = new RecurringCloseTradeScheduler(clientId, pair.TickerInMetatrader, pair.StrategyID, true);
                    scheduler.OnCloseTradeEvent += OnItsTimeToCloseTradeEvent;
                    var targetTime = DateTime.UtcNow.TimeOfDay.Add(pair.TimeframeAsTimespan * (pair.CloseTradeWithinXBars.Value + 1));
                    scheduler.Start(targetTime);
                    _schedulers.Add(scheduler);

                    // Print on screen
                    Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {deal.Symbol} / CLOSE TRADE @ {targetTime} / {deal.Magic}");
                }


                // Do null reference chekc on metadatatick
                if (api.MarketData != null)
                {
                    // Get the metadata tick
                    var metadataTick = api.MarketData.FirstOrDefault(f => f.Key == deal.Symbol).Value;

                    // Do null reference chekc on metadatatick
                    if (metadataTick != null)
                    {
                        // Get the spread
                        var spread = Calculator.CalculateSpread(metadataTick.Ask, metadataTick.Bid, metadataTick.TickSize, metadataTick.Digits);

                        // Get the cost of the spread
                        var costOfSpread = Calculator.CalculateCostSpread(spread, deal.Lots, metadataTick.TickSize, metadataTick.Digits, metadataTick.TickValue);

                        // Send logs
                        LogFactory.CreatedADealEvent(clientId, _appConfig.Debug, deal, tradeId, deal.Magic, api.AccountInfo?.Balance, api.AccountInfo?.Equity, metadataTick.Ask, spread, costOfSpread);
                    }
                }
            }
        }

        private void OnAccountInfoChangedEvent(long clientId, AccountInfo accountInfo)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Send to logs
                LogFactory.AccountInfoChangedEvent(clientId, _appConfig.Debug, accountInfo);
            }
        }


    }
}
