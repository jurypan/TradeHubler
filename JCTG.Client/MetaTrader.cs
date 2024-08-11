﻿using JCTG.Events;
using JCTG.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using static JCTG.Client.Helpers;

namespace JCTG.Client
{
    public class Metatrader : IDisposable
    {

        private TerminalConfig? _appConfig;
        private readonly List<MetatraderApi> _apis;
        private readonly List<CloseTradeScheduler> _schedulers;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // Initial count 1, maximum count 1
        private readonly ConcurrentDictionary<long, List<Log>> _buffers = new();
        private readonly Timer _timer;

        public Metatrader(TerminalConfig terminalConfig)
        {
            // Init APP Config + API
            _appConfig = terminalConfig;
            _apis = [];
            _schedulers = new List<CloseTradeScheduler>();
            _timer = new Timer(async _ => await FlushLogsToFileAsync(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

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
                Print($"WARNING : {DateTime.UtcNow} / UPDATED CONFIGURATION");

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
                        // Load logs into memory
                        await LoadLogFromFileAsync(_api.ClientId);

                        // Init the events
                        _api.OnOrderCreateEvent += OnOrderCreateEvent;
                        _api.OnOrderUpdateEvent += OnOrderUpdateEvent;
                        _api.OnOrderCloseEvent += OnOrderCloseEvent;
                        _api.OnLogEvent += OnLogEvent;
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
                            var timingCloseAllTradesAt = new CloseTradeScheduler(_api.ClientId, pair.TickerInMetatrader, pair.StrategyID, false);
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
                        _api.OnOrderCreateEvent -= OnOrderCreateEvent;
                        _api.OnOrderUpdateEvent -= OnOrderUpdateEvent;
                        _api.OnOrderCloseEvent -= OnOrderCloseEvent;
                        _api.OnLogEvent -= OnLogEvent;
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
                    azureQueue.OnSendTradingviewSignalCommand += async (cmd) =>
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
                                Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / NEW SIGNAL / {cmd.SignalID}");

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
                                                var spread = RiskCalculator.CalculateSpread(metadataTick.Ask, metadataTick.Bid, metadataTick.TickSize, metadataTick.Digits);

                                                // BUY
                                                if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                            && cmd.OrderType == "BUY"
                                                            && cmd.MarketOrder != null
                                                            && cmd.MarketOrder.Risk.HasValue
                                                            && cmd.MarketOrder.RiskRewardRatio.HasValue
                                                )
                                                {
                                                    // Do correlation check
                                                    if (CorrelatedPairs.IsNotCorrelated(pair.TickerInMetatrader, "BUY", pair.CorrelatedPairs, api.OpenOrders))
                                                    {
                                                        // Do do not open a deal x minutes before close
                                                        if (CloseTradeScheduler.CanOpenTrade(pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose))
                                                        {
                                                            // Get the Stop Loss price
                                                            var sl = metadataTick.Ask - (cmd.MarketOrder.Risk.Value * Convert.ToDecimal(pair.SLMultiplier));

                                                            // Add the spread options to the SL
                                                            if (pair.SpreadSL.HasValue)
                                                            {
                                                                // Calculate SL
                                                                sl = RiskCalculator.CalculateSpreadExecForLong(sl, spread, pair.SpreadSL.Value);
                                                            }

                                                            // Get the Take Profit Price
                                                            var tp = metadataTick.Ask + (cmd.MarketOrder.Risk.Value * cmd.MarketOrder.RiskRewardRatio.Value);

                                                            // Add the spread options to th TP
                                                            if (pair.SpreadTP.HasValue)
                                                            {
                                                                // Calculate TP
                                                                tp = RiskCalculator.CalculateSpreadExecForLong(tp, spread, pair.SpreadTP.Value);
                                                            }

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"StopLossPriceCalculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                                var description = string.Format($"StopLoss || Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                            }

                                                            // Calculate the lot size
                                                            var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.RiskLong, metadataTick.Ask, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, out Dictionary<string, string> logMessages, dynRisk);

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"LotSizeCalculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                                var description = string.Format($"LotSize || {string.Join(", ", logMessages.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                            }

                                                            // do 0.0 lotsize check
                                                            if (lotSize > 0.0M)
                                                            {
                                                                // Do 0.0 SL check
                                                                if (sl > 0.0M && tp > 0.0M)
                                                                {
                                                                    // Do lot size check
                                                                    if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                                    {
                                                                        // Do check if risk is x times the spread
                                                                        if (spread * pair.RiskMinXTimesTheSpread < metadataTick.Ask - sl)
                                                                        {
                                                                            // Send to logs
                                                                            if (_appConfig.Debug)
                                                                            {
                                                                                var message = string.Format($"TakeProfitCalculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                                                var description = string.Format($"TakeProfit || Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits}");
                                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                            }

                                                                            // Print on the screen
                                                                            Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / BUY COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                                            // Cancel open buy or limit orders
                                                                            if (pair.CancelStopOrLimitOrderWhenNewSignal)
                                                                            {
                                                                                foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol == pair.TickerInMetatrader
                                                                                                                                && f.Value.Type != null
                                                                                                                                && (f.Value.Type.Equals("buystop") || f.Value.Type.Equals("buylimit"))
                                                                                                                                ))
                                                                                {
                                                                                    // Close the order
                                                                                    api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                                                    // Send to logs
                                                                                    if (_appConfig.Debug)
                                                                                    {
                                                                                        var message = string.Format($"CancelledPassiveOrder || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                        var description = string.Format($"CloseOrder || Symbol={pair.TickerInMetatrader}");
                                                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                                    }
                                                                                }
                                                                            }

                                                                            // Open order
                                                                            var comment = string.Format($"{cmd.SignalID}/{Math.Round(metadataTick.Ask, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyID}/{spread}");
                                                                            var orderType = OrderType.Buy;

                                                                            // Round
                                                                            sl = RiskCalculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                            tp = RiskCalculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                            // Execute order
                                                                            api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, (int)cmd.SignalID, comment);

                                                                            // Send to logs
                                                                            if (_appConfig.Debug)
                                                                            {
                                                                                var message = string.Format($"MetatraderOrderExecuted || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                                                var description = string.Format($"ExecuteOrder || Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price=,SL={sl},TP={tp},SignalID={cmd.SignalID},Comment={comment}");
                                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            // Raise market abstention or error
                                                                            var message = string.Format($"Error || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                                            var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"The risk {metadataTick.Ask - sl} should be at least {pair.RiskMinXTimesTheSpread} times the spread : {spread}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                            await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.RiskShouldBeAtLeastXTimesTheSpread, log);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        // Raise market abstention or error
                                                                        var message = string.Format($"Error || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                                        var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the stop loss price", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                        await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.AmountOfLotSizeShouldBeSmallerThenMaxLotsize, log);
                                                                    }
                                                                }

                                                            }
                                                            else
                                                            {
                                                                // Raise market abstention or error
                                                                var message = string.Format($"LotSizeError || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                                var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation the lot size", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingLotSize, log);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // Raise market abstention or error
                                                            var message = string.Format($"CanNotOpenTradeDueClosingTime || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                            var log = new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = $"Can't open deal {pair.TickerInMetatrader} because market will be closed within {pair.DoNotOpenTradeXMinutesBeforeClose} minutes. It's now {DateTime.UtcNow} UTC time.", Magic = Convert.ToInt32(cmd.SignalID) };
                                                            await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.MarketWillBeClosedWithinXMinutes, log);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Raise market abstention or error
                                                        var message = string.Format($"CorrelatedPairsFound || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                        var log = new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = $"Correlated pair found in the open orders : {CorrelatedPairs.GetCorrelatedPair(pair.TickerInMetatrader, "BUY", pair.CorrelatedPairs, api.OpenOrders)}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                        await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.CorrelatedPairFound, log);
                                                    }
                                                }

                                                // BUY STOP
                                                else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                            && cmd.OrderType == "BUYSTOP"
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
                                                        if (CloseTradeScheduler.CanOpenTrade(pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose))
                                                        {
                                                            // Get the entry price
                                                            var price = DynamicEvaluator.EvaluateExpression(cmd.PassiveOrder.EntryExpression, api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(), out Dictionary<string, string> logMessages1);
                                                            var orgPrice = price;

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"EntryExpression || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                var description = string.Format($"Entry || {string.Join(", ", logMessages1.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                            }

                                                            // Do 0.0 check
                                                            if (price.HasValue)
                                                            {
                                                                // Add the spread options
                                                                if (pair.SpreadEntry.HasValue)
                                                                {
                                                                    // Calculate price
                                                                    price = RiskCalculator.CalculateSpreadExecForLong(price.Value, spread, pair.SpreadEntry.Value);
                                                                }

                                                                // Get the Stop Loss price
                                                                var sl = price.Value - (cmd.PassiveOrder.Risk.Value * Convert.ToDecimal(pair.SLMultiplier));
                                                                var slOrg = sl;

                                                                // If SL expression is enabled
                                                                if (!string.IsNullOrEmpty(cmd.PassiveOrder.StopLossExpression))
                                                                {
                                                                    // Get the SL price
                                                                    var slExpr = DynamicEvaluator.EvaluateExpression(cmd.PassiveOrder.StopLossExpression, api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(), out Dictionary<string, string> logMessages2);

                                                                    // Send to logs
                                                                    if (_appConfig.Debug)
                                                                    {
                                                                        var message = string.Format($"StopLossExpression || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                        var description = string.Format($"StopLoss || {string.Join(", ", logMessages2.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                    }

                                                                    // Do null reference check
                                                                    if (slExpr.HasValue)
                                                                    {
                                                                        // Get value
                                                                        sl = slExpr.Value;
                                                                        slOrg = slExpr.Value;

                                                                        // Add SL Multiplier (Price + (risk * SlMultiplier)
                                                                        sl = price.Value - (Math.Abs(price.Value - sl) * Convert.ToDecimal(pair.SLMultiplier));
                                                                    }
                                                                }

                                                                // Add the spread options
                                                                if (pair.SpreadSL.HasValue)
                                                                {
                                                                    // Calculate SP
                                                                    sl = RiskCalculator.CalculateSpreadExecForLong(sl, spread, pair.SpreadSL.Value);
                                                                }

                                                                // Get the Take Profit Price
                                                                var tp = price.Value + (cmd.PassiveOrder.Risk.Value * cmd.PassiveOrder.RiskRewardRatio.Value);

                                                                // Add the spread options
                                                                if (pair.SpreadTP.HasValue)
                                                                {
                                                                    // Calculate TP
                                                                    tp = RiskCalculator.CalculateSpreadExecForLong(tp, spread, pair.SpreadTP.Value);
                                                                }

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"Order Calculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},SlExpr={cmd.PassiveOrder.StopLossExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                    var description = string.Format($"EntryPrice || Price={orgPrice},Spread={spread},EntryPrice={price},EntryExpression={cmd.PassiveOrder.EntryExpression}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);

                                                                    description = string.Format($"StopLoss || StopLoss={slOrg},Spread={spread},EntryStopLoss={sl},RiskLong={cmd.PassiveOrder.Risk.Value},SLMultiplier={pair.SLMultiplier},StopLossExpression={cmd.PassiveOrder.StopLossExpression}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);

                                                                    description = string.Format($"TakeProfit ||  Spread={spread},EntryTakeProfit={tp},RiskLong={cmd.PassiveOrder.Risk.Value},RiskRewardRatio={cmd.PassiveOrder.RiskRewardRatio.Value}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                }

                                                                // Calculate the lot size
                                                                var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.RiskLong, price.Value, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, out Dictionary<string, string> logMessages3, dynRisk); ;

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"LotSizeCalculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                    var description = string.Format($"LotSize || {string.Join(", ", logMessages3.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                }

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
                                                                                if (pair.RiskMinXTimesTheSpread == 0 || spread * pair.RiskMinXTimesTheSpread < cmd.PassiveOrder.Risk.Value)
                                                                                {
                                                                                    // Print on the screen
                                                                                    if (metadataTick.Ask > price)
                                                                                        Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / BUY LIMIT COMMAND / {cmd.SignalID} / {cmd.StrategyID}");
                                                                                    else
                                                                                        Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / BUY STOP COMMAND / {cmd.SignalID} / {cmd.StrategyID}");


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
                                                                                                var message = string.Format($"CancelledPassiveOrder || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                                var description = string.Format($"CloseOrder || Symbol={pair.TickerInMetatrader}");
                                                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                                            }
                                                                                        }
                                                                                    }

                                                                                    // Open order
                                                                                    var comment = string.Format($"{cmd.SignalID}/{price}/{sl}/{(int)pair.StrategyID}/{spread}");
                                                                                    var orderType = OrderType.BuyStop;
                                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Bid <= price)
                                                                                        orderType = OrderType.BuyLimit;
                                                                                    else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Bid >= price)
                                                                                        orderType = OrderType.Buy;

                                                                                    // Round
                                                                                    price = RiskCalculator.RoundToNearestTickSize(price.Value, metadataTick.TickSize, metadataTick.Digits);
                                                                                    sl = RiskCalculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                    tp = RiskCalculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                    // Execute order
                                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Buy ? 0 : price.Value, sl, tp, (int)cmd.SignalID, comment);

                                                                                    // Send to logs
                                                                                    if (_appConfig.Debug)
                                                                                    {
                                                                                        var message = string.Format($"MetatraderOrderCreated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                        var description = string.Format($"ExecuteOrder || Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price={price},SL={sl},TP={tp},SignalID={cmd.SignalID},Comment={comment}");
                                                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                                    }
                                                                                }
                                                                                else
                                                                                {
                                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                    var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"The risk {cmd.PassiveOrder.Risk.Value} should be at least {pair.RiskMinXTimesTheSpread} times the spread : {spread}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                                    await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.RiskShouldBeAtLeastXTimesTheSpread, log);
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Max lot size {lotSize} exceeded : {pair.MaxLotSize}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                                await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.AmountOfLotSizeShouldBeSmallerThenMaxLotsize, log);
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                            var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the take profit price", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                            await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingTakeProfitPrice, log);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                        var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the stop loss price", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                        await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingStopLossPrice, log);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                    var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the lot size", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                    await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingLotSize, log);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Can't find entry candle {cmd.PassiveOrder.EntryExpression} with date : {DynamicEvaluator.GetDateFromBarString(cmd.PassiveOrder.EntryExpression)}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingEntryPrice, log);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                            var log = new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = $"Can't open deal {pair.TickerInMetatrader} because market will be closed within {pair.DoNotOpenTradeXMinutesBeforeClose} minutes. It's now {DateTime.UtcNow} UTC time.", Magic = Convert.ToInt32(cmd.SignalID) };
                                                            await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.MarketWillBeClosedWithinXMinutes, log);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                        var log = new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = $"Correlated pair found in the open orders : {CorrelatedPairs.GetCorrelatedPair(pair.TickerInMetatrader, "BUY", pair.CorrelatedPairs, api.OpenOrders)}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                        await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.CorrelatedPairFound, log);
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
                                                        if (CloseTradeScheduler.CanOpenTrade(pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose))
                                                        {
                                                            var price = metadataTick.Bid;

                                                            // Get the Stop Loss price
                                                            var sl = price + (cmd.MarketOrder.Risk.Value * Convert.ToDecimal(pair.SLMultiplier));

                                                            // Add the spread options
                                                            if (pair.SpreadSL.HasValue)
                                                            {
                                                                // Calculate SL
                                                                sl = RiskCalculator.CalculateSpreadExecForShort(sl, spread, pair.SpreadSL.Value);
                                                            }

                                                            // Get the Take Profit Price
                                                            var tp = price - (cmd.MarketOrder.Risk.Value * cmd.MarketOrder.RiskRewardRatio.Value);
                                                            if (pair.SpreadTP.HasValue)
                                                            {
                                                                // Calculate TP
                                                                tp = RiskCalculator.CalculateSpreadExecForShort(tp, spread, pair.SpreadTP.Value);
                                                            }

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"StopLossCalculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={price},TP={tp},SL={sl}");
                                                                var description = string.Format($"SLForShort: Bid={metadataTick.Bid},Spread={spread},Digits={metadataTick.Digits}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                            }

                                                            // Calculate the lot size
                                                            var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.RiskShort, price, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, out Dictionary<string, string> logMessages, dynRisk);

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"LotSizeCalculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={price},TP={tp},SL={sl}");
                                                                var description = string.Format($"LotSize || {string.Join(", ", logMessages.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                            }

                                                            // do 0.0 check
                                                            if (lotSize > 0.0M)
                                                            {
                                                                // Do 0.0 check
                                                                if (sl > 0.0M && tp > 0.0M)
                                                                {
                                                                    // Do lot size check
                                                                    if (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize)
                                                                    {
                                                                        // Do check if risk is x times the spread
                                                                        if (pair.RiskMinXTimesTheSpread <= 0 || (spread * pair.RiskMinXTimesTheSpread < sl - price))
                                                                        {
                                                                            // Send to logs
                                                                            if (_appConfig.Debug)
                                                                            {
                                                                                var message = string.Format($"TakeProfitCalculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={price},TP={tp},SL={sl}");
                                                                                var description = string.Format($"TPForShort: Bid={metadataTick.Bid},Spread={spread},Digits={metadataTick.Digits}");
                                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                            }

                                                                            // Print on the screen
                                                                            Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / SELL COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                                            // Cancel open buy or limit orders
                                                                            if (pair.CancelStopOrLimitOrderWhenNewSignal)
                                                                            {
                                                                                foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol == pair.TickerInMetatrader
                                                                                                                                && f.Value.Type != null
                                                                                                                                && (f.Value.Type.Equals("sellstop") || f.Value.Type.Equals("selllimit"))
                                                                                                                                ))
                                                                                {
                                                                                    // Close the order
                                                                                    api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                                                    // Send to logs
                                                                                    if (_appConfig.Debug)
                                                                                    {
                                                                                        var message = string.Format($"CancelledPassiveOrder || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                        var description = string.Format($"CancelStopOrLimitOrderWhenNewSignal: Symbol={pair.TickerInMetatrader}");
                                                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                                    }
                                                                                }
                                                                            }

                                                                            // Open order
                                                                            var comment = string.Format($"{cmd.SignalID}/{Math.Round(price, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyID}/{spread}");

                                                                            //  Active
                                                                            var orderType = OrderType.Sell;

                                                                            // Round
                                                                            sl = RiskCalculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                            tp = RiskCalculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                            // Execute order
                                                                            api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, 0, sl, tp, (int)cmd.SignalID, comment);

                                                                            // Send to logs
                                                                            if (_appConfig.Debug)
                                                                            {
                                                                                var message = string.Format($"MetatraderOrderCreated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={price},TP={tp},SL={sl}");
                                                                                var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price=,SL={sl},TP={tp},SignalID={cmd.SignalID},Comment={comment}");
                                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={price},TP={tp},SL={sl}");
                                                                            var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"The risk {sl - price} should be at least {pair.RiskMinXTimesTheSpread} times the spread : {spread}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                            await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.RiskShouldBeAtLeastXTimesTheSpread, log);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={price},TP={tp},SL={sl}");
                                                                        var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Max lot size {lotSize} exceeded : {pair.MaxLotSize}" };
                                                                        await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.AmountOfLotSizeShouldBeSmallerThenMaxLotsize, log);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={price},TP={tp},SL={sl}");
                                                                    var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the stop loss price", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                    await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingStopLossPrice, log);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},Price={price},TP={tp},SL={sl}");
                                                                var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation of the stop loss price", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingLotSize, log);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"CanNotOpenTradeDueClosingTime || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                            var log = new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = $"Can't open deal {pair.TickerInMetatrader} because market will be closed within {pair.DoNotOpenTradeXMinutesBeforeClose} minutes. It's now {DateTime.UtcNow} UTC time.", Magic = Convert.ToInt32(cmd.SignalID) };
                                                            await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.MarketWillBeClosedWithinXMinutes, log);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var message = string.Format($"CorrelatedPairsFound || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                        var log = new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = $"Correlated pair found in the open orders : {CorrelatedPairs.GetCorrelatedPair(pair.TickerInMetatrader, "SELL", pair.CorrelatedPairs, api.OpenOrders)}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                        await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.CorrelatedPairFound, log);
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
                                                        if (CloseTradeScheduler.CanOpenTrade(pair.CloseAllTradesAt, pair.DoNotOpenTradeXMinutesBeforeClose))
                                                        {
                                                            // Get the entry price
                                                            var price = DynamicEvaluator.EvaluateExpression(cmd.PassiveOrder.EntryExpression, api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(), out Dictionary<string, string> logMessages1);
                                                            var orgPrice = price;

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"EntryExpression || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                var description = string.Format($"Entry || {string.Join(", ", logMessages1.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                            }

                                                            // do 0.0 check
                                                            if (price.HasValue)
                                                            {
                                                                // Add the spread options
                                                                if (pair.SpreadEntry.HasValue)
                                                                {
                                                                    // Calculate EP
                                                                    price = RiskCalculator.CalculateSpreadExecForShort(price.Value, spread, pair.SpreadEntry.Value);
                                                                }

                                                                // Get the Stop Loss price
                                                                var sl = price.Value + (cmd.PassiveOrder.Risk.Value * Convert.ToDecimal(pair.SLMultiplier));
                                                                var slOrg = sl;

                                                                // If SL expression is enabled
                                                                if (!string.IsNullOrEmpty(cmd.PassiveOrder.StopLossExpression))
                                                                {
                                                                    // Get the SL price
                                                                    var slExpr = DynamicEvaluator.EvaluateExpression(cmd.PassiveOrder.StopLossExpression, api.HistoricBarData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList(), out Dictionary<string, string> logMessages2);

                                                                    // Send to logs
                                                                    if (_appConfig.Debug)
                                                                    {
                                                                        var message = string.Format($"StopLossExpression || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                        var description = string.Format($"StopLoss || {string.Join(", ", logMessages2.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                    }

                                                                    // Do null reference check
                                                                    if (slExpr.HasValue)
                                                                    {
                                                                        // Overwrite the stop loss price
                                                                        sl = slExpr.Value;
                                                                        slOrg = slExpr.Value;

                                                                        // Add SL Multiplier (Price + (risk * SlMultiplier)
                                                                        sl = price.Value + (Math.Abs(sl - price.Value) * Convert.ToDecimal(pair.SLMultiplier));
                                                                    }
                                                                }

                                                                // Add the spread options
                                                                if (pair.SpreadSL.HasValue)
                                                                {
                                                                    // Calculate SL
                                                                    sl = RiskCalculator.CalculateSpreadExecForShort(sl, spread, pair.SpreadSL.Value);
                                                                }

                                                                // Get the Take Profit Price
                                                                var tp = price.Value - (cmd.PassiveOrder.Risk.Value * cmd.PassiveOrder.RiskRewardRatio.Value);
                                                                if (pair.SpreadTP.HasValue)
                                                                {
                                                                    // Calculate TP
                                                                    tp = RiskCalculator.CalculateSpreadExecForShort(tp, spread, pair.SpreadTP.Value);
                                                                }

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"Order Calculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},SlExpr={cmd.PassiveOrder.StopLossExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                    var description = string.Format($"EntryPrice || Price={orgPrice},Spread={spread},EntryPrice={price},EntryExpression={cmd.PassiveOrder.EntryExpression}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);

                                                                    description = string.Format($"StopLossCalculated || StopLoss={slOrg},Spread={spread},EntryStopLoss={sl},RiskLong={cmd.PassiveOrder.Risk.Value},SLMultiplier={pair.SLMultiplier},StopLossExpression={cmd.PassiveOrder.StopLossExpression}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);

                                                                    description = string.Format($"TakeProfitCalculated ||  Spread={spread},EntryTakeProfit={tp},RiskLong={cmd.PassiveOrder.Risk.Value},RiskRewardRatio={cmd.PassiveOrder.RiskRewardRatio.Value}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                }

                                                                // Calculate the lot size
                                                                var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.RiskShort, price.Value, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, out Dictionary<string, string> logMessages3, dynRisk); ;

                                                                // Send to logs
                                                                if (_appConfig.Debug)
                                                                {
                                                                    var message = string.Format($"LotSizeCalculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                    var description = string.Format($"LotSize || {string.Join(", ", logMessages3.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                }

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
                                                                                if (pair.RiskMinXTimesTheSpread == 0 || (spread * pair.RiskMinXTimesTheSpread < cmd.PassiveOrder.Risk.Value))
                                                                                {
                                                                                    // Print on the screen
                                                                                    if (metadataTick.Bid < price)
                                                                                        Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / SELL LIMIT COMMAND / {cmd.SignalID} / {cmd.StrategyID}");
                                                                                    else
                                                                                        Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / SELL STOP COMMAND / {cmd.SignalID} / {cmd.StrategyID}");


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
                                                                                                var message = string.Format($"CancelledPassiveOrder || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                                var description = string.Format($"CancelStopOrLimitOrderWhenNewSignal: Symbol={pair.TickerInMetatrader}");
                                                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                                            }
                                                                                        }
                                                                                    }

                                                                                    // Setup the order
                                                                                    var comment = string.Format($"{cmd.SignalID}/{price}/{sl}/{(int)pair.StrategyID}/{spread}");
                                                                                    var orderType = OrderType.SellStop;
                                                                                    if (pair.OrderExecType == OrderExecType.Passive && metadataTick.Ask < price.Value)
                                                                                        orderType = OrderType.SellLimit;
                                                                                    else if (pair.OrderExecType == OrderExecType.Active && metadataTick.Ask <= price.Value)
                                                                                        orderType = OrderType.Sell;

                                                                                    // Round
                                                                                    price = RiskCalculator.RoundToNearestTickSize(price.Value, metadataTick.TickSize, metadataTick.Digits);
                                                                                    sl = RiskCalculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                                                    tp = RiskCalculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                                                    // Execute order
                                                                                    api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, orderType == OrderType.Sell ? 0 : price.Value, sl, tp, (int)cmd.SignalID, comment);

                                                                                    // Send to logs
                                                                                    if (_appConfig.Debug)
                                                                                    {
                                                                                        var message = string.Format($"MetatraderOrderExecuted || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                        var description = string.Format($"ExecuteOrder: Symbol={pair.TickerInMetatrader},OrderType={orderType},LotSize={lotSize},Price={price},SL={sl},TP={tp},SignalID={cmd.SignalID},Comment={comment}");
                                                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                                                    }
                                                                                }
                                                                                else
                                                                                {
                                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                    var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"The risk {cmd.PassiveOrder.Risk.Value} should be at least {pair.RiskMinXTimesTheSpread} times the spread : {spread}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                                    await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.RiskShouldBeAtLeastXTimesTheSpread, log);
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                                var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Max lot size {lotSize} exceeded : {pair.MaxLotSize}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                                await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.AmountOfLotSizeShouldBeSmallerThenMaxLotsize, log);
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                            var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Exception in calculating take profit price {tp}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                            await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingTakeProfitPrice, log);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                        var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Exception in calculating stop loss price {sl}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                        await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingStopLossPrice, log);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                    var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Exception in calculating lot size {lotSize}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                    await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingLotSize, log);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                                var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Can't find entry candle {cmd.PassiveOrder.EntryExpression} with date : {DynamicEvaluator.GetDateFromBarString(cmd.PassiveOrder.EntryExpression)}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                                await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingEntryPrice, log);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var message = string.Format($"CanNotOpenTradeDueClosingTime || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                            var log = new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = $"Can't open deal {pair.TickerInMetatrader} because market will be closed within {pair.DoNotOpenTradeXMinutesBeforeClose} minutes. It's now {DateTime.UtcNow} UTC time.", Magic = Convert.ToInt32(cmd.SignalID) };
                                                            await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.RiskShouldBeAtLeastXTimesTheSpread, log);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var message = string.Format($"CorrelatedPairsFound || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID},EntryExpr={cmd.PassiveOrder.EntryExpression},RiskLong={cmd.PassiveOrder.Risk},RR={cmd.PassiveOrder.RiskRewardRatio}");
                                                        var log = new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = $"Correlated pair found in the open orders : {CorrelatedPairs.GetCorrelatedPair(pair.TickerInMetatrader, "SELL", pair.CorrelatedPairs, api.OpenOrders)}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                        await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.CorrelatedPairFound, log);
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
                                                            // Calcualte price with offset
                                                            sl = ticketId.Value.OpenPrice + offset;

                                                            // Add the spread options
                                                            if (pair.SpreadSLtoBE.HasValue)
                                                            {
                                                                // Calculate SL
                                                                sl += RiskCalculator.CalculateSpreadExecForShort(sl, spread, pair.SpreadSLtoBE.Value);
                                                            }

                                                            // Check if the current price + spread is lower than the SL price
                                                            if (sl <= metadataTick.Ask)
                                                            {
                                                                // Set SL to 1 tick above the current price
                                                                sl = metadataTick.Ask + (2 * metadataTick.TickSize);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // If type is SELL, the SL should be set as BE minus Spread
                                                            sl = ticketId.Value.OpenPrice + offset;

                                                            // Add the spread options
                                                            if (pair.SpreadSLtoBE.HasValue)
                                                            {
                                                                // Calculate SL
                                                                sl -= RiskCalculator.CalculateSpreadExecForLong(sl, spread, pair.SpreadSLtoBE.Value);
                                                            }

                                                            // Check if the current price + spread is higher than the SL price
                                                            if (sl >= metadataTick.Bid)
                                                            {
                                                                // Set SL to 1 tick above the current price
                                                                sl = metadataTick.Bid - (2 * metadataTick.TickSize);
                                                            }
                                                        }

                                                        // Round
                                                        sl = RiskCalculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"StopLossCalculated || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                            var description = string.Format($"SL: OpenPrice={ticketId.Value.OpenPrice},Spread={spread},Offset={offset},Digits={metadataTick.Digits}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                        }

                                                        // Print on the screen
                                                        Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / MODIFY SL TO BE COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                        // Modify order
                                                        api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, sl, ticketId.Value.TakeProfit, (int)cmd.SignalID);

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"MetatraderOrderModified || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                            var description = string.Format($"ModifyOrder: TicketId={ticketId.Key},Lots={ticketId.Value.Lots},SL={sl},TP={ticketId.Value.TakeProfit}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Print on the screen
                                                        Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} {pair.TickerInMetatrader} / MODIFY SL TO BE / {cmd.SignalID} / UNABLE TO FIND TRADE");


                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Unable to find deal", Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                    }
                                                }

                                                // Close or cancel order
                                                else if ((cmd.OrderType == "CLOSE" || cmd.OrderType == "CANCEL") && cmd.SignalID > 0)
                                                {
                                                    // Check if the ticket still exist as open order
                                                    var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == cmd.SignalID);

                                                    // Null reference check
                                                    if (ticketId.Key > 0)
                                                    {
                                                        // Print on the screen
                                                        Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} {pair.TickerInMetatrader} / {cmd.OrderType} COMMAND / {cmd.SignalID}");

                                                        // Close order
                                                        api.CloseOrder(ticketId.Key, decimal.ToDouble(ticketId.Value.Lots));

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"MetatraderOrderClosed || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                            var description = string.Format($"CloseOrder: TicketId={ticketId.Key},Lots={ticketId.Value.Lots}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Print on the screen
                                                        Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / CLOSE COMMAND / {cmd.SignalID} / UNABLE TO FIND TRADE / {cmd.StrategyID}");

                                                        var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                        await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "Unable to find deal", Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                    }
                                                }

                                                // Close deal
                                                else if (cmd.OrderType == "CLOSEALL")
                                                {
                                                    // Null reference check
                                                    foreach (var order in api.OpenOrders)
                                                    {
                                                        // Get the strategy number from the comment field
                                                        string[] components = order.Value.Comment != null ? order.Value.Comment.Split('/') : [];
                                                        long strategyType = 0;
                                                        if (components != null && components.Length == 5)
                                                        {
                                                            _ = long.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyType);
                                                        }

                                                        if (strategyType == pair.StrategyID)
                                                        {
                                                            // Print on the screen
                                                            Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader} / CLOSEALL COMMAND / {cmd.SignalID} / {cmd.StrategyID}");

                                                            // Modify order
                                                            api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                                                            // Send to logs
                                                            if (_appConfig.Debug)
                                                            {
                                                                var message = string.Format($"MetatraderOrderClosed || Symbol={pair.TickerInMetatrader},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                                var description = string.Format($"CloseOrder: TicketId={order.Key},Lots={order.Value.Lots}");
                                                                await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = Convert.ToInt32(cmd.SignalID) }, cmd.SignalID);
                                                            }
                                                        }
                                                    }
                                                }

                                                if (pair.MaxSpread > 0 && spread > pair.MaxSpread)
                                                {
                                                    // Print on the screen
                                                    Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.TickerInMetatrader}  / {cmd.SignalID} /  SPREAD TOO HIGH / {cmd.StrategyID}");

                                                    // Raise market abstention or error
                                                    var message = string.Format($"Symbol={cmd.Instrument},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                    var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Spread is too high. Current spread is {spread} and expect max {pair.MaxSpread}", Magic = Convert.ToInt32(cmd.SignalID) };
                                                    await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.SpreadIsTooHigh, log);
                                                }
                                            }
                                            else
                                            {
                                                // Raise market abstention or error
                                                var message = string.Format($"Symbol={cmd.Instrument},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                                var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No market data available for this metatrader. Bid or ask price is 0.0", Magic = Convert.ToInt32(cmd.SignalID) };
                                                await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.NoMarketDataAvailable, log);
                                            }
                                        }
                                        else
                                        {
                                            // Raise market abstention or error
                                            var message = string.Format($"Symbol={cmd.Instrument},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                            var log = new Log() { Time = DateTime.UtcNow, Type = "WARNING", Message = message, ErrorType = "No subscription for this pair and strategy", Magic = Convert.ToInt32(cmd.SignalID) };
                                            await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.NoSubscriptionForThisPairAndStrategy, log);
                                        }
                                    }
                                    else
                                    {
                                        // Raise market abstention or error
                                        var message = string.Format($"Symbol={cmd.Instrument},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                        var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No market data available for this metatrader", Magic = Convert.ToInt32(cmd.SignalID) };
                                        await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.NoMarketDataAvailable, log);
                                    }
                                }
                                else
                                {
                                    // Raise market abstention or error
                                    var message = string.Format($"Symbol={cmd.Instrument},Type={cmd.OrderType},SignalID={cmd.SignalID},StrategyID={cmd.StrategyID}");
                                    var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No account info available for this metatrader", Magic = Convert.ToInt32(cmd.SignalID) };
                                    await RaiseMarketAbstentionAsync(api.ClientId, cmd.SignalID, cmd.Instrument, cmd.OrderType, MarketAbstentionType.NoAccountInfoAvailable,log);
                                }
                            });
                        }
                        else
                        {
                            await LogAsync(0, new Log() { Time = DateTime.UtcNow, Type = "ERROR", ErrorType = "Error message received from the server. Could not link it to an account." });
                        }
                    };

                    // OnSendManualOrderCommand
                    azureQueue.OnSendManualOrderCommand += async (cmd) =>
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
                                Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / MANUAL ORDER / {cmd.Magic}");

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
                                            var spread = Math.Round(Math.Abs(metadataTick.Ask - metadataTick.Bid), metadataTick.Digits, MidpointRounding.AwayFromZero);

                                            // BUY
                                            if (cmd.OrderType == "BUY" && cmd.MarketOrder != null)
                                            {
                                                // Get the Stop Loss price
                                                var sl = Convert.ToDecimal(cmd.MarketOrder.StopLossPrice);

                                                // Get the Take Profit Price
                                                var tp = metadataTick.Ask + ((metadataTick.Ask - Convert.ToDecimal(cmd.MarketOrder.StopLossPrice)) * Convert.ToDecimal(cmd.MarketOrder.RiskRewardRatio));

                                                // Calculate the lot size
                                                var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, Convert.ToDecimal(cmd.ProcentRiskOfBalance), metadataTick.Ask, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, out Dictionary<string, string> logMessages);

                                                // Send to logs
                                                if (_appConfig.Debug)
                                                {
                                                    var message = string.Format($"LotSizeCalculated || Symbol={pair.Instrument},Type={cmd.OrderType},SignalID={cmd.Magic},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                    var description = string.Format($"LotSize || {string.Join(", ", logMessages.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = cmd.Magic }, cmd.Magic);
                                                }

                                                // do 0.0 lotsize check
                                                if (lotSize > 0.0M)
                                                {
                                                    // Do 0.0 SL check
                                                    if (sl > 0.0M && tp > 0.0M)
                                                    {
                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"TakeProfitCalculated || Symbol={pair.Instrument},Type={cmd.OrderType},SignalID={cmd.Magic},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                            var description = string.Format($"TakeProfit || Ask={metadataTick.Ask},Spread={spread},Digits={metadataTick.Digits}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = cmd.Magic }, cmd.Magic);
                                                        }

                                                        // Print on the screen
                                                        Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.Instrument} / BUY COMMAND / {cmd.Magic} / {cmd.StrategyID}");

                                                        // Open order
                                                        var comment = string.Format($"{cmd.Magic}/{Math.Round(metadataTick.Ask, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{cmd.StrategyID}/{spread}");
                                                        var orderType = OrderType.Buy;

                                                        // Round
                                                        sl = RiskCalculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                        tp = RiskCalculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                        // Execute order
                                                        api.ExecuteOrder(pair.Instrument, orderType, lotSize, 0, sl, tp, cmd.Magic, comment);

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"MetatraderOrderExecuted || Symbol={pair.Instrument},Type={cmd.OrderType},SignalID={cmd.Magic},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                            var description = string.Format($"ExecuteOrder || Symbol={pair.Instrument},OrderType={orderType},LotSize={lotSize},Price={metadataTick.Ask},SL={sl},TP={tp},SignalID={cmd.Magic},Comment={comment}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = cmd.Magic }, cmd.Magic);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    // Raise market abstention or error
                                                    var message = string.Format($"LotSizeError || Symbol={pair.Instrument},Type={cmd.OrderType},SignalID={cmd.Magic},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                    var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation the lot size", Magic = cmd.Magic };
                                                    await RaiseMarketAbstentionAsync(api.ClientId, cmd.Magic, pair.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingLotSize, log);
                                                }
                                            }

                                            // SELL
                                            else if (cmd.OrderType == "SELL" && cmd.MarketOrder != null)
                                            {
                                                // Get the Stop Loss price
                                                var sl = Convert.ToDecimal(cmd.MarketOrder.StopLossPrice);

                                                // Get the Take Profit Price
                                                var tp = metadataTick.Bid - ((Convert.ToDecimal(cmd.MarketOrder.StopLossPrice) - metadataTick.Bid) * Convert.ToDecimal(cmd.MarketOrder.RiskRewardRatio));

                                                // Calculate the lot size
                                                var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, Convert.ToDecimal(cmd.ProcentRiskOfBalance), metadataTick.Bid, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, out Dictionary<string, string> logMessages);

                                                // Send to logs
                                                if (_appConfig.Debug)
                                                {
                                                    var message = string.Format($"LotSizeCalculated || Symbol={pair.Instrument},Type={cmd.OrderType},SignalID={cmd.Magic},StrategyID={cmd.StrategyID},Price={metadataTick.Bid},TP={tp},SL={sl}");
                                                    var description = string.Format($"LotSize || {string.Join(", ", logMessages.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                                                    await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = cmd.Magic }, cmd.Magic);
                                                }

                                                // do 0.0 lotsize check
                                                if (lotSize > 0.0M)
                                                {
                                                    // Do 0.0 SL check
                                                    if (sl > 0.0M && tp > 0.0M)
                                                    {
                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"TakeProfitCalculated || Symbol={pair.Instrument},Type={cmd.OrderType},SignalID={cmd.Magic},StrategyID={cmd.StrategyID},Price={metadataTick.Bid},TP={tp},SL={sl}");
                                                            var description = string.Format($"TakeProfit || Bid={metadataTick.Bid},Spread={spread},Digits={metadataTick.Digits}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = cmd.Magic }, cmd.Magic);
                                                        }

                                                        // Print on the screen
                                                        Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {pair.Instrument} / SELL COMMAND / {cmd.Magic} / {cmd.StrategyID}");

                                                        // Open order
                                                        var comment = string.Format($"{cmd.Magic}/{Math.Round(metadataTick.Bid, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{cmd.StrategyID}/{spread}");
                                                        var orderType = OrderType.Sell;

                                                        // Round
                                                        sl = RiskCalculator.RoundToNearestTickSize(sl, metadataTick.TickSize, metadataTick.Digits);
                                                        tp = RiskCalculator.RoundToNearestTickSize(tp, metadataTick.TickSize, metadataTick.Digits);

                                                        // Execute order
                                                        api.ExecuteOrder(pair.Instrument, orderType, lotSize, 0, sl, tp, cmd.Magic, comment);

                                                        // Send to logs
                                                        if (_appConfig.Debug)
                                                        {
                                                            var message = string.Format($"MetatraderOrderExecuted || Symbol={pair.Instrument},Type={cmd.OrderType},SignalID={cmd.Magic},StrategyID={cmd.StrategyID},Price={metadataTick.Bid},TP={tp},SL={sl}");
                                                            var description = string.Format($"ExecuteOrder || Symbol={pair.Instrument},OrderType={orderType},LotSize={lotSize},Price={metadataTick.Bid},SL={sl},TP={tp},SignalID={cmd.Magic},Comment={comment}");
                                                            await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = cmd.Magic }, cmd.Magic);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    // Raise market abstention or error
                                                    var message = string.Format($"LotSizeError || Symbol={pair.Instrument},Type={cmd.OrderType},SignalID={cmd.Magic},StrategyID={cmd.StrategyID},Price={metadataTick.Ask},TP={tp},SL={sl}");
                                                    var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = $"Unexpected error occurred with the calculation the lot size", Magic = cmd.Magic };
                                                    await RaiseMarketAbstentionAsync(api.ClientId, cmd.Magic, pair.Instrument, cmd.OrderType, MarketAbstentionType.ExceptionCalculatingLotSize, log);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Raise market abstention or error
                                        var message = string.Format($"Symbol={cmd.ClientInstruments?.First().Instrument},Type={cmd.OrderType},SignalID={cmd.Magic},StrategyID={cmd.StrategyID}");
                                        var log = new Log() { Time = DateTime.UtcNow, Type = "ERROR", Message = message, ErrorType = "No account info available for this metatrader", Magic = cmd.Magic };
                                        await RaiseMarketAbstentionAsync(cmd.ClientInstruments.First().ClientID, cmd.Magic, cmd.ClientInstruments.First().Instrument, cmd.OrderType, MarketAbstentionType.NoAccountInfoAvailable, log);
                                    }
                                }
                            });
                        }
                        else
                        {
                            await LogAsync(0, new Log() { Time = DateTime.UtcNow, Type = "ERROR", ErrorType = "Error message received from the server. Could not link it to an account" });
                        }
                    };

                    // StartCheckTimeAndExecuteOnceDaily listening to the queue
                    await azureQueue.ListeningToServerAsync();
                }
            }
        }




        private void OnItsTimeToCloseTradeEvent(long clientID, string instrument, long strID)
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
                        string[] components = order.Value.Comment != null ? order.Value.Comment.Split('/') : [];
                        long signalID = 0;
                        long strategyID = 0;
                        if (components != null && components.Length == 5)
                        {
                            _ = long.TryParse(components[0], out signalID);
                            _ = long.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyID);
                        }

                        if (strID == strategyID && signalID > 0)
                        {
                            // Print on the screen
                            Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {order.Value.Symbol} / IT'S TIME TO CLOSE THE ORDER EVENT / {order.Value.Magic} / {strategyID}");

                            // Modify order
                            api.CloseOrder(order.Key, decimal.ToDouble(order.Value.Lots));

                            // Send log to files
                            var message = string.Format($"Symbol={order.Value.Symbol},Ticket={order.Key},Lots={order.Value.Lots},Type={order.Value.Type},SignalID={order.Value.Magic},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={order.Value.StopLoss},Comment={order.Value.Comment}");
                            var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = $"Close all trades at {DateTime.UtcNow}", Message = message, Magic = Convert.ToInt32(signalID) };

                            // Send log to files
                            Task.Run(async () =>
                            {
                                // Send the event to server
                                await HttpCall.OnItsTimeToCloseTheOrderEvent(new OnItsTimeToCloseTheOrderEvent()
                                {
                                    ClientID = clientID,
                                    SignalID = signalID,
                                    Log = log
                                });

                                // Send log to file
                                await LogAsync(clientID, log, signalID);
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
                // StartCheckTimeAndExecuteOnceDaily the system
                foreach (var api in _apis.Where(f => f.IsActive && f.ClientId == clientId && f.OpenOrders != null && f.OpenOrders.Count > 0))
                {
                    // Clone the open order
                    foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol != null && f.Value.Symbol.Equals(instrument)).ToDictionary(entry => entry.Key, entry => entry.Value))
                    {
                        // Get the strategy number from the comment field
                        string[] components = order.Value.Comment != null ? order.Value.Comment.Split('/') : [];
                        long signalId = 0;
                        var signalEntryPrice = 0.0M;
                        var signalStopLoss = 0.0M;
                        var spread = 0.0M;
                        long strategyID = 0;
                        if (components != null && components.Length == 5)
                        {
                            _ = long.TryParse(components[0], out signalId);
                            _ = decimal.TryParse(components[1], out signalEntryPrice);
                            _ = decimal.TryParse(components[2], out signalStopLoss);
                            _ = decimal.TryParse(components[4], out spread);
                            _ = long.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyID);
                        }

                        // Get the right pair back from the local database
                        var pair = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(order.Value.Symbol) && f.StrategyID == strategyID && f.Timeframe.Equals(timeFrame));

                        // If this broker is listening to this signal and the account size is greater then zero
                        if (pair != null && signalEntryPrice > 0 && api.MarketData != null && signalStopLoss > 0 && strategyID != 0)
                        {
                            // When the risk setting is enabled
                            if (pair.RiskLong > 0 && pair.RiskShort > 0 && pair.SLtoBEafterR > 0)
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
                                        if (close >= (order.Value.OpenPrice + (Convert.ToDecimal(pair.SLtoBEafterR) * risk)))
                                        {
                                            // Set SL to BE
                                            var slPrice = order.Value.OpenPrice - spread;

                                            // Round
                                            slPrice = RiskCalculator.RoundToNearestTickSize(slPrice, marketdata.Value.TickSize, marketdata.Value.Digits);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != slPrice)
                                            {
                                                // Send to logs
                                                if (_appConfig.Debug)
                                                {
                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={order.Value.Type},SignalID={order.Value.Magic},StrategyID={strategyID},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={slPrice}");
                                                    var description = string.Format($"SL: OpenPrice={order.Value.OpenPrice},Spread={spread},Digits={marketdata.Value.Digits}");
                                                    Task.Run(async () => await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = order.Value.Magic }, signalId));
                                                }

                                                // Print on the screen
                                                Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {order.Value.Symbol} / AUTO - MODIFY SL TO BE / {order.Value.Magic} / {strategyID}");

                                                // Modify order
                                                api.ModifyOrder(order.Key, order.Value.Lots, 0, slPrice, order.Value.TakeProfit, order.Value.Magic);

                                                var message2 = string.Format($"Symbol={order.Value.Symbol},Ticket={order.Key},Lots={order.Value.Lots},Type={order.Value.Type},SignalID={order.Value.Magic},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={slPrice},Comment={order.Value.Comment}");
                                                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Message = message2, Description = "Auto move SL to BE", Magic = Convert.ToInt32(signalId) };

                                                // Send log to BE
                                                Task.Run(async () =>
                                                {
                                                    // Send the event to Azure PubSub server
                                                    await HttpCall.OnOrderAutoMoveSlToBeEvent(new OnOrderAutoMoveSlToBeEvent()
                                                    {
                                                        ClientID = clientId,
                                                        SignalID = signalId,
                                                        StopLossPrice = slPrice,
                                                        Log = log
                                                    });

                                                    // Send log to file
                                                    await LogAsync(clientId, log, signalId);
                                                });
                                            }
                                        }
                                    }
                                    else if (order.Value.Type?.ToUpper() == "SELL")
                                    {
                                        // If the current BID signalEntryPrice is smaller then x times the risk
                                        if (close <= (order.Value.OpenPrice - (Convert.ToDecimal(pair.SLtoBEafterR) * risk)))
                                        {
                                            // Set SL to BE
                                            var slPrice = order.Value.OpenPrice + spread;

                                            // Round
                                            slPrice = RiskCalculator.RoundToNearestTickSize(slPrice, marketdata.Value.TickSize, marketdata.Value.Digits);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != slPrice)
                                            {
                                                // Send to logs
                                                if (_appConfig.Debug)
                                                {
                                                    var message = string.Format($"Symbol={pair.TickerInMetatrader},Type={order.Value.Type},SignalID={order.Value.Magic},StrategyID={strategyID},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={slPrice}");
                                                    var description = string.Format($"SL: OpenPrice={order.Value.OpenPrice},Spread={spread},Digits={marketdata.Value.Digits}");
                                                    Task.Run(async () => await LogAsync(api.ClientId, new Log() { Time = DateTime.UtcNow, Type = "DEBUG", Message = message, Description = description, Magic = order.Value.Magic }, signalId));
                                                }

                                                // Print on the screen
                                                Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name} / {order.Value.Symbol} / AUTO - MODIFY SL TO BE / {order.Value.Magic} / {strategyID}");

                                                // Modify order
                                                api.ModifyOrder(order.Key, order.Value.Lots, 0, slPrice, order.Value.TakeProfit, order.Value.Magic);

                                                // Send log to files
                                                var message2 = string.Format($"Symbol={order.Value.Symbol},Ticket={order.Key},Lots={order.Value.Lots},Type={order.Value.Type},SignalID={order.Value.Magic},Price={order.Value.OpenPrice},TP={order.Value.TakeProfit},SL={slPrice},Comment={order.Value.Comment}");
                                                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Message = message2, Description = "Auto move SL to BE", Magic = Convert.ToInt32(signalId) };

                                                // Send log to BE
                                                Task.Run(async () =>
                                                {
                                                    // Send the event to Azure PubSub server
                                                    await HttpCall.OnOrderAutoMoveSlToBeEvent(new OnOrderAutoMoveSlToBeEvent()
                                                    {
                                                        ClientID = clientId,
                                                        SignalID = signalId,
                                                        StopLossPrice = slPrice,
                                                        Log = log
                                                    });

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

        private void OnLogEvent(long clientId, long id, Log log)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                if (log.Type?.ToUpper() == "ERROR")
                    Print($"ERROR : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / LOG EVENT / {log.ErrorType}");
                else if (log.Description != null)
                    Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / LOG EVENT / {log.Description}");
                else
                    Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / LOG EVENT / {log.Message}");

                // When log have an error and have a magic id => market abstention
                if (log.Type?.ToUpper() == "ERROR" && log.ErrorType != null && log.ErrorType.Equals("OPEN_ORDER") && log.Magic.HasValue && log.Magic.Value > 0 && log.Description != null)
                {
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
                    Task.Run(async () =>
                    {
                        // Send log to files
                        await RaiseMetatraderMarketAbstentionAsync(clientId, log.Magic.Value, symbol, orderType, log);
                    });

                    // Buffer log
                    var logs = _buffers.GetOrAdd(clientId, []);
                    lock (logs) // Ensure thread-safety for list modification
                    {
                        // Add log to the list
                        logs.Add(log);
                    }
                }
                else
                {
                    // Send log to files
                    Task.Run(async () =>
                    {
                        // Send log to files
                        await LogAsync(clientId, log, log.Magic);
                    });
                }
            }
        }

        private void OnOrderCreateEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Get the signal id from the comment field
                string[] components = order.Comment != null ? order.Comment.Split('/') : [];
                long signalID = 0;
                long strategyID = 0;
                if (components != null && components.Length == 5)
                {
                    _ = long.TryParse(components[0], out signalID);
                    _ = long.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyID);
                }

                // Send log to files
                var message = string.Format($"OrderCreated || Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},SignalID={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss},Comment={order.Comment}");
                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Create order", Message = message, Magic = Convert.ToInt32(signalID) };


                // Print on the screen
                Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {order.Symbol} / CREATED ORDER EVENT / {order.Magic} / {strategyID}");

                // Check if we need to start the scheduler
                var pair = _appConfig.Brokers.FirstOrDefault(f => f.ClientId == clientId && f.Pairs.Any(g => g.TickerInMetatrader == order.Symbol && g.CloseTradeWithinXBars.HasValue))?
                                    .Pairs.FirstOrDefault(g => g.TickerInMetatrader == order.Symbol && g.CloseTradeWithinXBars.HasValue);

                // check if pair is not null
                if(pair != null && pair.CloseTradeWithinXBars.HasValue)
                {
                    var scheduler = new CloseTradeScheduler(clientId, pair.TickerInMetatrader, pair.StrategyID, true);
                    scheduler.OnCloseTradeEvent += OnItsTimeToCloseTradeEvent;
                    scheduler.Start(pair.TimeframeAsTimespan * pair.CloseTradeWithinXBars.Value);
                    _schedulers.Add(scheduler);
                }


                Task.Run(async () =>
                {
                    // Send the tradejournal to Azure PubSub server
                    await HttpCall.OnOrderCreatedEvent(new OnOrderCreatedEvent()
                    {
                        ClientID = clientId,
                        SignalID = signalID,
                        Order = order,
                        Log = log
                    });

                    // Send log to files
                    await LogAsync(clientId, log, signalID);
                });
            }
        }

        private void OnOrderUpdateEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Get the signal id from the comment field
                string[] components = order.Comment != null ? order.Comment.Split('/') : [];
                long signalId = 0;
                long strategyID = 0;
                if (components != null && components.Length == 5)
                {
                    _ = long.TryParse(components[0], out signalId);
                    _ = long.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyID);
                }

                // Send log to files
                var message = string.Format($"OrderUpdated || Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},SignalID={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss},Comment={order.Comment}");
                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Update order", Message = message, Magic = Convert.ToInt32(signalId) };


                // Print on the screen
                Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {order.Symbol} / UPDATED ORDER EVENT / {order.Magic} / {strategyID}");

                Task.Run(async () =>
                {
                    // Send the tradejournal to Azure PubSub server
                    await HttpCall.OnOrderUpdatedEvent(new OnOrderUpdatedEvent()
                    {
                        ClientID = clientId,
                        SignalID = signalId,
                        Order = order,
                        Log = log
                    });

                    // Send log to files
                    await LogAsync(clientId, log, signalId);
                });
            }
        }

        private void OnOrderCloseEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null && _apis.Count(f => f.ClientId == clientId) == 1)
            {
                // Get the signal id from the comment field
                string[] components = order.Comment != null ? order.Comment.Split('/') : [];
                long signalID = 0;
                long strategyID = 0;
                if (components != null && components.Length == 5)
                {
                    _ = long.TryParse(components[0], out signalID);
                    _ = long.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyID);
                }

                // Send log to files
                var message = string.Format($"OrderCreated || Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},SignalID={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss},Comment={order.Comment}");
                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Close order", Message = message, Magic = Convert.ToInt32(signalID) };

                // Print on the screen
                Print($"INFO : {DateTime.UtcNow} / {_appConfig.Brokers.First(f => f.ClientId == clientId).Name} / {order.Symbol} / CLOSED ORDER EVENT / {order.Magic} / {strategyID}");

                // Get pair
                var api = _apis.First(f => f.ClientId == clientId);
                var marketdata = api.MarketData?.FirstOrDefault(f => f.Key == order.Symbol).Value;

                Task.Run(async () =>
                {
                    // Send the tradejournal to Azure PubSub server
                    await HttpCall.OnOrderClosedEvent(new OnOrderClosedEvent()
                    {
                        ClientID = clientId,
                        SignalID = signalID,
                        ClosePrice = marketdata != null ? marketdata.Ask : 0.0M,
                        Order = order,
                        Log = log
                    });

                    // Send logs to file
                    await LogAsync(clientId, log, signalID);
                });
            }
        }

        private void OnDealCreateEvent(long clientId, long tradeId, Deal deal)
        {
            // Do null reference check
            if (_appConfig != null && _apis.Count(f => f.ClientId == clientId) == 1)
            {
                // Send log to files
                var message = string.Format($"DealCreated || Symbol={deal.Symbol},TradeId={tradeId},Lots={deal.Lots},Type={deal.Type},SignalID={deal.Magic},Entry={deal.Entry}");
                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "Deal created", Message = message, Magic = deal.Magic };

                // Get api
                var api = _apis.First(f => f.ClientId == clientId);

                // Do null reference chekc on metadatatick
                if (api.MarketData != null)
                {
                    // Get the metadata tick
                    var metadataTick = api.MarketData.FirstOrDefault(f => f.Key == deal.Symbol).Value;

                    // Do null reference chekc on metadatatick
                    if (metadataTick != null)
                    {
                        // Get the spread
                        var spread = decimal.ToDouble(Math.Round(Math.Abs(metadataTick.Ask - metadataTick.Bid), metadataTick.Digits, MidpointRounding.AwayFromZero));

                        Task.Run(async () =>
                        {
                            // Send the tradejournal to Azure PubSub server
                            await HttpCall.OnDealCreatedEvent(new OnDealCreatedEvent()
                            {
                                ClientID = clientId,
                                MtDealID = tradeId,
                                Deal = deal,
                                Log = log,
                                AccountBalance = api.AccountInfo?.Balance,
                                AccountEquity = api.AccountInfo?.Equity,
                                Price = decimal.ToDouble(metadataTick.Ask),
                                Spread = spread,
                                SpreadCost = RiskCalculator.CostSpread(spread, deal.Lots, metadataTick.PointSize, metadataTick.ContractSize),
                            });
                        });
                    }
                }
            }
        }

        private void OnAccountInfoChangedEvent(long clientId, AccountInfo accountInfo)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Send log to files
                var message = string.Format($"AccountInfoChanged || Name={accountInfo.Name},Balance={accountInfo.Balance},Equity={accountInfo.Equity}");
                var log = new Log() { Time = DateTime.UtcNow, Type = "INFO", Description = "AccountInfo Changed", Message = message };

                Task.Run(async () =>
                {
                    // Send the tradejournal to Azure PubSub server
                    await HttpCall.OnAccountInfoChangedEvent(new OnAccountInfoChangedEvent()
                    {
                        ClientID = clientId,
                        AccountInfo = accountInfo,
                        Log = log,
                    });
                });
            }
        }

        private async Task RaiseMarketAbstentionAsync(long clientId, long signalId, string symbol, string orderType, MarketAbstentionType type, Log log)
        {
            // Do null reference check
            if (_appConfig != null && _apis != null && (_apis.Count(f => f.ClientId == clientId) == 1 || clientId == 0))
            {
                // Send the tradejournal to Azure PubSub server
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

        private async Task RaiseMetatraderMarketAbstentionAsync(long clientId, long magic, string symbol, string orderType, Log log)
        {
            // Do null reference check
            if (_appConfig != null && _apis != null && (_apis.Count(f => f.ClientId == clientId) == 1 || clientId == 0))
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
            }
        }

        public async Task LoadLogFromFileAsync(long clientId)
        {
            if (_appConfig?.Brokers.Any(f => f.ClientId == clientId) == true)
            {
                var pair = _appConfig.Brokers.First(f => f.ClientId == clientId);
                string fileName = "JCTG_Logs.json";

                await _semaphore.WaitAsync();
                try
                {
                    string fullPath = Path.Combine(pair.MetaTraderDirPath, "JCTG", fileName);
                    if (File.Exists(fullPath))
                    {
                        var json = await File.ReadAllTextAsync(fullPath);
                        var logsFromFile = JsonConvert.DeserializeObject<List<Log>>(json) ?? new List<Log>();

                        var logs = _buffers.GetOrAdd(clientId, _ => []);
                        lock (logs)
                        {
                            logs.Clear();
                            logs.AddRange(logsFromFile.OrderByDescending(f => f.Time));
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        private async Task LogAsync(long clientId, Log log, long? magic = null)
        {
            if (_appConfig?.DropLogsInFile == true && (_apis?.Count(f => f.ClientId == clientId) == 1 || clientId == 0))
            {
                await HttpCall.OnLogEvent(new OnLogEvent
                {
                    ClientID = clientId,
                    Magic = magic,
                    Log = log
                });

                var logs = _buffers.GetOrAdd(clientId, _ => new List<Log>());
                lock (logs)
                {
                    logs.Add(log);
                }
            }
        }

        private async Task FlushLogsToFileAsync()
        {
            if (_appConfig == null || _apis == null) return;

            var tasks = _buffers.Keys.Select(async clientId =>
            {
                List<Log> logsToWrite = [];
                bool logsAvailable = false;

                if (_appConfig.Brokers.FirstOrDefault(f => f.ClientId == clientId) is { } pair && _buffers.TryGetValue(clientId, out var logs))
                {
                    lock (logs)
                    {
                        if (logs.Count > 0)
                        {
                            logsToWrite = new List<Log>(logs);
                            logsAvailable = true;
                        }
                    }

                    if (logsAvailable)
                    {
                        string fileName = "JCTG_Logs.json";
                        await _semaphore.WaitAsync();
                        try
                        {
                            string fullPath = Path.Combine(pair.MetaTraderDirPath, "JCTG", fileName);
                            logsToWrite = [.. logsToWrite.Where(log => log.Time >= DateTime.UtcNow.AddMonths(-1)).OrderByDescending(log => log.Time)];
                            await TryWriteFileAsync(fullPath, JsonConvert.SerializeObject(logsToWrite));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error writing logs for clientID {clientId}: {ex.Message}");
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    }
                }
            });

            await Task.WhenAll(tasks);
        }

        // Ensure to call this method to properly dispose of the timerCheckTimeAndExecuteOnceDaily when the LogManager is no longer needed
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
