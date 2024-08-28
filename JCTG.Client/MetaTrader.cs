using JCTG.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace JCTG.Client
{
    public class Metatrader
    {

        private TerminalConfig? _appConfig;
        private readonly List<MetatraderApi> _apis;
        private readonly List<RecurringCloseTradeScheduler> _recurringCloseTradeScheduler;
        private readonly List<SpreadMonitor> _spreadMonitors;


        public Metatrader(TerminalConfig terminalConfig)
        {
            // InitAndStart APP Config + API
            _appConfig = terminalConfig;
            _apis = [];
            _recurringCloseTradeScheduler = [];
            _spreadMonitors = [];

            // Foreach broker, init the API
            foreach (var broker in _appConfig.Brokers.Where(f => f.IsEnable).ToList())
            {
                // InitAndStart API
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
                        // InitAndStart the events
                        _api.OnOrderCreateEvent += OnOrderCreatedEvent;
                        _api.OnOrderUpdateEvent += OnOrderUpdateEvent;
                        _api.OnOrderCloseEvent += OnOrderCloseEvent;
                        _api.OnLogEvent += OnMetatraderLogEvent;
                        _api.OnCandleCloseEvent += OnCandleCloseEvent;
                        _api.OnDealCreatedEvent += OnDealCreatedEvent;
                        _api.OnTickEvent += OnTickEvent;
                        _api.OnAccountInfoChangedEvent += OnAccountInfoChangedEvent;
                        _api.OnHistoricBarDataEvent += OnHistoricBarDataEvent;

                        // StartCheckTimeAndExecuteOnceDaily the API
                        await _api.StartAsync();

                        // Subscribe foreach pair
                        _api.SubscribeForTicks(broker.Pairs.Select(f => f.TickerInMetatrader).ToList());
                        _api.SubscribeForBarData(broker.Pairs.Select(p => new KeyValuePair<string, string>(p.TickerInMetatrader, p.Timeframe)).ToList());
                        _api.GetHistoricData(broker.Pairs);

                        // InitAndStart close trades on a particular time
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

                            _recurringCloseTradeScheduler.Add(timingCloseAllTradesAt);
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
                        // InitAndStart the events
                        _api.OnOrderCreateEvent -= OnOrderCreatedEvent;
                        _api.OnOrderUpdateEvent -= OnOrderUpdateEvent;
                        _api.OnOrderCloseEvent -= OnOrderCloseEvent;
                        _api.OnLogEvent -= OnMetatraderLogEvent;
                        _api.OnCandleCloseEvent -= OnCandleCloseEvent;
                        _api.OnDealCreatedEvent -= OnDealCreatedEvent;
                        _api.OnTickEvent -= OnTickEvent;
                        _api.OnAccountInfoChangedEvent -= OnAccountInfoChangedEvent;
                        _api.OnHistoricBarDataEvent -= OnHistoricBarDataEvent;

                        // StartCheckTimeAndExecuteOnceDaily the API
                        await _api.StopAsync();

                        // InitAndStart close trades on a particular time
                        foreach (var timing in _recurringCloseTradeScheduler)
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
                                var pair = Calculator.GetPairByTradingviewInstrument(_appConfig, api.ClientId, cmd.Instrument, cmd.StrategyID);

                                // StartCheckTimeAndExecuteOnceDaily balance
                                var startbalance = _appConfig.Brokers.First(f => f.ClientId == api.ClientId).StartBalance;

                                // Get dynamic risk
                                var dynRisk = Calculator.GetDynamicRisk(_appConfig, api.ClientId);

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
                                                            // InitAndStart entryBidPrice
                                                            var entryBidPrice = metadataTick.Bid;

                                                            // Calculate SL Price
                                                            var sl = Calculator.StoplossForLong(
                                                                entryBidPrice: entryBidPrice,
                                                                risk: cmd.MarketOrder.Risk.Value,
                                                                slMultiplier: pair.SLMultiplier,
                                                                stopLossExpression: cmd.MarketOrder.StopLossExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                tickSize: metadataTick.TickSize,
                                                                out Dictionary<string, string> logMessagesSL);

                                                            // Send to logs
                                                            LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);

                                                            // Get the Take Profit Price
                                                            var tp = Calculator.TakeProfitForLong(
                                                                entryBidPrice: entryBidPrice,
                                                                risk: cmd.MarketOrder.Risk.Value,
                                                                slMultiplier: pair.SLMultiplier,
                                                                stopLossExpression: cmd.MarketOrder.StopLossExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                riskRewardRatio: cmd.MarketOrder.RiskRewardRatio.Value,
                                                                out Dictionary<string, string> logMessagesTP);

                                                            // Send to logs
                                                            LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                            // Calculate the lot size
                                                            var lotSize = Calculator.LotSize(
                                                                startBalance: startbalance,
                                                                accountBalance: api.AccountInfo.Balance,
                                                                riskPercent: pair.RiskLong,
                                                                entryBidPrice: entryBidPrice,
                                                                stopLossPrice: sl,
                                                                tickValue: metadataTick.TickValue,
                                                                tickSize: metadataTick.TickSize,
                                                                lotStep: metadataTick.LotStep,
                                                                minLotSizeAllowed: metadataTick.MinLotSize,
                                                                maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                                spread: spread,
                                                                isLong: true,
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
                                                                            if (pair.RiskMinXTimesTheSpread <= 0 || (spread * pair.RiskMinXTimesTheSpread < Math.Abs(entryBidPrice - sl)))
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
                                                                                entryBidPrice = Calculator.RoundToNearestTickSize(entryBidPrice, metadataTick.TickSize, metadataTick.Digits);
                                                                                sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                // Generate comment
                                                                                var comment = Calculator.GenerateComment(cmd.SignalID, entryBidPrice, sl, pair.StrategyID, spread);

                                                                                // Execute order
                                                                                api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, Convert.ToInt32(cmd.SignalID), comment);

                                                                                // Send to logs
                                                                                LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, Convert.ToInt32(cmd.SignalID), comment);
                                                                            }
                                                                            else
                                                                            {
                                                                                // Raise market abstention or error
                                                                                await MarketAbstentionFactory.RiskShouldBeAtLeastXTimesTheSpreadAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.RiskMinXTimesTheSpread, entryBidPrice, sl);
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
                                                            // Get the entry entryBidPrice
                                                            var entryBidPrice = Calculator.EntryBidPriceForLong(
                                                                entryExpression: cmd.PassiveOrder.EntryExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                logMessages: out Dictionary<string, string> logMessagesENTRY);

                                                            // Send to logs
                                                            LogFactory.CalculateEntryBidPrice(api.ClientId, _appConfig.Debug, cmd, logMessagesENTRY);

                                                            // Do 0.0 check
                                                            if (entryBidPrice.HasValue)
                                                            {
                                                                // Calculate SL Price
                                                                var sl = Calculator.StoplossForLong(
                                                                    entryBidPrice: entryBidPrice.Value,
                                                                    risk: cmd.PassiveOrder.Risk.Value,
                                                                    slMultiplier: pair.SLMultiplier,
                                                                    stopLossExpression: cmd.PassiveOrder.StopLossExpression,
                                                                    bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                    spread: spread,
                                                                    tickSize: metadataTick.TickSize,
                                                                    out Dictionary<string, string> logMessagesSL);

                                                                // Send to logs
                                                                LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);

                                                                // Get the Take Profit Price
                                                                var tp = Calculator.TakeProfitForLong(
                                                                    entryBidPrice: entryBidPrice.Value,
                                                                    risk: cmd.PassiveOrder.Risk.Value,
                                                                    slMultiplier: pair.SLMultiplier,
                                                                    stopLossExpression: cmd.PassiveOrder.StopLossExpression,
                                                                    bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                    spread: spread,
                                                                    riskRewardRatio: cmd.PassiveOrder.RiskRewardRatio.Value,
                                                                    out Dictionary<string, string> logMessagesTP);

                                                                // Send to logs
                                                                LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                                // Calculate the lot size
                                                                var lotSize = Calculator.LotSize(
                                                                    startBalance: startbalance,
                                                                    accountBalance: api.AccountInfo.Balance,
                                                                    riskPercent: pair.RiskLong,
                                                                    entryBidPrice: entryBidPrice.Value,
                                                                    stopLossPrice: sl,
                                                                    tickValue: metadataTick.TickValue,
                                                                    tickSize: metadataTick.TickSize,
                                                                    lotStep: metadataTick.LotStep,
                                                                    minLotSizeAllowed: metadataTick.MinLotSize,
                                                                    maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                                    spread: spread,
                                                                    isLong: true,
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
                                                                                if (pair.RiskMinXTimesTheSpread == 0 || (spread * pair.RiskMinXTimesTheSpread < Math.Abs(entryBidPrice.Value - sl)))
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
                                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask <= entryBidPrice) // Ask price, because you need to calculate the spread into account
                                                                                        orderType = OrderType.BuyLimit;
                                                                                    else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Ask >= entryBidPrice) // Ask price, because you need to calculate the spread into account
                                                                                        orderType = OrderType.Buy;

                                                                                    // Round
                                                                                    entryBidPrice = Calculator.RoundToNearestTickSize(entryBidPrice.Value, metadataTick.TickSize, metadataTick.Digits);
                                                                                    sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                    tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                    // Generate comment
                                                                                    var comment = Calculator.GenerateComment(cmd.SignalID, orderType == OrderType.Buy ? metadataTick.Bid : entryBidPrice.Value, sl, pair.StrategyID, spread);

                                                                                    // Print on the screen
                                                                                    Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / {orderType.GetDescription().ToUpper()} COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                                                    // Execute order
                                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Buy ? 0 : entryBidPrice.Value, sl, tp, Convert.ToInt32(cmd.SignalID), comment);

                                                                                    // Add to the spread monitor
                                                                                    if (pair.AdaptPassiveOrdersBeforeEntryInSeconds > 0 && !_spreadMonitors.Any(f => f.IsStarted && f.ClientId == api.ClientId && f.Magic == Convert.ToInt32(cmd.SignalID)))
                                                                                    {
                                                                                        var monitor = SpreadMonitor.InitAndStart(api.ClientId, true, pair.TickerInMetatrader, Convert.ToInt32(cmd.SignalID), pair.AdaptPassiveOrdersBeforeEntryInSeconds);
                                                                                        monitor.OnSpreadChanged += OnMonitorSpreadWarning;
                                                                                        _spreadMonitors.Add(monitor);
                                                                                    }

                                                                                    // Send to logs
                                                                                    LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Buy ? 0 : entryBidPrice.Value, sl, tp, Convert.ToInt32(cmd.SignalID), comment);
                                                                                }
                                                                                else
                                                                                {
                                                                                    // Raise market abstention or error
                                                                                    await MarketAbstentionFactory.RiskShouldBeAtLeastXTimesTheSpreadAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.RiskMinXTimesTheSpread, entryBidPrice.Value, sl);
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
                                                                    && cmd.OrderType.Equals("SELL", StringComparison.CurrentCultureIgnoreCase)
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
                                                            // InitAndStart
                                                            var entryBidPrice = metadataTick.Bid;

                                                            // Calculate SL Price
                                                            var sl = Calculator.StoplossForShort(
                                                                entryBidPrice: entryBidPrice,
                                                                risk: cmd.MarketOrder.Risk.Value,
                                                                slMultiplier: pair.SLMultiplier,
                                                                stopLossExpression: cmd.MarketOrder.StopLossExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                tickSize: metadataTick.TickSize,
                                                                out Dictionary<string, string> logMessagesSL);

                                                            // Send to logs
                                                            LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);

                                                            // Get the Take Profit Price
                                                            var tp = Calculator.TakeProfitForShort(
                                                                entryBidPrice: entryBidPrice,
                                                                risk: cmd.MarketOrder.Risk.Value,
                                                                slMultiplier: pair.SLMultiplier,
                                                                stopLossExpression: cmd.MarketOrder.StopLossExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                riskRewardRatio: cmd.MarketOrder.RiskRewardRatio.Value,
                                                                out Dictionary<string, string> logMessagesTP);

                                                            // Send to logs
                                                            LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                            // Calculate the lot size
                                                            var lotSize = Calculator.LotSize(
                                                                startBalance: startbalance,
                                                                accountBalance: api.AccountInfo.Balance,
                                                                riskPercent: pair.RiskShort,
                                                                entryBidPrice: entryBidPrice,
                                                                stopLossPrice: sl,
                                                                tickValue: metadataTick.TickValue,
                                                                tickSize: metadataTick.TickSize,
                                                                lotStep: metadataTick.LotStep,
                                                                minLotSizeAllowed: metadataTick.MinLotSize,
                                                                maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                                spread: spread,
                                                                isLong: false,
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
                                                                            if (pair.RiskMinXTimesTheSpread <= 0 || (spread * pair.RiskMinXTimesTheSpread < Math.Abs(sl - entryBidPrice)))
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
                                                                                entryBidPrice = Calculator.RoundToNearestTickSize(entryBidPrice, metadataTick.TickSize, metadataTick.Digits);
                                                                                sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                // Generate comment
                                                                                var comment = Calculator.GenerateComment(cmd.SignalID, entryBidPrice, sl, pair.StrategyID, spread);

                                                                                // Execute order
                                                                                api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, Convert.ToInt32(cmd.SignalID), comment);

                                                                                // Send to logs
                                                                                LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, Convert.ToInt32(cmd.SignalID), comment);
                                                                            }
                                                                            else
                                                                            {
                                                                                // Raise market abstention or error
                                                                                await MarketAbstentionFactory.RiskShouldBeAtLeastXTimesTheSpreadAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.RiskMinXTimesTheSpread, entryBidPrice, sl);
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
                                                            && cmd.OrderType.Equals("SELLSTOP", StringComparison.CurrentCultureIgnoreCase)
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
                                                            // Get the entry entryBidPrice
                                                            var entryBidPrice = Calculator.EntryBidPriceForShort(
                                                                entryExpression: cmd.PassiveOrder.EntryExpression,
                                                                bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                spread: spread,
                                                                logMessages: out Dictionary<string, string> logMessagesENTRY);

                                                            // Send to logs
                                                            LogFactory.CalculateEntryBidPrice(api.ClientId, _appConfig.Debug, cmd, logMessagesENTRY);

                                                            // do 0.0 check
                                                            if (entryBidPrice.HasValue)
                                                            {
                                                                // Calculate SL Price
                                                                var sl = Calculator.StoplossForShort(
                                                                    entryBidPrice: entryBidPrice.Value,
                                                                    risk: cmd.PassiveOrder.Risk.Value,
                                                                    slMultiplier: pair.SLMultiplier,
                                                                    stopLossExpression: cmd.PassiveOrder.StopLossExpression,
                                                                    bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                    spread: spread,
                                                                    tickSize: metadataTick.TickSize,
                                                                    out Dictionary<string, string> logMessagesSL);

                                                                // Send to logs
                                                                LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);

                                                                // Get the Take Profit Price
                                                                var tp = Calculator.TakeProfitForShort(
                                                                    entryBidPrice: entryBidPrice.Value,
                                                                    risk: cmd.PassiveOrder.Risk.Value,
                                                                    slMultiplier: pair.SLMultiplier,
                                                                    stopLossExpression: cmd.PassiveOrder.StopLossExpression,
                                                                    bars: api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(),
                                                                    spread: spread,
                                                                    riskRewardRatio: cmd.PassiveOrder.RiskRewardRatio.Value,
                                                                    out Dictionary<string, string> logMessagesTP);

                                                                // Send to logs
                                                                LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                                // Calculate the lot size
                                                                var lotSize = Calculator.LotSize(
                                                                    startBalance: startbalance,
                                                                    accountBalance: api.AccountInfo.Balance,
                                                                    riskPercent: pair.RiskShort,
                                                                    entryBidPrice: entryBidPrice.Value,
                                                                    stopLossPrice: sl,
                                                                    tickValue: metadataTick.TickValue,
                                                                    tickSize: metadataTick.TickSize,
                                                                    lotStep: metadataTick.LotStep,
                                                                    minLotSizeAllowed: metadataTick.MinLotSize,
                                                                    maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                                    spread: spread,
                                                                    isLong: false,
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
                                                                                if (pair.RiskMinXTimesTheSpread == 0 || (spread * pair.RiskMinXTimesTheSpread < Math.Abs(sl - entryBidPrice.Value)))
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
                                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Bid < entryBidPrice.Value)
                                                                                        orderType = OrderType.SellLimit;
                                                                                    else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Bid <= entryBidPrice.Value)
                                                                                        orderType = OrderType.Sell;

                                                                                    // Round
                                                                                    entryBidPrice = Calculator.RoundToNearestTickSize(entryBidPrice.Value, metadataTick.TickSize, metadataTick.Digits);
                                                                                    sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                    tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                    // Generate comment
                                                                                    var comment = Calculator.GenerateComment(cmd.SignalID, entryBidPrice.Value, sl, pair.StrategyID, spread);

                                                                                    // Print on the screen
                                                                                    Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / {orderType.GetDescription().ToUpper()} COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                                                    // Execute order
                                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Sell ? 0 : entryBidPrice.Value, sl, tp, (int)cmd.SignalID, comment);

                                                                                    // Add to the spread monitor
                                                                                    if (pair.AdaptPassiveOrdersBeforeEntryInSeconds > 0 && !_spreadMonitors.Any(f => f.IsStarted && f.ClientId == api.ClientId && f.Magic == Convert.ToInt32(cmd.SignalID)))
                                                                                    {
                                                                                        var monitor = SpreadMonitor.InitAndStart(api.ClientId, false, pair.TickerInMetatrader, Convert.ToInt32(cmd.SignalID), pair.AdaptPassiveOrdersBeforeEntryInSeconds);
                                                                                        monitor.OnSpreadChanged += OnMonitorSpreadWarning;
                                                                                        _spreadMonitors.Add(monitor);
                                                                                    }

                                                                                    // Send to logs
                                                                                    LogFactory.ExecuteOrderCommand(api.ClientId, _appConfig.Debug, cmd, pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Sell ? 0 : entryBidPrice.Value, sl, tp, (int)cmd.SignalID, comment);
                                                                                }
                                                                                else
                                                                                {
                                                                                    // Raise market abstention or error
                                                                                    await MarketAbstentionFactory.RiskShouldBeAtLeastXTimesTheSpreadAsync(api.ClientId, _appConfig.Debug, cmd, cmd.SignalID, pair.RiskMinXTimesTheSpread, entryBidPrice.Value, sl);
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
                                                else if (cmd.OrderType.Equals("MOVESLTOBE", StringComparison.CurrentCultureIgnoreCase) && cmd.SignalID > 0)
                                                {
                                                    // Check if the ticket still exist as open order
                                                    var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == cmd.SignalID);

                                                    // Null reference check
                                                    if (ticketId.Key > 0 && ticketId.Value.Type != null)
                                                    {
                                                        // InitAndStart variable
                                                        var sl = 0.0M;

                                                        if (ticketId.Value.Type.Equals("SELL", StringComparison.CurrentCultureIgnoreCase))
                                                        {
                                                            // Calculate SL Price
                                                            sl = Calculator.StoplossToBreakEvenForShort(
                                                                entryBidPrice: ticketId.Value.OpenPrice,
                                                                currentBidPrice: metadataTick.Bid,
                                                                spread: spread,
                                                                tickSize: metadataTick.TickSize,
                                                                out Dictionary<string, string> logMessagesSL);

                                                            // Send to logs
                                                            LogFactory.CalculateStoploss(api.ClientId, _appConfig.Debug, cmd, logMessagesSL);
                                                        }
                                                        else
                                                        {
                                                            // Calculate SL Price
                                                            sl = Calculator.StoplossToBreakEvenForLong(
                                                                entryBidPrice: ticketId.Value.OpenPrice,
                                                                currentBidPrice: metadataTick.Bid,
                                                                spread: spread,
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
                                                else if ((cmd.OrderType.Equals("CLOSE", StringComparison.CurrentCultureIgnoreCase) || cmd.OrderType.Equals("CANCEL", StringComparison.CurrentCultureIgnoreCase)) && cmd.SignalID > 0)
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
                                                else if (cmd.OrderType.Equals("CLOSEALL", StringComparison.CurrentCultureIgnoreCase))
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
                                                // InitAndStart entryBidPrice
                                                var entryBidPrice = metadataTick.Bid;

                                                // Get the Stop Loss entryBidPrice
                                                var sl = Convert.ToDecimal(cmd.MarketOrder.StopLossPrice);

                                                // Generate risk
                                                var risk = entryBidPrice - Convert.ToDecimal(cmd.MarketOrder.StopLossPrice);

                                                // Get the Take Profit Price
                                                var tp = Calculator.TakeProfitForLong(
                                                    entryBidPrice: entryBidPrice,
                                                    risk: risk,
                                                    slMultiplier: 1,
                                                    stopLossExpression: null,
                                                    bars: [],
                                                    spread: spread,
                                                    riskRewardRatio: Convert.ToDecimal(cmd.MarketOrder.RiskRewardRatio),
                                                    out Dictionary<string, string> logMessagesTP);

                                                // Send to logs
                                                LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                // Calculate the lot size
                                                var lotSize = Calculator.LotSize(
                                                    startBalance: startbalance,
                                                    accountBalance: api.AccountInfo.Balance,
                                                    riskPercent: Convert.ToDecimal(cmd.ProcentRiskOfBalance),
                                                    entryBidPrice: entryBidPrice,
                                                    stopLossPrice: sl,
                                                    tickValue: metadataTick.TickValue,
                                                    tickSize: metadataTick.TickSize,
                                                    lotStep: metadataTick.LotStep,
                                                    minLotSizeAllowed: metadataTick.MinLotSize,
                                                    maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                    spread: spread,
                                                    isLong: true,
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
                                                        entryBidPrice = Calculator.RoundToNearestTickSize(entryBidPrice, metadataTick.TickSize, metadataTick.Digits);
                                                        sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                        tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                        // Generate comment
                                                        var comment = Calculator.GenerateComment(cmd.Magic, entryBidPrice, sl, cmd.StrategyID, spread);

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
                                                // InitAndStart entryBidPrice
                                                var entryBidPrice = metadataTick.Bid;

                                                // Get the Stop Loss entryBidPrice
                                                var sl = Convert.ToDecimal(cmd.MarketOrder.StopLossPrice);

                                                // Generate risk
                                                var risk = Math.Abs(Convert.ToDecimal(cmd.MarketOrder.StopLossPrice) - entryBidPrice);

                                                // Get the Take Profit Price
                                                var tp = Calculator.TakeProfitForShort(
                                                    entryBidPrice: entryBidPrice,
                                                    risk: risk,
                                                    slMultiplier: 1,
                                                    stopLossExpression: null,
                                                    bars: [],
                                                    spread: spread,
                                                    riskRewardRatio: Convert.ToDecimal(cmd.MarketOrder.RiskRewardRatio),
                                                    out Dictionary<string, string> logMessagesTP);

                                                // Send to logs
                                                LogFactory.CalculateTakeProfit(api.ClientId, _appConfig.Debug, cmd, logMessagesTP);

                                                // Calculate the lot size
                                                var lotSize = Calculator.LotSize(
                                                    startBalance: startbalance,
                                                    accountBalance: api.AccountInfo.Balance,
                                                    riskPercent: Convert.ToDecimal(cmd.ProcentRiskOfBalance),
                                                    entryBidPrice: entryBidPrice,
                                                    stopLossPrice: sl,
                                                    tickValue: metadataTick.TickValue,
                                                    tickSize: metadataTick.TickSize,
                                                    lotStep: metadataTick.LotStep,
                                                    minLotSizeAllowed: metadataTick.MinLotSize,
                                                    maxLotSizeAllowed: metadataTick.MaxLotSize,
                                                    spread: spread,
                                                    isLong: false,
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
                                                        entryBidPrice = Calculator.RoundToNearestTickSize(entryBidPrice, metadataTick.TickSize, metadataTick.Digits);
                                                        sl = Calculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                        tp = Calculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                        // Generate comment
                                                        var comment = Calculator.GenerateComment(cmd.Magic, entryBidPrice, sl, cmd.StrategyID, spread);

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

        private void OnMonitorSpreadWarning(long clientId, bool isLong, string instrument, int magic)
        {
            // Do null reference check
            if (_appConfig != null && _apis.Count(f => f.ClientId == clientId) == 1)
            {
                // Check if the monitor is started
                if (_spreadMonitors.Any(f => f.IsStarted && f.ClientId == clientId && f.Instrument.Equals(instrument, StringComparison.CurrentCultureIgnoreCase) && f.Magic == magic))
                {
                    // Get the api
                    var api = _apis.First(f => f.ClientId == clientId);

                    // Get the order from the api
                    var order = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == magic && f.Value.Symbol == instrument);

                    // Get the metadata
                    var metadata = api.MarketData.FirstOrDefault(f => f.Key.Equals(instrument, StringComparison.CurrentCultureIgnoreCase));

                    // Get the pair
                    var broker = _appConfig.Brokers.Where(f => f.ClientId == clientId).FirstOrDefault();

                    // startbalance
                    var startbalance = _appConfig.Brokers.First(f => f.ClientId == api.ClientId)?.StartBalance;

                    // Get dynamic risk
                    var dynRisk = Calculator.GetDynamicRisk(_appConfig, api.ClientId);

                    // Do null reference check
                    if (order.Key > 0 && order.Value != null && !string.IsNullOrEmpty(metadata.Key) && metadata.Value != null && broker != null && api.AccountInfo != null && startbalance.HasValue)
                    {
                        // Get extra information from the comments
                        var strategyId = Calculator.GetStrategyIdFromComment(order.Value.Comment);
                        var entryPrice = Calculator.GetEntryPriceFromComment(order.Value.Comment);
                        var entrySpread = Calculator.GetSpreadFromComment(order.Value.Comment);

                        // Do null reference check
                        if (entryPrice.HasValue && entrySpread.HasValue && strategyId.HasValue)
                        {
                            // Calculate the spread
                            var currentSpread = Calculator.CalculateSpread(metadata.Value.Ask, metadata.Value.Bid, metadata.Value.TickSize, metadata.Value.Digits);

                            // Get the right pair back from the local database
                            var pair = Calculator.GetPairByTradingviewInstrument(_appConfig, clientId, instrument, strategyId.Value);

                            // Do null reference check
                            if (pair != null)
                            {
                                // Execute the order (this code only applies on LONG's. In case of a SHORT position, the entry is taken on the BID price, and will not change if the spread is changing.
                                if (isLong == true)
                                {
                                    // Calculate initial entry price (in case of LONG, the entry price is the ASK price, so we should substract the spread from the entry price to get the right BID price)
                                    var initialEntryBidPrice = entryPrice.Value - entrySpread.Value;

                                    // Calculate new entry price
                                    var newEntryPrice = initialEntryBidPrice + currentSpread;

                                    // Print on the screen
                                    Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {instrument} / AUTO MONITOR : ADAPT ORDER / {magic} / {strategyId}");

                                    // Calculate the lot size
                                    var lotSize = Calculator.LotSize(
                                        startBalance: startbalance.Value,
                                        accountBalance: api.AccountInfo.Balance,
                                        riskPercent: pair.RiskLong,
                                        entryBidPrice: newEntryPrice,
                                        stopLossPrice: order.Value.StopLoss,
                                        tickValue: metadata.Value.TickValue,
                                        tickSize: metadata.Value.TickSize,
                                        lotStep: metadata.Value.LotStep,
                                        minLotSizeAllowed: metadata.Value.MinLotSize,
                                        maxLotSizeAllowed: metadata.Value.MaxLotSize,
                                        spread: currentSpread,
                                        isLong: true,
                                        out Dictionary<string, string> logMessagesLOT,
                                        riskData: dynRisk);

                                    // Send to logs
                                    LogFactory.CalculateLotSize(api.ClientId, _appConfig.Debug, order.Value, logMessagesLOT);

                                    // Execute order
                                    api.ModifyOrder(order.Key, lotSize, newEntryPrice, order.Value.StopLoss, order.Value.TakeProfit, order.Value.Magic);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void CheckIfEverythingIsLoadedCorrectly()
        {
            // Do null reference checks
            if (_appConfig != null && _apis != null)
            {
                // Check the brokers configured in the settings
                foreach (var broker in _appConfig.Brokers.Where(f => f.IsEnable))
                {
                    // Get client from the api
                    var client = _apis.FirstOrDefault(f => f.ClientId == broker.ClientId);

                    if (client != null)
                    {
                        if (client.AccountInfo == null)
                        {
                            // Print on the screen
                            Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == client.ClientId).Name} / ACCOUNT INFO DOESN'T EXIST", true);
                        }

                        if (client.HistoricBarData != null)
                        {
                            // Foreach pair of the broker, there should be historic bar data
                            foreach (var pair in broker.Pairs)
                            {
                                if (!client.HistoricBarData.ContainsKey(pair.TickerInMetatrader))
                                {
                                    // Print on the screen
                                    Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == client.ClientId).Name} / {pair.TickerInMetatrader} / NO HISTORIC BARDATA AVAILABLE", true);
                                }
                            }
                        }
                        else
                        {
                            // Print on the screen
                            Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == client.ClientId).Name} / NO HISTORIC BARDATA AVAILABLE FOR ALL PAIRS", true);
                        }

                        if (client.LastBarData != null)
                        {
                            // Foreach pair of the broker, there should be historic bar data
                            foreach (var pair in broker.Pairs)
                            {
                                if (!client.LastBarData.ContainsKey(pair.TickerInMetatrader))
                                {
                                    // Print on the screen
                                    Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == client.ClientId).Name} / {pair.TickerInMetatrader} / NO LAST BARDATA AVAILABLE", true);
                                }
                            }
                        }
                        else
                        {
                            // Print on the screen
                            Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == client.ClientId).Name} / NO LAST BARDATA AVAILABLE FOR ALL PAIRS", true);
                        }

                        if (client.MarketData != null)
                        {
                            // Foreach pair of the broker, there should be historic bar data
                            foreach (var pair in broker.Pairs)
                            {
                                if (!client.MarketData.ContainsKey(pair.TickerInMetatrader))
                                {
                                    // Print on the screen
                                    Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == client.ClientId).Name} / {pair.TickerInMetatrader} / NO MARKETDATA AVAILABLE", true);
                                }
                            }
                        }
                        else
                        {
                            // Print on the screen
                            Helpers.Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == client.ClientId).Name} / NO MARKET DATA FOR ALL PAIRS", true);
                        }
                    }
                    else
                    {
                        // Print on the screen
                        Helpers.Print($"ERROR : {DateTime.UtcNow} / {broker.Name} / DOESN'T EXIST IN METATRADER", true);
                    }
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
                        var pair = Calculator.GetPairByMetatraderInstrument(_appConfig, clientId, order.Value.Symbol, strategyID.HasValue ? strategyID.Value : 0);

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
                                                entryBidPrice: order.Value.OpenPrice,
                                                currentBidPrice: metadataTick.Value.Bid,
                                                spread: Calculator.CalculateSpread(metadataTick.Value.Ask, metadataTick.Value.Bid, metadataTick.Value.TickSize, metadataTick.Value.Digits),
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
                                                entryBidPrice: order.Value.OpenPrice,
                                                currentBidPrice: metadataTick.Value.Bid,
                                                spread: Calculator.CalculateSpread(metadataTick.Value.Ask, metadataTick.Value.Bid, metadataTick.Value.TickSize, metadataTick.Value.Digits),
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

        private void OnTickEvent(long clientId, string symbol, decimal ask, decimal bid, decimal tickSize, int digits)
        {
            foreach (var api in _apis)
            {

            }

            // Update the spread in the monitor
            foreach (var monitor in _spreadMonitors.Where(f => f.IsStarted && f.ClientId == clientId && f.Instrument.Equals(symbol, StringComparison.CurrentCultureIgnoreCase)))
            {
                monitor.UpdateSpread(Calculator.CalculateSpread(ask, bid, tickSize, digits));
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
                var entryPrice = Calculator.GetEntryPriceFromComment(order.Comment);
                var stoploss = Calculator.GetStoplossFromComment(order.Comment);
                var strategyID = Calculator.GetStrategyIdFromComment(order.Comment);

                // Print on the screen
                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {order.Symbol} / CLOSED ORDER EVENT / {order.Magic} / {strategyID}");

                // Do null reference check
                if (entryPrice.HasValue && stoploss.HasValue)
                {
                    // Calculate the risk to reward
                    var rrr = Calculator.CalculateRiskReward(!string.IsNullOrEmpty(order.Type) && order.Type.Contains("buy", StringComparison.CurrentCultureIgnoreCase), entryPrice.Value, stoploss.Value, marketdata.Bid);

                    // Send to logs
                    LogFactory.ClosedAnOrderEvent(clientId, _appConfig.Debug, order, ticketId, marketdata.Bid, order.Magic, rrr);
                }
            }
        }

        private void OnDealCreatedEvent(long clientId, long tradeId, Deal deal)
        {
            // Do null reference check
            if (_appConfig != null && _apis.Count(f => f.ClientId == clientId) == 1)
            {
                // Get api
                var api = _apis.First(f => f.ClientId == clientId);

                // Do null reference check
                if (deal.Entry.Contains("entry_in") && !string.IsNullOrEmpty(deal.Comment))
                {
                    // Get strategy id
                    var strategyId = Calculator.GetStrategyIdFromComment(deal.Comment);

                    // Do null reference check
                    if (strategyId.HasValue)
                    {
                        // Get the right pair back from the local database
                        var pair = Calculator.GetPairByMetatraderInstrument(_appConfig, clientId, deal.Symbol, strategyId.Value);

                        // Do null reference check
                        if (pair != null)
                        {
                            // Start script close trade within x bars
                            if (pair.CloseTradeWithinXBars.HasValue)
                            {
                                var scheduler = new RecurringCloseTradeScheduler(clientId, pair.TickerInMetatrader, strategyId.Value, true);
                                scheduler.OnCloseTradeEvent += OnItsTimeToCloseTradeEvent;
                                var targetTime = DateTime.UtcNow.TimeOfDay.Add(pair.TimeframeAsTimespan * (pair.CloseTradeWithinXBars.Value + 1));
                                scheduler.Start(targetTime);
                                _recurringCloseTradeScheduler.Add(scheduler);

                                // Print on screen
                                Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {deal.Symbol} / CLOSE TRADE @ {targetTime} / {deal.Magic}");
                            }

                            // Stop the monitor
                            if (pair.AdaptPassiveOrdersBeforeEntryInSeconds > 0)
                            {
                                // Get the monitor from the list
                                var monitor = _spreadMonitors.FirstOrDefault(f => f.IsStarted && f.ClientId == clientId && f.Instrument.Equals(deal.Symbol, StringComparison.CurrentCultureIgnoreCase) && f.Magic == deal.Magic);

                                // Do null reference check
                                if (monitor != null)
                                {
                                    // Stop the monitor
                                    monitor.Stop();

                                    // Remove the monitor from the list
                                    _spreadMonitors.Remove(monitor);

                                    // Print on screen
                                    Helpers.Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {deal.Symbol} / STOP THE SPREAD MONITOR / {deal.Magic}");
                                }
                            }
                        }
                    }
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
