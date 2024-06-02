using JCTG.Command;
using JCTG.Entity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using JCTG.WebApp.Backend.Queue;


namespace JCTG.WebApp.Backend.Api
{
    [ApiController]
    [Route("api")]
    public class TradingviewController : ControllerBase
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<TradingviewController>();
        private readonly JCTGDbContext _dbContext;
        private readonly AzureQueueClient _server;

        public TradingviewController(JCTGDbContext dbContext, AzureQueueClient server)
        {
            _dbContext = dbContext;
            _server = server;
        }

        [HttpGet("tradingview")]
        [HttpPost("tradingview")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TradingView()
        {
            var code = Request.Query["code"];

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

            _logger.Debug($"Request body: {requestBody}");


            if (!string.IsNullOrEmpty(requestBody))
            {
                try
                {
                    var signal = Signal.Parse(requestBody);

                    _logger.Information($"Parsed object to Signal : {JsonConvert.SerializeObject(signal)}", signal);

                    // Check the order type of the signal
                    switch (signal.OrderType.ToLower())
                    {
                        case "buy":
                        case "buystop":
                        case "buylimit":
                        case "sell":
                        case "selllimit":
                        case "sellstop":

                            // Check if previous signal of this strategy is set as init (if -> set as cancelled)
                            if (signal.OrderType.Equals("buystop", StringComparison.CurrentCultureIgnoreCase)
                                 || signal.OrderType.Equals("buylimit", StringComparison.CurrentCultureIgnoreCase)
                                 || signal.OrderType.Equals("selllimit", StringComparison.CurrentCultureIgnoreCase)
                                 || signal.OrderType.Equals("sellstop", StringComparison.CurrentCultureIgnoreCase)
                                )
                            {
                                var prevSignal = await _dbContext.Signal.Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.OrderType == signal.OrderType && s.StrategyType == signal.StrategyType).OrderByDescending(f => f.DateCreated).FirstOrDefaultAsync();

                                // do null reference check
                                if (prevSignal != null)
                                {
                                    if (prevSignal.TradingviewStateType == TradingviewStateType.Init)
                                        prevSignal.TradingviewStateType = TradingviewStateType.CancelOrder;
                                }
                            }


                            if (signal.OrderType.Equals("buy", StringComparison.CurrentCultureIgnoreCase))
                                signal.TradingviewStateType = TradingviewStateType.Entry;
                            else if (signal.OrderType.Equals("buystop", StringComparison.CurrentCultureIgnoreCase))
                                signal.TradingviewStateType = TradingviewStateType.Init;
                            else if (signal.OrderType.Equals("buylimit", StringComparison.CurrentCultureIgnoreCase))
                                signal.TradingviewStateType = TradingviewStateType.Init;
                            else if (signal.OrderType.Equals("sell", StringComparison.CurrentCultureIgnoreCase))
                                signal.TradingviewStateType = TradingviewStateType.Entry;
                            else if (signal.OrderType.Equals("selllimit", StringComparison.CurrentCultureIgnoreCase))
                                signal.TradingviewStateType = TradingviewStateType.Init;
                            else if (signal.OrderType.Equals("sellstop", StringComparison.CurrentCultureIgnoreCase))
                                signal.TradingviewStateType = TradingviewStateType.Init;

                            // If the order type is one of the order types, add the signal to the database
                            await _dbContext.Signal.AddAsync(signal);

                            // Save to the database
                            await _dbContext.SaveChangesAsync();

                            // Add to the tradingview alert
                            await _dbContext.TradingviewAlert.AddAsync(new TradingviewAlert()
                            {
                                DateCreated = DateTime.UtcNow,
                                RawMessage = requestBody,
                                SignalID = signal.ID,
                                Type = TradingviewAlert.ParseTradingviewAlertTypeOrDefault(signal.OrderType.ToLower()),
                            });

                            // Save to the database
                            await _dbContext.SaveChangesAsync();

                            // Add log
                            _logger.Information($"Added to database in table Signal with ID: {signal.ID}", signal);

                            // Create model and send to the client
                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, new OnSendTradingviewSignalCommand()
                            {
                                SignalID = signal.ID,
                                AccountID = signal.AccountID,
                                Instrument = signal.Instrument,
                                Magic = signal.ID,
                                OrderType = signal.OrderType,
                                StrategyType = signal.StrategyType,
                                MarketOrder = signal.OrderType == "BUY" || signal.OrderType == "SELL" ? new OnReceivingTradingviewSignalEventMarketOrder()
                                {
                                    StopLoss = Convert.ToDecimal(signal.StopLoss),
                                    Price = Convert.ToDecimal(signal.EntryPrice),
                                    TakeProfit = Convert.ToDecimal(signal.TakeProfit),
                                } : null,
                                PassiveOrder = signal.OrderType == "BUYSTOP" || signal.OrderType == "SELLSTOP" ? new OnReceivingTradingviewSignalEventPassiveOrder()
                                {
                                    EntryExpression = signal.EntryExpression,
                                    Risk = Convert.ToDecimal(signal.Risk),
                                    RiskRewardRatio = Convert.ToDecimal(signal.RiskRewardRatio),
                                } : null,
                            });

                            // Add log
                            _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);
                            break;
                        case "entry":
                        case "movesltobe":
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
                                            && s.StrategyType == signal.StrategyType
                                )
                                .OrderByDescending(f => f.DateCreated)
                                .FirstOrDefaultAsync();

                            if(existingSignal != null) 
                            {
                                // If is entry
                                if (signal.OrderType.Equals("entry", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    // Create a list to store the items to be removed
                                    var marketAbstentionsToRemove = new List<MarketAbstention>();

                                    // IF this order is of type BUY STOP/LIMIT or SELL STOP/LIMIT
                                    if (existingSignal.OrderType.Equals("buystop", StringComparison.CurrentCultureIgnoreCase) || existingSignal.OrderType.Equals("buylimit", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        foreach(var marketAbstention in existingSignal.MarketAbstentions)
                                        {
                                            if(marketAbstention.MarketAbstentionType == Models.MarketAbstentionType.ExceptionCalculatingEntryPrice)
                                            {
                                                // Create model and send to the client
                                                id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, new OnSendTradingviewSignalCommand()
                                                {
                                                    SignalID = signal.ID,
                                                    AccountID = signal.AccountID,
                                                    ClientIDs = [marketAbstention.ClientID],
                                                    Instrument = signal.Instrument,
                                                    Magic = signal.ID,
                                                    OrderType = "BUY",
                                                    StrategyType = signal.StrategyType,
                                                    MarketOrder = new OnReceivingTradingviewSignalEventMarketOrder()
                                                    {
                                                        StopLoss = Convert.ToDecimal(signal.StopLoss),
                                                        Price = Convert.ToDecimal(signal.EntryPrice),
                                                        TakeProfit = Convert.ToDecimal(signal.TakeProfit),
                                                    },
                                                });

                                                // Add log
                                                _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);

                                                marketAbstentionsToRemove.Add(marketAbstention);
                                            }
                                        }
                                    }
                                    else if (existingSignal.OrderType.Equals("sellstop", StringComparison.CurrentCultureIgnoreCase) || existingSignal.OrderType.Equals("selllimit", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        foreach (var marketAbstention in existingSignal.MarketAbstentions)
                                        {
                                            if (marketAbstention.MarketAbstentionType == Models.MarketAbstentionType.ExceptionCalculatingEntryPrice)
                                            {
                                                // Create model and send to the client
                                                id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, new OnSendTradingviewSignalCommand()
                                                {
                                                    SignalID = signal.ID,
                                                    AccountID = signal.AccountID,
                                                    ClientIDs = [marketAbstention.ClientID],
                                                    Instrument = signal.Instrument,
                                                    Magic = signal.ID,
                                                    OrderType = "SELL",
                                                    StrategyType = signal.StrategyType,
                                                    MarketOrder = new OnReceivingTradingviewSignalEventMarketOrder()
                                                    {
                                                        StopLoss = Convert.ToDecimal(signal.StopLoss),
                                                        Price = Convert.ToDecimal(signal.EntryPrice),
                                                        TakeProfit = Convert.ToDecimal(signal.TakeProfit),
                                                    },
                                                });

                                                // Add log
                                                _logger.Information($"Sent to Azure Queue with response client request id: {id}", id);

                                                marketAbstentionsToRemove.Add(marketAbstention);
                                            }
                                        }
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
                                // For example: existingSignal.Status = "Updated";
                                if (signal.OrderType.Equals("tphit", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.TradingviewStateType = TradingviewStateType.TpHit;
                                else if (signal.OrderType.Equals("slhit", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.TradingviewStateType = TradingviewStateType.SlHit;
                                else if (signal.OrderType.Equals("behit", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.TradingviewStateType = TradingviewStateType.BeHit;
                                else if (signal.OrderType.Equals("entry", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.TradingviewStateType = TradingviewStateType.Entry;

                                // Update
                                existingSignal.DateLastUpdated = DateTime.UtcNow;
                                existingSignal.ExitRiskRewardRatio = signal.ExitRiskRewardRatio;

                                // Update state
                                _dbContext.Signal.Update(existingSignal);

                                // Add to the tradingview alert
                                await _dbContext.TradingviewAlert.AddAsync(new TradingviewAlert()
                                {
                                    DateCreated = DateTime.UtcNow,
                                    RawMessage = requestBody,
                                    SignalID = existingSignal.ID,
                                    Type = TradingviewAlert.ParseTradingviewAlertTypeOrDefault(signal.OrderType.ToLower()),
                                });

                                // Update database
                                _logger.Information($"Updated database in table Signal with ID: {existingSignal.ID}", existingSignal);

                                // Save to the database
                                await _dbContext.SaveChangesAsync();

                               
                            }
                            else
                            {
                                // Add error to log
                                _logger.Error($"Error! Could not find Signal with magic: {signal.Magic} in database", signal);
                            }

                            break;
                        case "closeall":
                            // Implement the logic to update the database based on instrument, client, and magic number.
                            // This is a placeholder for your actual update logic.
                            var existingSignal2 = await _dbContext.Signal
                                .Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.StrategyType == signal.StrategyType)
                                .OrderByDescending(f => f.DateCreated)
                                .FirstOrDefaultAsync();
                                ;

                            if(existingSignal2 != null) 
                            { 
                                if (existingSignal2.TradingviewStateType == TradingviewStateType.Entry)
                                {
                                    existingSignal2.TradingviewStateType = TradingviewStateType.CloseAll;
                                    existingSignal2.DateLastUpdated = DateTime.UtcNow;
                                    existingSignal2.ExitRiskRewardRatio = signal.ExitRiskRewardRatio;

                                    // Update state
                                    _dbContext.Signal.Update(existingSignal2);

                                    // Add to the tradingview alert
                                    await _dbContext.TradingviewAlert.AddAsync(new TradingviewAlert()
                                    {
                                        DateCreated = DateTime.UtcNow,
                                        RawMessage = requestBody,
                                        SignalID = existingSignal2.ID,
                                        Type = TradingviewAlert.ParseTradingviewAlertTypeOrDefault(signal.OrderType.ToLower()),
                                    });


                                    // Update database
                                    _logger.Information($"Updated database in table Signal with ID: {existingSignal2.ID}", existingSignal2);
                                }
                                else if (existingSignal2.TradingviewStateType == TradingviewStateType.Init)
                                {
                                    existingSignal2.TradingviewStateType = TradingviewStateType.CancelOrder;
                                    existingSignal2.DateLastUpdated = DateTime.UtcNow;
                                    existingSignal2.ExitRiskRewardRatio = signal.ExitRiskRewardRatio;

                                    // Update state
                                    _dbContext.Signal.Update(existingSignal2);

                                    // Add to the tradingview alert
                                    await _dbContext.TradingviewAlert.AddAsync(new TradingviewAlert()
                                    {
                                        DateCreated = DateTime.UtcNow,
                                        RawMessage = requestBody,
                                        SignalID = existingSignal2.ID,
                                        Type = TradingviewAlert.ParseTradingviewAlertTypeOrDefault(signal.OrderType.ToLower()),
                                    });


                                    // Update database
                                    _logger.Information($"Updated database in table Signal with ID: {existingSignal2.ID}", existingSignal2);
                                }

                                // Update
                                existingSignal2.TradingviewStateType = TradingviewStateType.CloseAll;

                                // Save to the database
                                await _dbContext.SaveChangesAsync();

                                // Update logger
                                _logger.Information($"Updated database in table Signal with ID: {existingSignal2.ID}", existingSignal2);
                            }
                            else
                            {
                                // Add error to log
                                _logger.Error($"Error! Could not find Signal with magic: {signal.Magic} in database", signal);
                            }

                            break;
                        case "cancelorder":
                            break;
                        default:
                            // Optionally, handle unknown order types
                            _logger.Error($"Error! Unknown or not used ordertype: {signal.OrderType}");
                            break;
                    }



                }
                catch (Exception ex)
                {
                    _logger.Error($"Exception: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
                    return StatusCode(500, "Internal server error");
                }
            }

            return Ok("Processed successfully");
        }
    }
}
