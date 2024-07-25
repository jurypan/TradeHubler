using JCTG.Command;
using JCTG.Entity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using JCTG.WebApp.Backend.Queue;
using Microsoft.AspNetCore.Authorization;
using JCTG.Models;


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

        [AllowAnonymous]
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

                    // ALWAYS MANDATORY
                    if (string.IsNullOrEmpty(signal.Instrument))
                    {
                        _logger.Error($"'instrument' is mandatory for {signal.OrderType}");
                        return BadRequest("'instrument' is mandatory");
                    }

                    if (signal.AccountID == 0)
                    {
                        _logger.Error($"'account_id' is mandatory for {signal.OrderType}");
                        return BadRequest("'account_id' is mandatory");
                    }

                    if (string.IsNullOrEmpty(signal.OrderType))
                    {
                        _logger.Error($"'order_type' is mandatory for {signal.OrderType}");
                        return BadRequest("'order_type' is mandatory");
                    }

                    if (signal.StrategyID == 0)
                    {
                        _logger.Error($"'strategy' is mandatory for {signal.OrderType}");
                        return BadRequest("'strategy' is mandatory");
                    }

                    if (signal.Magic == 0)
                    {
                        _logger.Error($"'magic' is mandatory for {signal.OrderType}");
                        return BadRequest("'magic' is mandatory");
                    }

                    // MARKET ORDERS + PASSIVE ORDERS
                    switch (signal.OrderType.ToLower())
                    {
                        case "buy":
                        case "buystop":
                        case "buylimit":
                        case "sell":
                        case "selllimit":
                        case "sellstop":
                            if (signal.Risk == 0)
                            {
                                _logger.Error($"'risk' is mandatory for {signal.OrderType}");
                                return BadRequest("'risk' is mandatory");
                            }

                            if (signal.RiskRewardRatio == 0)
                            {
                                _logger.Error($"'rr' is mandatory for {signal.OrderType}");
                                return BadRequest("'rr' is mandatory");
                            }

                            if (!signal.EntryPrice.HasValue)
                            {
                                _logger.Error($"'entryprice' is mandatory for {signal.OrderType}");
                                return BadRequest("'entryprice' is mandatory");
                            }
                            break;
                        default:
                            break;
                    }

                    // PASSIVE ORDERS
                    switch (signal.OrderType.ToLower())
                    {
                        case "buystop":
                        case "buylimit":
                        case "selllimit":
                        case "sellstop":
                            if (string.IsNullOrEmpty(signal.EntryExpression))
                            {
                                _logger.Error($"'entryexpr' is mandatory for {signal.OrderType}");
                                return BadRequest("'entryexpr' is mandatory");
                            }

                            break;
                        default:
                            break;
                    }

                    // CANCEL ORDER
                    switch (signal.OrderType.ToLower())
                    {
                        case "tphit":
                        case "slhit":
                        case "behit":
                        case "close":
                        case "closeall":
                        case "cancelorder":
                            if (!signal.ExitRiskRewardRatio.HasValue)
                            {
                                _logger.Error($"'exitrr' is mandatory for {signal.OrderType}");
                                return BadRequest("'exitrr' is mandatory");
                            }
                            break;
                        case "entry":
                        case "movesltobe":
                        default:
                            break;
                    }




                    // Action!
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
                                // Cancel potential previous  trades
                                var prevSignal = await _dbContext.Signal.Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.OrderType == signal.OrderType && s.StrategyID == signal.StrategyID).OrderByDescending(f => f.DateCreated).FirstOrDefaultAsync();

                                // do null reference check
                                if (prevSignal != null)
                                {
                                    if (prevSignal.SignalStateType == SignalStateType.Init)
                                    {
                                        prevSignal.SignalStateType = SignalStateType.CancelOrder;
                                        prevSignal.ExitRiskRewardRatio = 0;
                                    }
                                }
                            }


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
                                StrategyType = (StrategyType)signal.StrategyID,
                                MarketOrder = signal.OrderType == "BUY" || signal.OrderType == "SELL" ? new OnReceivingMarketOrder()
                                {
                                    Risk = Convert.ToDecimal(signal.Risk),
                                    RiskRewardRatio = Convert.ToDecimal(signal.RiskRewardRatio)
                                } : null,
                                PassiveOrder = signal.OrderType == "BUYSTOP" || signal.OrderType == "SELLSTOP" ? new OnReceivingPassiveOrder()
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
                                            && s.StrategyID == signal.StrategyID
                                )
                                .OrderByDescending(f => f.DateCreated)
                                .FirstOrDefaultAsync();

                            if (existingSignal != null)
                            {
                                // If is entry
                                if (signal.OrderType.Equals("entry", StringComparison.CurrentCultureIgnoreCase))
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
                                                    id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, new OnSendTradingviewSignalCommand()
                                                    {
                                                        SignalID = signal.ID,
                                                        AccountID = signal.AccountID,
                                                        ClientIDs = [marketAbstention.ClientID],
                                                        Instrument = signal.Instrument,
                                                        Magic = signal.ID,
                                                        OrderType = "BUY",
                                                        StrategyType = (StrategyType)signal.StrategyID,
                                                        MarketOrder = new OnReceivingMarketOrder()
                                                        {
                                                            Risk = Convert.ToDecimal(signal.Risk),
                                                            RiskRewardRatio = Convert.ToDecimal(signal.RiskRewardRatio),
                                                        },
                                                    });

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
                                    else if (existingSignal.OrderType.Equals("SELLSTOP", StringComparison.CurrentCultureIgnoreCase) || existingSignal.OrderType.Equals("SELLLIMIT", StringComparison.CurrentCultureIgnoreCase))
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
                                                    id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, new OnSendTradingviewSignalCommand()
                                                    {
                                                        SignalID = signal.ID,
                                                        AccountID = signal.AccountID,
                                                        ClientIDs = [marketAbstention.ClientID],
                                                        Instrument = signal.Instrument,
                                                        Magic = signal.ID,
                                                        OrderType = "SELL",
                                                        StrategyType = (StrategyType)signal.StrategyID,
                                                        MarketOrder = new OnReceivingMarketOrder()
                                                        {
                                                            Risk = Convert.ToDecimal(signal.Risk),
                                                            RiskRewardRatio = Convert.ToDecimal(signal.RiskRewardRatio),
                                                        },
                                                    });

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
                                // For example: existingSignal.Status = "Updated";
                                if (signal.OrderType.Equals("tphit", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.SignalStateType = SignalStateType.TpHit;
                                else if (signal.OrderType.Equals("slhit", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.SignalStateType = SignalStateType.SlHit;
                                else if (signal.OrderType.Equals("behit", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.SignalStateType = SignalStateType.BeHit;
                                else if (signal.OrderType.Equals("entry", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.SignalStateType = SignalStateType.Entry;

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
                        case "close":
                            // Implement the logic to update the database based on instrument, client, and magic number.
                            // This is a placeholder for your actual update logic.
                            var existingSignal3 = await _dbContext.Signal
                                .Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.StrategyID == signal.StrategyID)
                                .OrderByDescending(f => f.DateCreated)
                                .FirstOrDefaultAsync();
                            ;

                            if (existingSignal3 != null)
                            {
                                // Add to the tradingview alert
                                await _dbContext.TradingviewAlert.AddAsync(new TradingviewAlert()
                                {
                                    DateCreated = DateTime.UtcNow,
                                    RawMessage = requestBody,
                                    SignalID = existingSignal3.ID,
                                    Type = TradingviewAlert.ParseTradingviewAlertTypeOrDefault(signal.OrderType.ToLower()),
                                });

                                // Update
                                existingSignal3.SignalStateType = SignalStateType.Close;
                                existingSignal3.ExitRiskRewardRatio = signal.ExitRiskRewardRatio;

                                // Save to the database
                                await _dbContext.SaveChangesAsync();

                                // Update logger
                                _logger.Information($"Updated database in table Signal with ID: {existingSignal3.ID}", existingSignal3);

                                // Create model and send to the client
                                var id2 = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, new OnSendTradingviewSignalCommand()
                                {
                                    SignalID = existingSignal3.ID,
                                    AccountID = signal.AccountID,
                                    Instrument = signal.Instrument,
                                    Magic = existingSignal3.ID,
                                    OrderType = signal.OrderType.ToUpper(),
                                    StrategyType = (StrategyType)signal.StrategyID
                                });
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
                                .Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.StrategyID == signal.StrategyID)
                                .OrderByDescending(f => f.DateCreated)
                                .FirstOrDefaultAsync();
                            ;

                            if (existingSignal2 != null)
                            {
                                if (existingSignal2.SignalStateType == SignalStateType.Entry)
                                {
                                    existingSignal2.SignalStateType = SignalStateType.CloseAll;
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
                                else if (existingSignal2.SignalStateType == SignalStateType.Init)
                                {
                                    existingSignal2.SignalStateType = SignalStateType.CancelOrder;
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
                                existingSignal2.SignalStateType = SignalStateType.CloseAll;
                                existingSignal2.ExitRiskRewardRatio = signal.ExitRiskRewardRatio;

                                // Save to the database
                                await _dbContext.SaveChangesAsync();

                                // Update logger
                                _logger.Information($"Updated database in table Signal with ID: {existingSignal2.ID}", existingSignal2);

                                // Create model and send to the client
                                var id3 = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, new OnSendTradingviewSignalCommand()
                                {
                                    SignalID = existingSignal2.ID,
                                    AccountID = signal.AccountID,
                                    Instrument = signal.Instrument,
                                    Magic = existingSignal2.ID,
                                    OrderType = signal.OrderType.ToUpper(),
                                    StrategyType = (StrategyType)signal.StrategyID
                                });
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

                            // Return bad request
                            if (string.IsNullOrEmpty(signal.OrderType))
                            {
                                return BadRequest("wrong 'order_type'");
                            }

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
