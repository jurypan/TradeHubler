using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Helpers
{
    public class WebsocketServer(AzurePubSubClient client, IServiceScopeFactory scopeFactory)
    {
        public async Task RunAsync()
        {

            client.OnOrderCreateEvent += async (onOrderCreate) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                if (onOrderCreate != null && onOrderCreate.ClientID > 0 && onOrderCreate.SignalID > 0)
                {
                    // Check trade journal
                    var journal = await dbContext.Order.FirstOrDefaultAsync(f => f.ClientID == onOrderCreate.ClientID && f.SignalID == onOrderCreate.Order.Magic);

                    // Check for duplicates
                    if (journal == null)
                    {
                        // Deal journal
                        journal = new Order()
                        {
                            DateCreated = DateTime.UtcNow,
                            IsTradeClosed = false,
                            SignalID = onOrderCreate.SignalID,
                            ClientID = onOrderCreate.ClientID,
                            Symbol = onOrderCreate.Order.Symbol ?? string.Empty,
                            Type = onOrderCreate.Order.Type ?? string.Empty,
                            OpenLots = decimal.ToDouble(onOrderCreate.Order.Lots),
                            OpenPrice = decimal.ToDouble(onOrderCreate.Order.OpenPrice),
                            OpenTime = onOrderCreate.Order.OpenTime,
                            OpenStopLoss = decimal.ToDouble(onOrderCreate.Order.StopLoss),
                            OpenTakeProfit = decimal.ToDouble(onOrderCreate.Order.TakeProfit),
                            Comment = onOrderCreate.Order.Comment,
                            Magic = onOrderCreate.Order.Magic,
                        };
                        await dbContext.Order.AddAsync(journal);
                    }

                    // Log
                    var log = new Log()
                    {
                        SignalID = onOrderCreate.SignalID,
                        ClientID = onOrderCreate.ClientID,
                        Description = onOrderCreate.Log.Description,
                        ErrorType = onOrderCreate.Log.ErrorType,
                        Message = onOrderCreate.Log.Message,
                        Time = onOrderCreate.Log.Time,
                        Type = onOrderCreate.Log.Type,
                    }
                    ;
                    await dbContext.Log.AddAsync(log);

                    // Save
                    await dbContext.SaveChangesAsync();
                }
            };

            client.OnOrderUpdateEvent += async (onOrderUpdate) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onOrderUpdate != null && onOrderUpdate.ClientID > 0 && onOrderUpdate.Order != null && onOrderUpdate.SignalID > 0)
                {
                    // Get the trade journal from the database
                    var journal = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == onOrderUpdate.SignalID && f.ClientID == onOrderUpdate.ClientID);

                    // Do null reference check
                    if (journal != null)
                    {
                        journal.CloseStopLoss = decimal.ToDouble(onOrderUpdate.Order.StopLoss);
                        journal.CloseTakeProfit = decimal.ToDouble(onOrderUpdate.Order.TakeProfit);
                    }

                    // Log
                    var log = new Log()
                    {
                        SignalID = onOrderUpdate.SignalID,
                        ClientID = onOrderUpdate.ClientID,
                        Description = onOrderUpdate.Log.Description,
                        ErrorType = onOrderUpdate.Log.ErrorType,
                        Message = onOrderUpdate.Log.Message,
                        Time = onOrderUpdate.Log.Time,
                        Type = onOrderUpdate.Log.Type,
                    };
                    await dbContext.Log.AddAsync(log);

                    // Save
                    await dbContext.SaveChangesAsync();
                }
            };

            client.OnOrderCloseEvent += async (onOrderClose) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onOrderClose != null && onOrderClose.ClientID > 0 && onOrderClose.SignalID > 0)
                {
                    // Get the trade journal from the database
                    var journal = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == onOrderClose.SignalID && f.ClientID == onOrderClose.ClientID);

                    // Do null reference check
                    if (journal != null)
                    {
                        // Update close properties
                        journal.ClosePrice = decimal.ToDouble(onOrderClose.ClosePrice);
                        journal.CloseStopLoss = decimal.ToDouble(onOrderClose.Order.StopLoss);
                        journal.CloseTakeProfit = decimal.ToDouble(onOrderClose.Order.TakeProfit);

                        // Log
                        var log = new Log()
                        {
                            SignalID = onOrderClose.SignalID,
                            ClientID = onOrderClose.ClientID,
                            Description = onOrderClose.Log.Description,
                            ErrorType = onOrderClose.Log.ErrorType,
                            Message = onOrderClose.Log.Message,
                            Time = onOrderClose.Log.Time,
                            Type = onOrderClose.Log.Type,
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
                    // Get the trade journal from the database
                    var journal = await dbContext.Order.FirstOrDefaultAsync(f => f.SignalID == onOrderAutoMoveSlToBe.SignalID && f.ClientID == onOrderAutoMoveSlToBe.ClientID);

                    // Do null reference check
                    if (journal != null)
                    {
                        journal.CloseStopLoss = decimal.ToDouble(onOrderAutoMoveSlToBe.StopLossPrice);
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

            client.OnDealEvent += async (onDealEvent) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onDealEvent != null && onDealEvent.ClientID > 0 && onDealEvent.Deal != null && onDealEvent.DealID > 0)
                {
                    // Check if the deal already exist
                    if (!dbContext.Deal.Any(f => f.MtDealId == onDealEvent.DealID))
                    {
                        int maxRetries = 3; // Maximum number of retries
                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                            // Get the trade journal from the database
                            var journal = await dbContext.Order.Where(f => f.Symbol == onDealEvent.Deal.Symbol && f.ClientID == onDealEvent.ClientID && f.IsTradeClosed == false).OrderByDescending(f => f.DateCreated).FirstOrDefaultAsync();

                            // Do null reference check
                            if (journal != null)
                            {
                                // Stop loop
                                attempt = maxRetries;

                                // Add deal
                                var deal = new Deal()
                                {
                                    DateCreated = DateTime.UtcNow,
                                    MtDealId = onDealEvent.DealID,
                                    Commission = onDealEvent.Deal.Commission,
                                    Entry = onDealEvent.Deal.Entry,
                                    Lots =  onDealEvent.Deal.Lots,
                                    Magic = onDealEvent.Deal.Magic,
                                    Pnl = onDealEvent.Deal.Pnl,
                                    Price = onDealEvent.Deal.Price,
                                    Swap = onDealEvent.Deal.Swap,
                                    Symbol = onDealEvent.Deal.Symbol,
                                    OrderID = journal.ID,
                                    Type = onDealEvent.Deal.Type,
                                };
                                await dbContext.Deal.AddAsync(deal);

                                // Update the journal
                                journal.Swap += onDealEvent.Deal.Swap;
                                journal.Commission += onDealEvent.Deal.Commission;
                                journal.Pnl += onDealEvent.Deal.Pnl;

                                // Log
                                var log = new Log()
                                {
                                    SignalID = journal.SignalID,
                                    ClientID = onDealEvent.ClientID,
                                    Description = onDealEvent.Log.Description,
                                    ErrorType = onDealEvent.Log.ErrorType,
                                    Message = onDealEvent.Log.Message,
                                    Time = onDealEvent.Log.Time,
                                    Type = onDealEvent.Log.Type,
                                };
                                await dbContext.Log.AddAsync(log);

                                // If MT4
                                if(onDealEvent.Deal.Entry == "trade")
                                {
                                    journal.CloseTime = DateTime.UtcNow;
                                    journal.IsTradeClosed = true;
                                }
                                else
                                {
                                    // MT5 is more complex, check if trade is closed
                                    var totalLotsResult = dbContext.Deal
                                                                .Where(f => f.OrderID == journal.ID)
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
                                        journal.CloseTime = DateTime.UtcNow;
                                        journal.IsTradeClosed = true;
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
