using JCTG.Entity;
using JCTG.Events;
using JCTG.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace JCTG.WebApp.Backend.Api
{
    [ApiController]
    [Route("api")]
    public class TerminalController(JCTGDbContext dbContext) : ControllerBase
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<TerminalController>();

        [HttpGet("TerminalConfig")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult TerminalConfig()
        {
            // Security 
            var code = Request.Query["code"];

            if (code != "Ocebxtg1excWosFez5rWMtNp3ZsmIzSFQ0XhqtrfHlMuAzFuQ0OGhA==")
            {
                _logger.Debug("code is not ok");
                return BadRequest();
            }

            // Attempt to parse accountId from the query string as a int
            if (!int.TryParse(Request.Query["accountid"].ToString(), out int accountId))
            {
                _logger.Debug("accountId is not in a valid format.");
                return BadRequest();
            }

            // Init retour
            var retour = new TerminalConfig()
            {
                AccountId = accountId,
                Brokers = dbContext.Client
                                        .Where(f => f.AccountID == accountId)
                                        .Include(f => f.Pairs)
                                        .Include(f => f.Risks)
                                        .Select(f => new Brokers()
                                        {
                                            ClientId = f.ID,
                                            IsEnable = f.IsEnable,
                                            MetaTraderDirPath = f.MetaTraderDirPath,
                                            Name = f.Name,
                                            Pairs = f.Pairs.OrderBy(f => f.TickerInMetatrader).Select(p => new Pairs()
                                            {
                                                TickerInMetatrader = p.TickerInMetatrader,
                                                TickerInTradingView = p.TickerInTradingView,
                                                Timeframe = p.Timeframe,
                                                CancelStopOrLimitOrderWhenNewSignal = p.CancelStopOrLimitOrderWhenNewSignal,
                                                CloseAllTradesAt = p.CloseAllTradesAt == null ? null : TimeSpan.Parse(p.CloseAllTradesAt),
                                                CorrelatedPairs = new List<string>(),
                                                DoNotOpenTradeXMinutesBeforeClose = p.DoNotOpenTradeXMinutesBeforeClose,
                                                MaxLotSize = Convert.ToInt32(p.MaxLotSize),
                                                MaxSpread = Convert.ToDecimal(p.MaxSpread),
                                                NumberOfHistoricalBarsRequested = p.NumberOfHistoricalBarsRequested,
                                                OrderExecType = p.OrderExecType,
                                                Risk = Convert.ToDecimal(p.Risk),
                                                RiskMinXTimesTheSpread = p.RiskMinXTimesTheSpread,
                                                SLMultiplier = p.SLMultiplier,
                                                SLtoBEafterR = p.SLtoBEafterR,
                                                SpreadEntry = p.SpreadEntry,
                                                SpreadSL = p.SpreadSL,
                                                SpreadTP = p.SpreadTP,
                                                StrategyNr = p.StrategyType
                                            }).ToList(),
                                            Risk = f.Risks.OrderBy(f => f.Procent).Select(r => new Risk()
                                            {
                                                Multiplier = r.Multiplier,
                                                Procent = r.Procent,
                                            }).ToList(),
                                            StartBalance = f.StartBalance,
                                        }).ToList(),
                Debug = true,
                DropLogsInFile = true,
                LoadOrdersFromFile = true,
                MaxRetryCommandSeconds = 10,
                SleepDelay = 250,
            };


            return Ok(retour);
        }

        [HttpPost("OnOrderCreatedEvent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OnOrderCreatedEvent([FromBody] OnOrderCreatedEvent model)
        {

            // Log
            _logger.Debug($"On order create event triggered: {JsonConvert.SerializeObject(model)}");

            // Null reference check
            if (model != null && model.ClientID > 0 && model.SignalID > 0)
            {
                // Log
                _logger.Debug($"Signal id is {model.SignalID}", model);

                // Check trade order
                var order = await dbContext.Order.FirstOrDefaultAsync(f => f.ClientID == model.ClientID && f.SignalID == model.SignalID);

                // Check for duplicates
                if (order == null)
                {
                    // Log
                    _logger.Debug($"Order not found");

                    // Deal order
                    order = new Entity.Order()
                    {
                        DateCreated = DateTime.UtcNow,
                        IsTradeClosed = false,
                        SignalID = model.SignalID,
                        ClientID = model.ClientID,
                        Symbol = model.Order.Symbol ?? string.Empty,
                        Type = model.Order.Type ?? string.Empty,
                        OpenLots = decimal.ToDouble(model.Order.Lots),
                        OpenPrice = decimal.ToDouble(model.Order.OpenPrice),
                        OpenTime = model.Order.OpenTime,
                        OpenStopLoss = decimal.ToDouble(model.Order.StopLoss),
                        OpenTakeProfit = decimal.ToDouble(model.Order.TakeProfit),
                        Comment = model.Order.Comment,
                        Magic = model.Order.Magic,
                    };
                    await dbContext.Order.AddAsync(order);
                }

                // Log
                var log = new Entity.Log()
                {
                    SignalID = model.SignalID,
                    ClientID = model.ClientID,
                    Description = model.Log.Description,
                    ErrorType = model.Log.ErrorType,
                    Message = model.Log.Message,
                    Time = model.Log.Time,
                    Type = model.Log.Type,
                }
                ;
                await dbContext.Log.AddAsync(log);

                // Save
                await dbContext.SaveChangesAsync();

                // Log
                _logger.Information($"Saved order in database with id {order.ID}");
            }

            return Ok();
        }

        [HttpPost("OnOrderUpdatedEvent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OnOrderUpdatedEvent([FromBody] OnOrderUpdatedEvent model)
        {

            // Log
            _logger.Debug($"On order update event triggered: {JsonConvert.SerializeObject(model)}");

            // Null reference check
            if (model != null && model.ClientID > 0 && model.Order != null && model.SignalID > 0)
            {
                // Log
                _logger.Debug($"Signal id is {model.SignalID}", model);

                // Get the trade order from the database
                var order = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == model.SignalID && f.ClientID == model.ClientID);

                // Do null reference check
                if (order != null)
                {
                    // Log
                    _logger.Debug($"Order found, update SL {decimal.ToDouble(model.Order.StopLoss)} and TP {decimal.ToDouble(model.Order.TakeProfit)}", order);

                    // Update entity
                    order.CloseStopLoss = decimal.ToDouble(model.Order.StopLoss);
                    order.CloseTakeProfit = decimal.ToDouble(model.Order.TakeProfit);

                    // Log
                    var log = new Entity.Log()
                    {
                        SignalID = model.SignalID,
                        ClientID = model.ClientID,
                        Description = model.Log.Description,
                        ErrorType = model.Log.ErrorType,
                        Message = model.Log.Message,
                        Time = model.Log.Time,
                        Type = model.Log.Type,
                    };
                    await dbContext.Log.AddAsync(log);

                    // Save
                    await dbContext.SaveChangesAsync();

                    // Log
                    _logger.Debug($"Saved database");
                }
                else
                {
                    // Log
                    _logger.Error($"Order not foudn with signal id {model.SignalID}");
                }
            }
            return Ok();
        }

        [HttpPost("OnOrderClosedEvent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OnOrderClosedEvent([FromBody] OnOrderClosedEvent model)
        {
            // Log
            _logger.Debug($"On order close event triggered: {JsonConvert.SerializeObject(model)}");

            // Do null reference check
            if (model != null && model.ClientID > 0 && model.SignalID > 0)
            {
                // Log
                _logger.Debug($"Signal id is {model.SignalID}", model);

                // Get the trade order from the database
                var order = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == model.SignalID && f.ClientID == model.ClientID);

                // Do null reference check
                if (order != null)
                {
                    // Log
                    _logger.Debug($"Order found, update Price {decimal.ToDouble(model.ClosePrice)}, update SL {decimal.ToDouble(model.Order.StopLoss)} and TP {decimal.ToDouble(model.Order.TakeProfit)}", order);

                    // Update close properties
                    order.ClosePrice = decimal.ToDouble(model.ClosePrice);
                    order.CloseStopLoss = decimal.ToDouble(model.Order.StopLoss);
                    order.CloseTakeProfit = decimal.ToDouble(model.Order.TakeProfit);

                    // Log
                    var log = new Entity.Log()
                    {
                        SignalID = model.SignalID,
                        ClientID = model.ClientID,
                        Description = model.Log.Description,
                        ErrorType = model.Log.ErrorType,
                        Message = model.Log.Message,
                        Time = model.Log.Time,
                        Type = model.Log.Type,
                    };
                    await dbContext.Log.AddAsync(log);

                    // Save
                    await dbContext.SaveChangesAsync();

                    // Log
                    _logger.Debug($"Saved database");
                }
                else
                {
                    // Log
                    _logger.Error($"Order not found with signal id {model.SignalID}");
                }
            }

            return Ok();
        }

        [HttpPost("OnOrderAutoMoveSlToBeEvent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OnOrderAutoMoveSlToBeEvent([FromBody] OnOrderAutoMoveSlToBeEvent model)
        {
            // Log
            _logger.Debug($"On auto move SL to BE event triggered: {JsonConvert.SerializeObject(model)}");

            // Do null reference check
            if (model != null && model.ClientID > 0 && model.SignalID > 0)
            {
                // Log
                _logger.Debug($"Signal id is {model.SignalID}", model);

                // Get the trade order from the database
                var order = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == model.SignalID && f.ClientID == model.ClientID);

                // Do null reference check
                if (order != null)
                {
                    // Log
                    _logger.Debug($"Order found, update SL {decimal.ToDouble(model.StopLossPrice)}", order);

                    // Update entity
                    order.CloseStopLoss = decimal.ToDouble(model.StopLossPrice);
                }
                else
                {
                    // Log
                    _logger.Error($"Order not found with signal id {model.SignalID}");
                }

                // Log
                var log = new Entity.Log()
                {
                    SignalID = model.SignalID,
                    ClientID = model.ClientID,
                    Description = model.Log.Description,
                    ErrorType = model.Log.ErrorType,
                    Message = model.Log.Message,
                    Time = model.Log.Time,
                    Type = model.Log.Type,
                }
                ;
                await dbContext.Log.AddAsync(log);

                // Save
                await dbContext.SaveChangesAsync();

                // Log
                _logger.Debug($"Saved database");

            }

            return Ok();
        }

        [HttpPost("OnItsTimeToCloseTheOrderEvent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OnItsTimeToCloseTheOrderEvent([FromBody] OnItsTimeToCloseTheOrderEvent model)
        {
            // Log
            _logger.Debug($"On Its time to close the order event triggered: {JsonConvert.SerializeObject(model)}");

            // Do null reference check
            if (model != null && model.ClientID > 0 && model.SignalID > 0)
            {
                // Log
                _logger.Debug($"Signal id is {model.SignalID}", model);

                // Log
                var log = new Entity.Log()
                {
                    SignalID = model.SignalID,
                    ClientID = model.ClientID,
                    Description = model.Log.Description,
                    ErrorType = model.Log.ErrorType,
                    Message = model.Log.Message,
                    Time = model.Log.Time,
                    Type = model.Log.Type,
                }
                ;
                await dbContext.Log.AddAsync(log);

                // Save
                await dbContext.SaveChangesAsync();

                // Log
                _logger.Debug($"Saved database");
            }

            return Ok();
        }

        [HttpPost("OnLogEvent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OnLogEvent([FromBody] OnLogEvent model)
        {
            // Log
            _logger.Debug($"On log event triggered: {JsonConvert.SerializeObject(model)}");

            // Do null reference check
            if (model != null)
            {
                // Log
                var log = new Entity.Log()
                {
                    ClientID = model.ClientID,
                    SignalID = !model.SignalID.HasValue || model.SignalID.Value == 0 ? null : model.SignalID.Value,
                    Description = model.Log.Description,
                    ErrorType = model.Log.ErrorType,
                    Message = model.Log.Message,
                    Time = model.Log.Time,
                    Type = model.Log.Type,
                };
                await dbContext.Log.AddAsync(log);

                // Save
                await dbContext.SaveChangesAsync();

                // Log
                _logger.Debug($"Saved database");
            }

            return Ok();
        }

        [HttpPost("OnDealCreatedEvent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OnDealCreatedEvent([FromBody] OnDealCreatedEvent model)
        {
            // Log
            _logger.Debug($"On deal created event triggered: {JsonConvert.SerializeObject(model)}");

            // Do null reference check
            if (model != null && model.ClientID > 0 && model.Deal != null && model.MtDealID > 0)
            {
                // Log
                _logger.Debug($"Deal id is {model.MtDealID}", model);

                // Check if the deal already exist
                if (!dbContext.Deal.Any(f => f.MtDealId == model.MtDealID))
                {
                    // Log
                    _logger.Debug($"Deal id {model.MtDealID} is not found in the database ", model);

                    int maxRetries = 3; // Maximum number of retries
                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        // Get the trade order from the database
                        var order = await dbContext.Order.Where(f => f.Symbol == model.Deal.Symbol && f.ClientID == model.ClientID && f.IsTradeClosed == false).OrderByDescending(f => f.DateCreated).FirstOrDefaultAsync();

                        // Do null reference check
                        if (order != null)
                        {
                            // Stop loop
                            attempt = maxRetries;

                            // Log
                            _logger.Debug($"Order with id {order.ID} is found in the database ", order);

                            // Add deal
                            var deal = new Entity.Deal()
                            {
                                DateCreated = DateTime.UtcNow,
                                OrderID = order.ID,
                                MtDealId = model.MtDealID,
                                Commission = model.Deal.Commission,
                                Entry = model.Deal.Entry,
                                Lots = model.Deal.Lots,
                                Magic = model.Deal.Magic,
                                Pnl = model.Deal.Pnl,
                                Price = model.Deal.Price,
                                Swap = model.Deal.Swap,
                                Symbol = model.Deal.Symbol,
                                Type = model.Deal.Type,
                                AccountBalance = model.AccountBalance,
                                AccountEquity = model.AccountEquity,
                                Spread = model.Spread,
                                SpreadCost = model.SpreadCost,
                            };
                            await dbContext.Deal.AddAsync(deal);

                            // Update the order
                            order.Swap += model.Deal.Swap;
                            order.Commission += model.Deal.Commission;
                            order.Pnl += model.Deal.Pnl;
                            order.SpreadCost += model.SpreadCost;
                            order.DateLastUpdated = DateTime.UtcNow;

                            // Log
                            _logger.Debug($"Order swap {model.Deal.Swap}, commission {model.Deal.Commission} and pnl {model.Deal.Pnl} updated in the database ", model);

                            // Log
                            var log = new Entity.Log()
                            {
                                SignalID = order.SignalID,
                                ClientID = model.ClientID,
                                Description = model.Log.Description,
                                ErrorType = model.Log.ErrorType,
                                Message = model.Log.Message,
                                Time = model.Log.Time,
                                Type = model.Log.Type,
                            };
                            await dbContext.Log.AddAsync(log);

                            // Save
                            await dbContext.SaveChangesAsync();

                            // If MT4
                            if (model.Deal.Entry == "trade")
                            {
                                order.CloseTime = DateTime.UtcNow;
                                order.IsTradeClosed = true;

                                // Log
                                _logger.Debug($"MT4, Set order as closed", order);

                            }
                            else
                            {
                                // Get total lots
                                var deals = await dbContext.Deal.Where(f => f.OrderID == order.ID && (f.Entry == "entry_in" || f.Entry == "entry_out" || f.Entry == "entry_out_by")).ToListAsync();

                                // do null reference check
                                if (deals.Count > 0)
                                {
                                    // Count the total amount of lots that are used for entry in
                                    var sumLotsIn = deals.Where(f => f.Entry == "entry_in").Sum(f => f.Lots);

                                    // Count the total amount of lots that are used for entry_out or entry_out_by
                                    var sumLotsOut = deals.Where(f => f.Entry == "entry_out" || f.Entry == "entry_out_by").Sum(f => f.Lots);

                                    // Do the calculation
                                    var totalLotsResult = sumLotsIn - sumLotsOut;

                                    // Log
                                    _logger.Debug($"MT5, Total lot result is {totalLotsResult} for order {order.ID} ", totalLotsResult);

                                    if (totalLotsResult <= 0.0)
                                    {
                                        order.CloseTime = DateTime.UtcNow;
                                        order.IsTradeClosed = true;

                                        // Log
                                        _logger.Debug($"MT5, Set order {order.ID} as closed", order);
                                    }
                                }
                                else
                                {
                                    // Log
                                    _logger.Error("MT5, No deal found to calculate if the trade is closed or not");
                                }
                            }

                            // Save
                            await dbContext.SaveChangesAsync();

                            // Log
                            _logger.Debug($"Saved database");

                        }
                        else if (attempt < maxRetries)
                        {
                            // Log
                            _logger.Information($"Order not found in database, wait 1 second");

                            // Wait for 1 second before retrying
                            await Task.Delay(1000);
                        }
                        else
                        {
                            // Log
                            _logger.Error($"Order not found in database after 3 retries");
                        }
                    }
                }
            }

            return Ok();
        }

        [HttpPost("OnAccountInfoChangedEvent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OnAccountInfoChangedEvent([FromBody] OnAccountInfoChangedEvent model)
        {
            // Log
            _logger.Debug($"On  account info changed event triggered: {JsonConvert.SerializeObject(model)}");

            // Do null reference check
            if (model != null && model.ClientID > 0 && model.AccountInfo != null)
            {
                // Get server from DB
                var client = await dbContext.Client.FirstOrDefaultAsync(f => f.ID == model.ClientID);

                // Do null reference check
                if (client != null)
                {
                    // Set properties
                    client.Balance = model.AccountInfo.Balance;
                    client.Equity = model.AccountInfo.Equity;
                    client.Currency = model.AccountInfo.Currency;
                    client.Leverage = model.AccountInfo.Leverage;

                    // Save
                    await dbContext.SaveChangesAsync();

                    // Log
                    _logger.Debug($"Saved database");
                }
                else
                {
                    // Log
                    _logger.Error($"Client not found with id {model.ClientID}", model);
                }
            }

            return Ok();
        }

        [HttpPost("OnMarketAbstentionEvent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OnMarketAbstentionEvent([FromBody] OnMarketAbstentionEvent model)
        {
            // Log
            _logger.Debug($"On market abstention event triggered: {JsonConvert.SerializeObject(model)}");

            // Do null reference check
            if (model != null &&
                    model.ClientID > 0 &&
                    model.SignalID > 0 &&
                    model.Magic > 0 &&
                    model.Log != null &&
                    model.Log.ErrorType != null
                    )
            {
                await dbContext.MarketAbstention.AddAsync(new MarketAbstention()
                {
                    LogMessage = model.Log.ErrorType,
                    Symbol = model.Symbol,
                    Type = model.OrderType,
                    ClientID = model.ClientID,
                    Magic = Convert.ToInt32(model.Magic),
                    MarketAbstentionType = model.Type,
                    SignalID = model.SignalID,
                });


                await dbContext.Log.AddAsync(new Entity.Log()
                {
                    ClientID = model.ClientID,
                    SignalID = model.SignalID,
                    Description = model.Log.Description,
                    ErrorType = model.Log.ErrorType,
                    Message = model.Log.Message,
                    Time = model.Log.Time,
                    Type = model.Log.Type,
                });

                // Save
                await dbContext.SaveChangesAsync();

                // Log
                _logger.Debug($"Saved database");
            }

            return Ok();
        }
    }
}
