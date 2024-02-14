using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Helpers
{
    public class WebsocketServer(AzurePubSubClient client, IServiceScopeFactory scopeFactory)
    {
        public async Task RunAsync()
        {

            client.OnOrderCreatedEvent += async (onOrderCreated) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                if (onOrderCreated != null && onOrderCreated.ClientID > 0 && onOrderCreated.SignalID > 0)
                {
                    // Check trade order
                    var journal = await dbContext.Order.FirstOrDefaultAsync(f => f.ClientID == onOrderCreated.ClientID && f.SignalID == onOrderCreated.Order.Magic);

                    // Check for duplicates
                    if (journal == null)
                    {
                        // Deal order
                        journal = new Order()
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
                        await dbContext.Order.AddAsync(journal);
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
                }
            };

            client.OnOrderUpdateEvent += async (onOrderUpdated) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onOrderUpdated != null && onOrderUpdated.ClientID > 0 && onOrderUpdated.Order != null && onOrderUpdated.SignalID > 0)
                {
                    // Get the trade order from the database
                    var journal = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == onOrderUpdated.SignalID && f.ClientID == onOrderUpdated.ClientID);

                    // Do null reference check
                    if (journal != null)
                    {
                        journal.CloseStopLoss = decimal.ToDouble(onOrderUpdated.Order.StopLoss);
                        journal.CloseTakeProfit = decimal.ToDouble(onOrderUpdated.Order.TakeProfit);
                    }

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
                }
            };

            client.OnOrderCloseEvent += async (onOrderClosed) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onOrderClosed != null && onOrderClosed.ClientID > 0 && onOrderClosed.SignalID > 0)
                {
                    // Get the trade order from the database
                    var journal = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == onOrderClosed.SignalID && f.ClientID == onOrderClosed.ClientID);

                    // Do null reference check
                    if (journal != null)
                    {
                        // Update close properties
                        journal.ClosePrice = decimal.ToDouble(onOrderClosed.ClosePrice);
                        journal.CloseStopLoss = decimal.ToDouble(onOrderClosed.Order.StopLoss);
                        journal.CloseTakeProfit = decimal.ToDouble(onOrderClosed.Order.TakeProfit);

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
                        }
                        ;
                        await dbContext.Log.AddAsync(log);

                        // Save
                        await dbContext.SaveChangesAsync();
                    }
                }
            };

            client.OnOrderAutoMoveSlToBeEvent += async (onOrderAutoMoveSlToBe) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onOrderAutoMoveSlToBe != null && onOrderAutoMoveSlToBe.ClientID > 0 && onOrderAutoMoveSlToBe.SignalID > 0)
                {
                    // Get the trade order from the database
                    var order = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == onOrderAutoMoveSlToBe.SignalID && f.ClientID == onOrderAutoMoveSlToBe.ClientID);

                    // Do null reference check
                    if (order != null)
                    {
                        order.CloseStopLoss = decimal.ToDouble(onOrderAutoMoveSlToBe.StopLossPrice);
                    }

                    // Log
                    var log = new Log()
                    {
                        SignalID = onOrderAutoMoveSlToBe.SignalID,
                        ClientID = onOrderAutoMoveSlToBe.ClientID,
                        Description = onOrderAutoMoveSlToBe.Log.Description,
                        ErrorType = onOrderAutoMoveSlToBe.Log.ErrorType,
                        Message = onOrderAutoMoveSlToBe.Log.Message,
                        Time = onOrderAutoMoveSlToBe.Log.Time,
                        Type = onOrderAutoMoveSlToBe.Log.Type,
                    }
                    ;
                    await dbContext.Log.AddAsync(log);

                    // Save
                    await dbContext.SaveChangesAsync();
                }
            };

            client.OnItsTimeToCloseTheOrderEvent += async (onItsTimeToCloseTheOrder) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onItsTimeToCloseTheOrder != null && onItsTimeToCloseTheOrder.ClientID > 0 && onItsTimeToCloseTheOrder.SignalID > 0)
                {
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
                }
            };

            client.OnLogEvent += async (onLog) =>
            {
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
                }
            };

            client.OnDealCreatedEvent += async (onDealCreatedEvent) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onDealCreatedEvent != null && onDealCreatedEvent.ClientID > 0 && onDealCreatedEvent.Deal != null && onDealCreatedEvent.DealID > 0)
                {
                    // Check if the deal already exist
                    if (!dbContext.Deal.Any(f => f.MtDealId == onDealCreatedEvent.DealID))
                    {
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

                                // Add deal
                                var deal = new Deal()
                                {
                                    DateCreated = DateTime.UtcNow,
                                    OrderID = order.ID,
                                    MtDealId = onDealCreatedEvent.DealID,
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

                                    if (totalLotsResult != null && totalLotsResult.TotalLots == 0.0)
                                    {
                                        order.CloseTime = DateTime.UtcNow;
                                        order.IsTradeClosed = true;
                                    }
                                }

                                // Save
                                await dbContext.SaveChangesAsync();

                            }
                            else if (attempt < maxRetries)
                            {
                                await Task.Delay(1000); // Wait for 1 second before retrying
                            }
                            else
                            {
                                // Logic for handling the case where all retries have failed.
                                // You might log this event or handle it according to your application's requirements.
                            }
                        }
                    }
                }
            };

            await client.ListeningToServerAsync();
        }
    }
}
