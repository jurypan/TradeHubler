using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Helpers
{
    public class WebsocketServer(AzurePubSubClient client, IServiceScopeFactory scopeFactory)
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<WebsocketServer>();

        public async Task RunAsync()
        {
            // Log
            _logger.Debug($"Init event handlers");

            client.OnOrderCreatedEvent += async (onOrderCreated) =>
            {
                // Log
                _logger.Debug("On order create event triggered", onOrderCreated);

                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                if (onOrderCreated != null && onOrderCreated.ClientID > 0 && onOrderCreated.SignalID > 0)
                {
                    // Log
                    _logger.Debug($"Signal id is {onOrderCreated.SignalID}", onOrderCreated);

                    // Check trade order
                    var order = await dbContext.Order.FirstOrDefaultAsync(f => f.ClientID == onOrderCreated.ClientID && f.SignalID == onOrderCreated.SignalID);

                    // Check for duplicates
                    if (order == null)
                    {
                        // Log
                        _logger.Debug($"Order not found");

                        // Deal order
                        order = new Order()
                        {
                            DateCreated = DateTime.UtcNow,
                            IsTradeClosed = false,
                            SignalID = onOrderCreated.SignalID,
                            ClientID = onOrderCreated.ClientID,
                            Symbol = onOrderCreated.Order.Symbol ?? string.Empty,
                            Type = onOrderCreated.Order.Type ?? string.Empty,
                            OpenLots = decimal.ToDouble(onOrderCreated.Order.Lots),
                            OpenPrice = decimal.ToDouble(onOrderCreated.Order.OpenPrice),
                            OpenTime = onOrderCreated.Order.OpenTime,
                            OpenStopLoss = decimal.ToDouble(onOrderCreated.Order.StopLoss),
                            OpenTakeProfit = decimal.ToDouble(onOrderCreated.Order.TakeProfit),
                            Comment = onOrderCreated.Order.Comment,
                            Magic = onOrderCreated.Order.Magic,
                        };
                        await dbContext.Order.AddAsync(order);
                    }

                    // Log
                    var log = new Log()
                    {
                        SignalID = onOrderCreated.SignalID,
                        ClientID = onOrderCreated.ClientID,
                        Description = onOrderCreated.Log.Description,
                        ErrorType = onOrderCreated.Log.ErrorType,
                        Message = onOrderCreated.Log.Message,
                        Time = onOrderCreated.Log.Time,
                        Type = onOrderCreated.Log.Type,
                    }
                    ;
                    await dbContext.Log.AddAsync(log);

                    // Save
                    await dbContext.SaveChangesAsync();

                    // Log
                    _logger.Information($"Saved order in database with id {order.ID}");
                }
            };

            client.OnOrderUpdateEvent += async (onOrderUpdated) =>
            {
                // Log
                _logger.Debug("On order update event triggered", onOrderUpdated);

                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onOrderUpdated != null && onOrderUpdated.ClientID > 0 && onOrderUpdated.Order != null && onOrderUpdated.SignalID > 0)
                {
                    // Log
                    _logger.Debug($"Signal id is {onOrderUpdated.SignalID}", onOrderUpdated);

                    // Get the trade order from the database
                    var order = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == onOrderUpdated.SignalID && f.ClientID == onOrderUpdated.ClientID);

                    // Do null reference check
                    if (order != null)
                    {
                        // Log
                        _logger.Debug($"Order found, update SL {decimal.ToDouble(onOrderUpdated.Order.StopLoss)} and TP {decimal.ToDouble(onOrderUpdated.Order.TakeProfit)}", order);

                        // Update entity
                        order.CloseStopLoss = decimal.ToDouble(onOrderUpdated.Order.StopLoss);
                        order.CloseTakeProfit = decimal.ToDouble(onOrderUpdated.Order.TakeProfit);

                        // Log
                        var log = new Log()
                        {
                            SignalID = onOrderUpdated.SignalID,
                            ClientID = onOrderUpdated.ClientID,
                            Description = onOrderUpdated.Log.Description,
                            ErrorType = onOrderUpdated.Log.ErrorType,
                            Message = onOrderUpdated.Log.Message,
                            Time = onOrderUpdated.Log.Time,
                            Type = onOrderUpdated.Log.Type,
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
                        _logger.Error($"Order not foudn with signal id {onOrderUpdated.SignalID}");
                    }
                }
            };

            client.OnOrderCloseEvent += async (onOrderClosed) =>
            {
                // Log
                _logger.Debug("On order close event triggered", onOrderClosed);

                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onOrderClosed != null && onOrderClosed.ClientID > 0 && onOrderClosed.SignalID > 0)
                {
                    // Log
                    _logger.Debug($"Signal id is {onOrderClosed.SignalID}", onOrderClosed);

                    // Get the trade order from the database
                    var order = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == onOrderClosed.SignalID && f.ClientID == onOrderClosed.ClientID);

                    // Do null reference check
                    if (order != null)
                    {
                        // Log
                        _logger.Debug($"Order found, update Price {decimal.ToDouble(onOrderClosed.ClosePrice)}, update SL {decimal.ToDouble(onOrderClosed.Order.StopLoss)} and TP {decimal.ToDouble(onOrderClosed.Order.TakeProfit)}", order);

                        // Update close properties
                        order.ClosePrice = decimal.ToDouble(onOrderClosed.ClosePrice);
                        order.CloseStopLoss = decimal.ToDouble(onOrderClosed.Order.StopLoss);
                        order.CloseTakeProfit = decimal.ToDouble(onOrderClosed.Order.TakeProfit);

                        // Log
                        var log = new Log()
                        {
                            SignalID = onOrderClosed.SignalID,
                            ClientID = onOrderClosed.ClientID,
                            Description = onOrderClosed.Log.Description,
                            ErrorType = onOrderClosed.Log.ErrorType,
                            Message = onOrderClosed.Log.Message,
                            Time = onOrderClosed.Log.Time,
                            Type = onOrderClosed.Log.Type,
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
                        _logger.Error($"Order not found with signal id {onOrderClosed.SignalID}");
                    }
                }
            };

            client.OnAutoMoveSlToBeEvent += async (onAutoMoveSlToBe) =>
            {
                // Log
                _logger.Debug("On auto move SL to BE event triggered", onAutoMoveSlToBe);

                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onAutoMoveSlToBe != null && onAutoMoveSlToBe.ClientID > 0 && onAutoMoveSlToBe.SignalID > 0)
                {
                    // Log
                    _logger.Debug($"Signal id is {onAutoMoveSlToBe.SignalID}", onAutoMoveSlToBe);

                    // Get the trade order from the database
                    var order = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == onAutoMoveSlToBe.SignalID && f.ClientID == onAutoMoveSlToBe.ClientID);

                    // Do null reference check
                    if (order != null)
                    {
                        // Log
                        _logger.Debug($"Order found, update SL {decimal.ToDouble(onAutoMoveSlToBe.StopLossPrice)}", order);

                        // Update entity
                        order.CloseStopLoss = decimal.ToDouble(onAutoMoveSlToBe.StopLossPrice);
                    }
                    else
                    { 
                        // Log
                        _logger.Error($"Order not found with signal id {onAutoMoveSlToBe.SignalID}");
                    }

                    // Log
                    var log = new Log()
                    {
                        SignalID = onAutoMoveSlToBe.SignalID,
                        ClientID = onAutoMoveSlToBe.ClientID,
                        Description = onAutoMoveSlToBe.Log.Description,
                        ErrorType = onAutoMoveSlToBe.Log.ErrorType,
                        Message = onAutoMoveSlToBe.Log.Message,
                        Time = onAutoMoveSlToBe.Log.Time,
                        Type = onAutoMoveSlToBe.Log.Type,
                    }
                    ;
                    await dbContext.Log.AddAsync(log);

                    // Save
                    await dbContext.SaveChangesAsync();

                    // Log
                    _logger.Debug($"Saved database");

                }
            };

            client.OnItsTimeToCloseTheOrderEvent += async (onItsTimeToCloseTheOrder) =>
            {
                // Log
                _logger.Debug("On Its time to close the order event triggered", onItsTimeToCloseTheOrder);

                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onItsTimeToCloseTheOrder != null && onItsTimeToCloseTheOrder.ClientID > 0 && onItsTimeToCloseTheOrder.SignalID > 0)
                {
                    // Log
                    _logger.Debug($"Signal id is {onItsTimeToCloseTheOrder.SignalID}", onItsTimeToCloseTheOrder);

                    // Log
                    var log = new Log()
                    {
                        SignalID = onItsTimeToCloseTheOrder.SignalID,
                        ClientID = onItsTimeToCloseTheOrder.ClientID,
                        Description = onItsTimeToCloseTheOrder.Log.Description,
                        ErrorType = onItsTimeToCloseTheOrder.Log.ErrorType,
                        Message = onItsTimeToCloseTheOrder.Log.Message,
                        Time = onItsTimeToCloseTheOrder.Log.Time,
                        Type = onItsTimeToCloseTheOrder.Log.Type,
                    }
                    ;
                    await dbContext.Log.AddAsync(log);

                    // Save
                    await dbContext.SaveChangesAsync();

                    // Log
                    _logger.Debug($"Saved database");
                }
            };

            client.OnLogEvent += async (onLog) =>
            {
                // Log
                _logger.Debug("On log event triggered", onLog);

                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onLog != null)
                {
                    // Log
                    var log = new Log()
                    {
                        ClientID = onLog.ClientID,
                        SignalID = !onLog.SignalID.HasValue || onLog.SignalID.Value == 0 ? null : onLog.SignalID.Value,
                        Description = onLog.Log.Description,
                        ErrorType = onLog.Log.ErrorType,
                        Message = onLog.Log.Message,
                        Time = onLog.Log.Time,
                        Type = onLog.Log.Type,
                    };
                    await dbContext.Log.AddAsync(log);

                    // Save
                    await dbContext.SaveChangesAsync();

                    // Log
                    _logger.Debug($"Saved database");
                }
            };

            client.OnDealCreatedEvent += async (onDealCreatedEvent) =>
            {
                // Log
                _logger.Debug("On deal created event triggered", onDealCreatedEvent);

                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onDealCreatedEvent != null && onDealCreatedEvent.ClientID > 0 && onDealCreatedEvent.Deal != null && onDealCreatedEvent.MtDealID > 0)
                {
                    // Log
                    _logger.Debug($"Deal id is {onDealCreatedEvent.MtDealID}", onDealCreatedEvent);

                    // Check if the deal already exist
                    if (!dbContext.Deal.Any(f => f.MtDealId == onDealCreatedEvent.MtDealID))
                    {
                        // Log
                        _logger.Debug($"Deal id {onDealCreatedEvent.MtDealID} is not found in the database ", onDealCreatedEvent);

                        int maxRetries = 3; // Maximum number of retries
                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                            // Get the trade order from the database
                            var order = await dbContext.Order.Where(f => f.Symbol == onDealCreatedEvent.Deal.Symbol && f.ClientID == onDealCreatedEvent.ClientID && f.IsTradeClosed == false).OrderByDescending(f => f.DateCreated).FirstOrDefaultAsync();

                            // Do null reference check
                            if (order != null)
                            {
                                // Stop loop
                                attempt = maxRetries;

                                // Log
                                _logger.Debug($"Order with id {order.ID} is found in the database ", order);

                                // Add deal
                                var deal = new Deal()
                                {
                                    DateCreated = DateTime.UtcNow,
                                    OrderID = order.ID,
                                    MtDealId = onDealCreatedEvent.MtDealID,
                                    Commission = onDealCreatedEvent.Deal.Commission,
                                    Entry = onDealCreatedEvent.Deal.Entry,
                                    Lots =  onDealCreatedEvent.Deal.Lots,
                                    Magic = onDealCreatedEvent.Deal.Magic,
                                    Pnl = onDealCreatedEvent.Deal.Pnl,
                                    Price = onDealCreatedEvent.Deal.Price,
                                    Swap = onDealCreatedEvent.Deal.Swap,
                                    Symbol = onDealCreatedEvent.Deal.Symbol,
                                    Type = onDealCreatedEvent.Deal.Type,
                                };
                                await dbContext.Deal.AddAsync(deal);

                                // Update the order
                                order.Swap += onDealCreatedEvent.Deal.Swap;
                                order.Commission += onDealCreatedEvent.Deal.Commission;
                                order.Pnl += onDealCreatedEvent.Deal.Pnl;

                                // Log
                                _logger.Debug($"Order swap {onDealCreatedEvent.Deal.Swap}, commission {onDealCreatedEvent.Deal.Commission} and pnl {onDealCreatedEvent.Deal.Pnl} updated in the database ", onDealCreatedEvent);

                                // Log
                                var log = new Log()
                                {
                                    SignalID = order.SignalID,
                                    ClientID = onDealCreatedEvent.ClientID,
                                    Description = onDealCreatedEvent.Log.Description,
                                    ErrorType = onDealCreatedEvent.Log.ErrorType,
                                    Message = onDealCreatedEvent.Log.Message,
                                    Time = onDealCreatedEvent.Log.Time,
                                    Type = onDealCreatedEvent.Log.Type,
                                };
                                await dbContext.Log.AddAsync(log);

                                // If MT4
                                if(onDealCreatedEvent.Deal.Entry == "trade")
                                {
                                    order.CloseTime = DateTime.UtcNow;
                                    order.IsTradeClosed = true;

                                    // Log
                                    _logger.Debug($"MT4, Set order as closed", order);

                                }
                                else
                                {
                                    // MT5 is more complex, check if trade is closed
                                    var totalLotsResult = dbContext.Deal
                                                                .Where(f => f.OrderID == order.ID)
                                                                .GroupBy(trade => 1) // Group by a constant to aggregate over all trades
                                                                .Select(g => new
                                                                {
                                                                    TotalLots = g.Sum(trade =>
                                                                        trade.Entry == "entry_in" ? trade.Lots :
                                                                        trade.Entry == "entry_out" || trade.Entry == "entry_out_by" ? -trade.Lots :
                                                                        0)
                                                                })
                                                                .FirstOrDefault();
                                    // Log
                                    _logger.Debug($"MT5, Total lot result is {totalLotsResult}", totalLotsResult);

                                    if (totalLotsResult != null && totalLotsResult.TotalLots == 0.0)
                                    {
                                        order.CloseTime = DateTime.UtcNow;
                                        order.IsTradeClosed = true;

                                        // Log
                                        _logger.Debug($"MT5, Set order as closed", order);
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
            };

            // Listen to the server
            await client.ListeningToServerAsync();

            // Log
            _logger.Information($"Websocket started");
        }
    }
}
