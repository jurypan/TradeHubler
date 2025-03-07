﻿using JCTG.Command;
using JCTG.Entity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using JCTG.WebApp.Backend.Queue;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;

namespace JCTG.WebApp.Backend.Api
{
    [ApiController]
    [Route("api")]
    public class TradingviewController : ControllerBase
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<TradingviewController>();
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public TradingviewController(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        [AllowAnonymous]
        [HttpGet("tradingview")]
        [HttpPost("tradingview")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TradingView()
        {
            // Start measuring time
            var stopwatch = Stopwatch.StartNew();

            // Security check
            var code = Request.Query["code"];
            var method = Request.Query["method"];
            if (code != "Ocebxtg1excWosFez5rWMtNp3ZsmIzSFQ0XhqtrfHlMuAzFuQ0OGhA==")
            {
                _logger.Debug("code is not ok");
                return BadRequest();
            }

            // Read body from request
            string requestBody;
            using (var reader = new StreamReader(Request.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            // Log signal
            _logger.Debug($"!! TRADINGVIEW SIGNAL : {requestBody}");

            var processTask = ProcessTradingViewSignal(requestBody.Trim().Trim('\n', '\r', ' '), string.IsNullOrEmpty(method) ? "webhook" : method.ToString());

            // Wait for either the processing task to complete or a timeout of 3 seconds
            var completedTask = await Task.WhenAny(processTask, Task.Delay(3000));

            stopwatch.Stop(); // Stop measuring time

            if (completedTask == processTask)
            {
                // If the task completed within 3 seconds, return the result
                await processTask; // This will rethrow any exceptions from the task if it failed
                return Ok($"Processed successfully in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            }
            else
            {
                // If the task took longer than 3 seconds, return a timeout response
                _logger.Warning("Processing exceeded the 3-second timeout.");
                return Ok($"Processing in progress, exceeded 3-second timeout, processed for {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
            }
        }

        private async Task ProcessTradingViewSignal(string requestBody, string method)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();
                var _server = scope.ServiceProvider.GetRequiredService<AzureQueueClient>();

                if (!string.IsNullOrEmpty(requestBody))
                {
                    try
                    {
                        // Parse as signal
                        var signal = Signal.Parse(requestBody);

                        // Log
                        _logger.Information($"Parsed object {JsonConvert.SerializeObject(signal)}", signal);

                        // ALWAYS MANDATORY
                        if (signal.AccountID == 0)
                        {
                            _logger.Error($"'licenseId' is mandatory for {signal.OrderType}");
                            return;
                        }

                        if (string.IsNullOrEmpty(signal.Instrument))
                        {
                            _logger.Error($"'ticker' is mandatory for {signal.OrderType}");
                            return;
                        }

                        if (string.IsNullOrEmpty(signal.OrderType))
                        {
                            _logger.Error($"'ordertype' is mandatory for {signal.OrderType}");
                            return;
                        }

                        if (signal.StrategyID == 0)
                        {
                            _logger.Error($"'strategy' is mandatory for {signal.OrderType}");
                            return;
                        }
                        else
                        {
                            if (!_dbContext.Strategy.Any(f => f.AccountID == signal.AccountID && f.ID == signal.StrategyID))
                            {
                                _logger.Error($"'strategy' with id {signal.StrategyID} doesn't exist for this account");
                                return;
                            }
                        }

                        if (!await _dbContext.ClientPair.Include(f => f.Client).AnyAsync(f => f.Client != null && f.Client.AccountID == signal.AccountID && f.TickerInTradingView.Equals(signal.Instrument)))
                        {
                            _logger.Error($"'instrument' {signal.Instrument} doesn't exist for this account");
                            return;
                        }



                        // MARKET ORDERS
                        switch (signal.OrderType.ToLower())
                        {
                            case "buy":
                            case "sell":


                                if (signal.Magic == 0)
                                {
                                    _logger.Error($"'magic' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (signal.Risk == 0)
                                {
                                    _logger.Error($"'risk' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (signal.RiskRewardRatio == 0)
                                {
                                    _logger.Error($"'rr' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.EntryPrice.HasValue)
                                {
                                    _logger.Error($"'entryprice' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.StopLoss.HasValue)
                                {
                                    _logger.Error($"'sl' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.TakeProfit.HasValue)
                                {
                                    _logger.Error($"'tp' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                break;
                            case "buystop":
                            case "buylimit":
                            case "selllimit":
                            case "sellstop":

                                if (signal.Magic == 0)
                                {
                                    _logger.Error($"'magic' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (signal.Risk == 0)
                                {
                                    _logger.Error($"'risk' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (signal.RiskRewardRatio == 0)
                                {
                                    _logger.Error($"'rr' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.EntryPrice.HasValue)
                                {
                                    _logger.Error($"'entryprice' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (string.IsNullOrEmpty(signal.EntryExpression))
                                {
                                    _logger.Error($"'entryexpr' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.StopLoss.HasValue)
                                {
                                    _logger.Error($"'sl' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (string.IsNullOrEmpty(signal.StopLossExpression))
                                {
                                    _logger.Error($"'slexpr' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.TakeProfit.HasValue)
                                {
                                    _logger.Error($"'tp' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                break;
                            case "entrylong":
                            case "entryshort":

                                if (signal.Magic == 0)
                                {
                                    _logger.Error($"'magic' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (signal.Risk == 0)
                                {
                                    _logger.Error($"'risk' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (signal.RiskRewardRatio == 0)
                                {
                                    _logger.Error($"'rr' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.EntryPrice.HasValue)
                                {
                                    _logger.Error($"'entryprice' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.StopLoss.HasValue)
                                {
                                    _logger.Error($"'sl' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.TakeProfit.HasValue)
                                {
                                    _logger.Error($"'tp' is mandatory for {signal.OrderType}");
                                    return;
                                }
                                break;
                            case "tphit":
                            case "slhit":
                            case "behit":
                            case "close":
                            case "closeall":

                                if (signal.Magic == 0)
                                {
                                    _logger.Error($"'magic' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                if (!signal.ExitRiskRewardRatio.HasValue)
                                {
                                    _logger.Error($"'exitrr' is mandatory for {signal.OrderType}");
                                    return;
                                }

                                break;

                            case "cancel":
                            case "movesltobe":
                                // No extra parameters required
                                break;
                            default:
                                break;
                        }


                        // Add to the tradingview alert
                        if (!await _dbContext.TradingviewAlert.AnyAsync(f => f.RawMessage.Equals(requestBody)))
                        {
                            // Add log
                            _logger.Debug($"Add signal to the database");

                            // Add to database
                            await _dbContext.TradingviewAlert.AddAsync(new TradingviewAlert()
                            {
                                DateCreated = DateTime.UtcNow,
                                AccountID = signal.AccountID,
                                RawMessage = requestBody,
                                TvMagic = signal.Magic,
                                Type = TradingviewAlert.ParseTradingviewAlertTypeOrDefault(signal.OrderType.ToLower()),
                                Method = method == "webhook" ? TradingviewMethod.Webhook : TradingviewMethod.Email,
                            });

                            // Save to the database
                            await _dbContext.SaveChangesAsync();

                            // Get the client id's related to this instrument
                            var clientIds = await _dbContext.ClientPair
                                .Include(f => f.Client)
                                .Where(f => f.Client != null && f.Client.AccountID == signal.AccountID && f.TickerInTradingView.Equals(signal.Instrument) && f.StrategyID == signal.StrategyID)
                                .Select(f => f.ClientID)
                                .ToListAsync();

                            // Action!
                            switch (signal.OrderType.ToLower())
                            {
                                case "buy":
                                case "buystop":
                                case "buylimit":
                                case "sell":
                                case "selllimit":
                                case "sellstop":

                                    var entryLongOrShortFlag = false;

                                    // Check if previous signal of this strategy is set as init (if -> set as cancelled)
                                    if (signal.OrderType.Equals("buystop", StringComparison.CurrentCultureIgnoreCase)
                                         || signal.OrderType.Equals("buylimit", StringComparison.CurrentCultureIgnoreCase)
                                         || signal.OrderType.Equals("selllimit", StringComparison.CurrentCultureIgnoreCase)
                                         || signal.OrderType.Equals("sellstop", StringComparison.CurrentCultureIgnoreCase)
                                        )
                                    {
                                        // Cancel potential previous  trades
                                        var prevSignal = await _dbContext.Signal.Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.OrderType == signal.OrderType && s.StrategyID == signal.StrategyID).OrderByDescending(f => f.DateCreated).FirstOrDefaultAsync();

                                        // do null reference check
                                        if (prevSignal != null)
                                        {
                                            if (prevSignal.SignalStateType == SignalStateType.Init)
                                            {
                                                prevSignal.SignalStateType = SignalStateType.Cancel;
                                                prevSignal.ExitRiskRewardRatio = 0;
                                            }
                                        }

                                        // Check if there is a BUY or SELL order in the database, coming from entrylong or entryshort
                                        if (await _dbContext.Signal.Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.OrderType == signal.OrderType && s.StrategyID == signal.StrategyID && s.Magic == signal.Magic).AnyAsync())
                                        {
                                            entryLongOrShortFlag = true;
                                        }
                                    }

                                    // Check if this command is already in the database via entrylong or entryshort
                                    if (entryLongOrShortFlag == false)
                                    {
                                        if (signal.OrderType.Equals("buy", StringComparison.CurrentCultureIgnoreCase))
                                            signal.SignalStateType = SignalStateType.Entry;
                                        else if (signal.OrderType.Equals("buystop", StringComparison.CurrentCultureIgnoreCase))
                                            signal.SignalStateType = SignalStateType.Init;
                                        else if (signal.OrderType.Equals("buylimit", StringComparison.CurrentCultureIgnoreCase))
                                            signal.SignalStateType = SignalStateType.Init;
                                        else if (signal.OrderType.Equals("sell", StringComparison.CurrentCultureIgnoreCase))
                                            signal.SignalStateType = SignalStateType.Entry;
                                        else if (signal.OrderType.Equals("selllimit", StringComparison.CurrentCultureIgnoreCase))
                                            signal.SignalStateType = SignalStateType.Init;
                                        else if (signal.OrderType.Equals("sellstop", StringComparison.CurrentCultureIgnoreCase))
                                            signal.SignalStateType = SignalStateType.Init;

                                        // Check if this magic id is already in the database
                                        var __signal = await _dbContext.Signal.FirstOrDefaultAsync(f => f.AccountID == signal.AccountID && f.Instrument.Equals(signal.Instrument) && f.StrategyID == signal.StrategyID && f.Magic == signal.Magic);

                                        // Do null reference check
                                        if (__signal == null)
                                        {
                                            // If the order type is one of the order types, add the signal to the database
                                            await _dbContext.Signal.AddAsync(signal);

                                            // Save to the database
                                            await _dbContext.SaveChangesAsync();

                                            // Add log
                                            _logger.Information($"Added to database in table Signal with ID: {signal.ID}", signal);
                                        }

                                        // Create model and send to the client
                                        if (signal.OrderType.Equals("BUY", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.Buy
                                                (
                                                    signalId: __signal == null ? signal.ID : __signal.ID,
                                                    accountId: signal.AccountID,
                                                    instrument: signal.Instrument,
                                                    strategyId: signal.StrategyID,
                                                    risk: signal.Risk,
                                                    riskToRewardRatio: signal.RiskRewardRatio,
                                                    stopLossExpression: signal.StopLossExpression,
                                                    clientIds: clientIds
                                                ));

                                            // Add log
                                            _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);
                                        }
                                        else if (signal.OrderType.Equals("SELL", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.Sell
                                                (
                                                    signalId: __signal == null ? signal.ID : __signal.ID,
                                                    accountId: signal.AccountID,
                                                    instrument: signal.Instrument,
                                                    strategyId: signal.StrategyID,
                                                    risk: signal.Risk,
                                                    riskToRewardRatio: signal.RiskRewardRatio,
                                                    stopLossExpression: signal.StopLossExpression,
                                                    clientIds: clientIds
                                                ));

                                            // Add log
                                            _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);
                                        }
                                        else if (signal.OrderType.Equals("BUYSTOP", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.BuyStop
                                                (
                                                    signalId: __signal == null ? signal.ID : __signal.ID,
                                                    accountId: signal.AccountID,
                                                    instrument: signal.Instrument,
                                                    strategyId: signal.StrategyID,
                                                    risk: signal.Risk,
                                                    riskToRewardRatio: signal.RiskRewardRatio,
                                                    entryExpression: signal.EntryExpression ?? string.Empty,
                                                    stopLossExpression: signal.StopLossExpression,
                                                    clientIds: clientIds
                                                ));

                                            // Add log
                                            _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);
                                        }
                                        else if (signal.OrderType.Equals("BUYLIMIT", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.BuyLimit
                                                (
                                                    signalId: __signal == null ? signal.ID : __signal.ID,
                                                    accountId: signal.AccountID,
                                                    instrument: signal.Instrument,
                                                    strategyId: signal.StrategyID,
                                                    risk: signal.Risk,
                                                    riskToRewardRatio: signal.RiskRewardRatio,
                                                    entryExpression: signal.EntryExpression ?? string.Empty,
                                                    stopLossExpression: signal.StopLossExpression,
                                                    clientIds: clientIds
                                                ));

                                            // Add log
                                            _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);
                                        }
                                        else if (signal.OrderType.Equals("SELLSTOP", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.SellStop
                                                (
                                                    signalId: signal.ID,
                                                    accountId: signal.AccountID,
                                                    instrument: signal.Instrument,
                                                    strategyId: signal.StrategyID,
                                                    risk: signal.Risk,
                                                    riskToRewardRatio: signal.RiskRewardRatio,
                                                    entryExpression: signal.EntryExpression ?? string.Empty,
                                                    stopLossExpression: signal.StopLossExpression,
                                                    clientIds: clientIds
                                                ));

                                            // Add log
                                            _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);
                                        }
                                        else if (signal.OrderType.Equals("SELLLIMIT", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.SellLimit
                                                (
                                                    signalId: signal.ID,
                                                    accountId: signal.AccountID,
                                                    instrument: signal.Instrument,
                                                    strategyId: signal.StrategyID,
                                                    risk: signal.Risk,
                                                    riskToRewardRatio: signal.RiskRewardRatio,
                                                    entryExpression: signal.EntryExpression ?? string.Empty,
                                                    stopLossExpression: signal.StopLossExpression,
                                                    clientIds: clientIds
                                                ));

                                            // Add log
                                            _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);
                                        }
                                    }
                                    else
                                    {
                                        // Add log
                                        _logger.Information($"Signal with magic id {signal.Magic} already in the database from entrylong or entryshort", signal);
                                    }


                                    break;
                                case "entrylong":
                                case "entryshort":
                                case "tphit":
                                case "slhit":
                                case "behit":
                                    // Implement the logic to update the database based on instrument, client, and magic number.
                                    // This is a placeholder for your actual update logic.
                                    var existingSignal = await _dbContext.Signal
                                        .Include(f => f.MarketAbstentions)
                                        .Where(s => s.Instrument == signal.Instrument
                                                    && s.AccountID == signal.AccountID
                                                    && s.Magic == signal.Magic
                                                    && s.StrategyID == signal.StrategyID
                                        )
                                        .OrderByDescending(f => f.DateCreated)
                                        .FirstOrDefaultAsync();


                                    if (existingSignal == null && (signal.OrderType.Equals("entrylong", StringComparison.CurrentCultureIgnoreCase) || signal.OrderType.Equals("entryshort", StringComparison.CurrentCultureIgnoreCase)))
                                    {
                                        // Get the settings from the database
                                        var pair = await _dbContext.ClientPair.Include(f => f.Client).FirstOrDefaultAsync(f => f.Client != null && f.Client.AccountID == signal.AccountID && f.TickerInTradingView.Equals(signal.Instrument) && f.StrategyID == signal.StrategyID);

                                        // Do null reference check
                                        if (pair != null && pair.ExecuteMarketOrderOnEntryIfNoPendingOrders == true)
                                        {
                                            // Add log
                                            _logger.Information($"No stop or limit order found for signal, generate market order", signal);

                                            // Set signal state
                                            signal.SignalStateType = SignalStateType.Entry;
                                            if (signal.OrderType.Equals("entrylong", StringComparison.CurrentCultureIgnoreCase))
                                                signal.OrderType = "BUY";
                                            else
                                                signal.OrderType = "SELL";

                                            // Check if this magic id is already in the database
                                            var __signal = await _dbContext.Signal.FirstOrDefaultAsync(f => f.AccountID == signal.AccountID && f.StrategyID == signal.StrategyID && f.Magic == signal.Magic);

                                            // Do null reference check
                                            if (__signal == null)
                                            {
                                                // If the order type is one of the order types, add the signal to the database
                                                await _dbContext.Signal.AddAsync(signal);

                                                // Save to the database
                                                await _dbContext.SaveChangesAsync();

                                                // Add log
                                                _logger.Information($"Added to database in table Signal with ID: {signal.ID}", signal);
                                            }

                                            // If the previous order is a init, cancel the order

                                            // Generate a BUY order
                                            if (signal.OrderType.Equals("entrylong", StringComparison.CurrentCultureIgnoreCase))
                                            {
                                                var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.Buy
                                                (
                                                    signalId: __signal == null ? signal.ID : __signal.ID,
                                                    accountId: signal.AccountID,
                                                    instrument: signal.Instrument,
                                                    strategyId: signal.StrategyID,
                                                    risk: signal.Risk,
                                                    riskToRewardRatio: signal.RiskRewardRatio,
                                                    stopLossExpression: signal.StopLossExpression,
                                                    clientIds: clientIds
                                                ));

                                                // Add to log
                                                _logger.Information($"Send OnSendTradingviewSignalCommand to client with id {id}", id);
                                            }

                                            // Generate a SELL order
                                            else if (signal.OrderType.Equals("entryshort", StringComparison.CurrentCultureIgnoreCase))
                                            {
                                                var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.Sell
                                                (
                                                    signalId: __signal == null ? signal.ID : __signal.ID,
                                                    accountId: signal.AccountID,
                                                    instrument: signal.Instrument,
                                                    strategyId: signal.StrategyID,
                                                    risk: signal.Risk,
                                                    riskToRewardRatio: signal.RiskRewardRatio,
                                                    stopLossExpression: signal.StopLossExpression,
                                                    clientIds: clientIds
                                                ));

                                                // Add to log
                                                _logger.Information($"Send OnSendTradingviewSignalCommand to client with id {id}", id);
                                            }
                                        }
                                    }
                                    else if (existingSignal == null && (signal.OrderType.Equals("tphit", StringComparison.CurrentCultureIgnoreCase) || signal.OrderType.Equals("slhit", StringComparison.CurrentCultureIgnoreCase) || signal.OrderType.Equals("behit", StringComparison.CurrentCultureIgnoreCase)))
                                    {
                                        // Add log
                                        _logger.Information($"No stop or limit order found for signal with ID: {signal.ID}, generate market order", signal);

                                        // Set signal state
                                        if (signal.OrderType.Equals("tphit", StringComparison.CurrentCultureIgnoreCase))
                                            signal.SignalStateType = SignalStateType.TpHit;
                                        else if (signal.OrderType.Equals("slhit", StringComparison.CurrentCultureIgnoreCase))
                                            signal.SignalStateType = SignalStateType.SlHit;
                                        else if (signal.OrderType.Equals("behit", StringComparison.CurrentCultureIgnoreCase))
                                            signal.SignalStateType = SignalStateType.BeHit;

                                        // If the order type is one of the order types, add the signal to the database
                                        await _dbContext.Signal.AddAsync(signal);

                                        // Save to the database
                                        await _dbContext.SaveChangesAsync();

                                        // Add log
                                        _logger.Information($"Added to database in table Signal with ID: {signal.ID}", signal);
                                    }

                                    if (existingSignal != null)
                                    {
                                        // If is entry
                                        if (signal.OrderType.Equals("entrylong", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            // Create a list to store the items to be removed
                                            var marketAbstentionsToRemove = new List<MarketAbstention>();

                                            // Set flag that you send a message
                                            var flag = false;

                                            // IF this order is of type BUY STOP/LIMIT or SELL STOP/LIMIT
                                            if (existingSignal.OrderType.Equals("BUYSTOP", StringComparison.CurrentCultureIgnoreCase) || existingSignal.OrderType.Equals("BUYLIMIT", StringComparison.CurrentCultureIgnoreCase))
                                            {
                                                // Forech market abstention
                                                foreach (var marketAbstention in existingSignal.MarketAbstentions)
                                                {
                                                    if (marketAbstention.MarketAbstentionType == Models.MarketAbstentionType.ExceptionCalculatingEntryPrice || marketAbstention.MarketAbstentionType == Models.MarketAbstentionType.MetatraderOpenOrderError)
                                                    {
                                                        // Check the flag
                                                        if (flag == false)
                                                        {
                                                            // Create model and send to the client
                                                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.Buy
                                                            (
                                                                signalId: signal.ID,
                                                                accountId: signal.AccountID,
                                                                instrument: signal.Instrument,
                                                                strategyId: signal.StrategyID,
                                                                risk: signal.Risk,
                                                                riskToRewardRatio: signal.RiskRewardRatio,
                                                                stopLossExpression: signal.StopLossExpression,
                                                                clientIds: [marketAbstention.ClientID]
                                                            ));

                                                            // Add log
                                                            _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);

                                                            // Delete abstention
                                                            marketAbstentionsToRemove.Add(marketAbstention);

                                                            // Set flag to true
                                                            flag = true;
                                                        }

                                                    }
                                                }

                                                // If there is no order, and there should be an order and flag is still false

                                            }

                                            // Remove the items from the original collection
                                            foreach (var marketAbstention in marketAbstentionsToRemove)
                                            {
                                                _dbContext.MarketAbstention.Remove(marketAbstention);
                                            }

                                            // Save to the database
                                            await _dbContext.SaveChangesAsync();
                                        }
                                        else if (signal.OrderType.Equals("entryshort", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            // Create a list to store the items to be removed
                                            var marketAbstentionsToRemove = new List<MarketAbstention>();

                                            // Set flag that you send a message
                                            var flag = false;

                                            // IF this order is of type SELL STOP/LIMIT
                                            if (existingSignal.OrderType.Equals("SELLSTOP", StringComparison.CurrentCultureIgnoreCase) || existingSignal.OrderType.Equals("SELLLIMIT", StringComparison.CurrentCultureIgnoreCase))
                                            {
                                                // Forech market abstention
                                                foreach (var marketAbstention in existingSignal.MarketAbstentions)
                                                {
                                                    if (marketAbstention.MarketAbstentionType == Models.MarketAbstentionType.ExceptionCalculatingEntryPrice || marketAbstention.MarketAbstentionType == Models.MarketAbstentionType.MetatraderOpenOrderError)
                                                    {
                                                        // Check the flag
                                                        if (flag == false)
                                                        {
                                                            // Create model and send to the client
                                                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.Sell
                                                            (
                                                                signalId: signal.ID,
                                                                accountId: signal.AccountID,
                                                                instrument: signal.Instrument,
                                                                strategyId: signal.StrategyID,
                                                                risk: signal.Risk,
                                                                riskToRewardRatio: signal.RiskRewardRatio,
                                                                stopLossExpression: signal.StopLossExpression,
                                                                clientIds: [marketAbstention.ClientID]
                                                            ));

                                                            // Add log
                                                            _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);

                                                            // Remove abstention
                                                            marketAbstentionsToRemove.Add(marketAbstention);

                                                            // Set flag
                                                            flag = true;
                                                        }
                                                    }
                                                }

                                                // If there is no order, and there should be an order and flag is still false
                                            }

                                            // Remove the items from the original collection
                                            foreach (var marketAbstention in marketAbstentionsToRemove)
                                            {
                                                _dbContext.MarketAbstention.Remove(marketAbstention);
                                            }

                                            // Save to the database
                                            await _dbContext.SaveChangesAsync();
                                        }

                                        // Update properties based on your logic
                                        if (signal.OrderType.Equals("tphit", StringComparison.CurrentCultureIgnoreCase))
                                            existingSignal.SignalStateType = SignalStateType.TpHit;
                                        else if (signal.OrderType.Equals("slhit", StringComparison.CurrentCultureIgnoreCase))
                                            existingSignal.SignalStateType = SignalStateType.SlHit;
                                        else if (signal.OrderType.Equals("behit", StringComparison.CurrentCultureIgnoreCase))
                                            existingSignal.SignalStateType = SignalStateType.BeHit;
                                        else if (signal.OrderType.Equals("entrylong", StringComparison.CurrentCultureIgnoreCase))
                                            existingSignal.SignalStateType = SignalStateType.Entry;
                                        else if (signal.OrderType.Equals("entryshort", StringComparison.CurrentCultureIgnoreCase))
                                            existingSignal.SignalStateType = SignalStateType.Entry;

                                        // Update
                                        existingSignal.DateLastUpdated = DateTime.UtcNow;
                                        existingSignal.ExitRiskRewardRatio = signal.ExitRiskRewardRatio;

                                        // Update state
                                        _dbContext.Signal.Update(existingSignal);

                                        // Send close order if is there is an open order


                                        // Update database
                                        _logger.Information($"Updated database in table Signal with ID: {existingSignal.ID}", existingSignal);

                                        // Save to the database
                                        await _dbContext.SaveChangesAsync();


                                    }

                                    break;
                                case "close":

                                    var existingSignal3 = await _dbContext.Signal
                                        .Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.StrategyID == signal.StrategyID && s.Magic == signal.Magic)
                                        .FirstOrDefaultAsync();

                                    if (existingSignal3 != null)
                                    {
                                        // Update
                                        existingSignal3.DateLastUpdated = DateTime.UtcNow;
                                        existingSignal3.SignalStateType = SignalStateType.Close;
                                        existingSignal3.ExitRiskRewardRatio = signal.ExitRiskRewardRatio;

                                        // Save to the database
                                        await _dbContext.SaveChangesAsync();

                                        // Update logger
                                        _logger.Information($"Updated database in table Signal with ID: {existingSignal3.ID}", existingSignal3);

                                        // Create model and send to the client
                                        var id2 = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.Close
                                        (
                                            signalId: existingSignal3.ID,
                                            accountId: signal.AccountID,
                                            instrument: signal.Instrument,
                                            strategyId: signal.StrategyID,
                                            clientIds: clientIds
                                        ));

                                        // Add log
                                        _logger.Information($"Send OnSendTradingviewSignalCommand to client with id {id2}", id2);
                                    }
                                    else
                                    {
                                        // Add error to log
                                        _logger.Error($"Error! Could not find Signal with magic: {signal.Magic} in database", signal);
                                    }

                                    break;
                                case "movesltobe":

                                    var existingSignal4 = await _dbContext.Signal
                                                                .Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.StrategyID == signal.StrategyID && s.Magic == signal.Magic)
                                                                .FirstOrDefaultAsync();

                                    if (existingSignal4 != null)
                                    {
                                        // Update
                                        existingSignal4.DateLastUpdated = DateTime.UtcNow;
                                        existingSignal4.SignalStateType = SignalStateType.MoveSlToBe;
                                        existingSignal4.ExitRiskRewardRatio = signal.ExitRiskRewardRatio;

                                        // Save to the database
                                        await _dbContext.SaveChangesAsync();

                                        // Update logger
                                        _logger.Information($"Updated database in table Signal with ID: {existingSignal4.ID}", existingSignal4);

                                        // Create model and send to the client
                                        var id4 = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.MoveSLtoBE
                                        (
                                            signalId: existingSignal4.ID,
                                            accountId: signal.AccountID,
                                            instrument: signal.Instrument,
                                            strategyId: signal.StrategyID,
                                            clientIds: clientIds
                                        ));

                                        // Add log
                                        _logger.Information($"Send OnSendTradingviewSignalCommand to client with id {id4}", id4);
                                    }
                                    else
                                    {
                                        // Add error to log
                                        _logger.Error($"Error! Could not find Signal with magic: {signal.Magic} in database", signal);
                                    }

                                    break;
                                case "closeall":

                                    var existingSignal2 = await _dbContext.Signal
                                        .Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.StrategyID == signal.StrategyID)
                                        .OrderByDescending(f => f.DateCreated)
                                        .FirstOrDefaultAsync();
                                    ;

                                    if (existingSignal2 != null)
                                    {
                                        // Update
                                        existingSignal2.DateLastUpdated = DateTime.UtcNow;
                                        existingSignal2.SignalStateType = SignalStateType.CloseAll;
                                        existingSignal2.ExitRiskRewardRatio = signal.ExitRiskRewardRatio;

                                        // Save to the database
                                        await _dbContext.SaveChangesAsync();

                                        // Update logger
                                        _logger.Information($"Updated database in table Signal with ID: {existingSignal2.ID}", existingSignal2);

                                        // Create model and send to the client
                                        var id3 = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.CloseAll
                                        (
                                            signalId: existingSignal2.ID,
                                            accountId: signal.AccountID,
                                            instrument: signal.Instrument,
                                            strategyId: signal.StrategyID,
                                            clientIds: clientIds
                                        ));

                                        // Add log
                                        _logger.Information($"Send OnSendTradingviewSignalCommand to client with id {id3}", id3);
                                    }
                                    else
                                    {
                                        // Add error to log
                                        _logger.Error($"Error! Could not find Signal with magic: {signal.Magic} in database", signal);
                                    }

                                    break;
                                case "cancel":
                                    var existingSignal5 = await _dbContext.Signal
                                                                                .Where(s => s.Instrument == signal.Instrument
                                                                                            && s.AccountID == signal.AccountID
                                                                                            && s.StrategyID == signal.StrategyID
                                                                                            && s.Magic == signal.Magic
                                                                                )
                                                                                .OrderByDescending(f => f.DateCreated)
                                                                                .FirstOrDefaultAsync();
                                    if (existingSignal5 != null)
                                    {
                                        // Only cancel passive orders, don't cancel active orders (else it will close the trade)
                                        if (signal.OrderType.Equals("buystop", StringComparison.CurrentCultureIgnoreCase)
                                                || signal.OrderType.Equals("buylimit", StringComparison.CurrentCultureIgnoreCase)
                                                || signal.OrderType.Equals("selllimit", StringComparison.CurrentCultureIgnoreCase)
                                                || signal.OrderType.Equals("sellstop", StringComparison.CurrentCultureIgnoreCase)
                                                )
                                        {

                                            // Update
                                            existingSignal5.DateLastUpdated = DateTime.UtcNow;
                                            existingSignal5.SignalStateType = SignalStateType.Cancel;
                                            existingSignal5.ExitRiskRewardRatio = null;

                                            // Save to the database
                                            await _dbContext.SaveChangesAsync();

                                            // Update logger
                                            _logger.Information($"Updated database in table Signal with ID: {existingSignal5.ID}", existingSignal5);

                                            // Create model and send to the client
                                            var id4 = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, OnSendTradingviewSignalCommand.Cancel
                                            (
                                                signalId: existingSignal5.ID,
                                                accountId: signal.AccountID,
                                                instrument: signal.Instrument,
                                                strategyId: signal.StrategyID,
                                                clientIds: clientIds
                                            ));

                                            // Add log
                                            _logger.Information($"Send OnSendTradingviewSignalCommand to client with id {id4}", id4);
                                        }
                                        {
                                            // Add error to log
                                            _logger.Error($"Error! Cancel order without previous passive order in the market: {signal.Magic} in database", signal);
                                        }
                                    }
                                    else
                                    {
                                        // Add error to log
                                        _logger.Error($"Error! Could not find Signal with magic: {signal.Magic} in database", signal);
                                    }
                                    break;
                                default:
                                    // Optionally, handle unknown order types
                                    _logger.Error($"Error! Unknown or not used ordertype: {signal.OrderType}");

                                    // Return bad request
                                    if (string.IsNullOrEmpty(signal.OrderType))
                                    {
                                        return;
                                    }

                                    break;
                            }
                        }
                        else
                        {
                            _logger.Information($"Signal already exist in the database {JsonConvert.SerializeObject(signal)}", signal);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Exception: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
                        return;
                    }
                }
            }
        }
    }
}
