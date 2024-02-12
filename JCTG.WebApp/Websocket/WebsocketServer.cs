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
                    // Check for duplicates
                    if(!await dbContext.TradeJournal.AnyAsync(f => f.ClientID == onOrderCreate.ClientID && f.SignalID == onOrderCreate.SignalID))
                    {
                        // Trade journal
                        var journal = new TradeJournal()
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
                        await dbContext.TradeJournal.AddAsync(journal);

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
                    var journal = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onOrderUpdate.SignalID && f.ClientID == onOrderUpdate.ClientID);

                    // Do null reference check
                    if (journal != null)
                    {
                        journal.Comment = onOrderUpdate.Order.Comment;
                        journal.CloseLots = decimal.ToDouble(onOrderUpdate.Order.Lots);
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
                    var journal = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onOrderClose.SignalID && f.ClientID == onOrderClose.ClientID);

                    // Do null reference check
                    if (journal != null)
                    {
                        // Update costs
                        journal.Swap = onOrderClose.Order.Swap;
                        journal.Commission = onOrderClose.Order.Commission;
                        journal.Pnl = onOrderClose.Order.Pnl;
                        journal.Comment = onOrderClose.Order.Comment;

                        // Update close properties
                        journal.CloseTime = DateTime.UtcNow;
                        journal.CloseLots = decimal.ToDouble(onOrderClose.Order.Lots);
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
                    var journal = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onOrderAutoMoveSlToBe.SignalID && f.ClientID == onOrderAutoMoveSlToBe.ClientID);

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

            client.OnTradeEvent += async (onTradeEvent) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onTradeEvent != null && onTradeEvent.ClientID > 0 && onTradeEvent.Trade != null && onTradeEvent.TradeID > 0)
                {
                    // When a new trade is just entered
                    if(onTradeEvent.SignalID.HasValue && onTradeEvent.Trade.Entry == "entry_in")
                    {
                        // Get the trade journal from the database
                        var journal = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onTradeEvent.SignalID && f.ClientID == onTradeEvent.ClientID);

                        // Do null reference check
                        if (journal != null)
                        {
                            journal.TradeID = onTradeEvent.TradeID;
                            journal.Swap += onTradeEvent.Trade.Swap;
                            journal.Commission += onTradeEvent.Trade.Commission;
                            journal.Pnl += onTradeEvent.Trade.Pnl;
                            journal.Comment = onTradeEvent.Trade.Comment;
                        }

                        // Log
                        var log = new Log()
                        {
                            SignalID = onTradeEvent.SignalID,
                            ClientID = onTradeEvent.ClientID,
                            Description = onTradeEvent.Log.Description,
                            ErrorType = onTradeEvent.Log.ErrorType,
                            Message = onTradeEvent.Log.Message,
                            Time = onTradeEvent.Log.Time,
                            Type = onTradeEvent.Log.Type,
                        };
                        await dbContext.Log.AddAsync(log);

                        // Save
                        await dbContext.SaveChangesAsync();
                    }

                    // When a new trade is just entered
                    if (!onTradeEvent.SignalID.HasValue && onTradeEvent.Trade.Entry == "entry_out")
                    {
                        // Get the trade journal from the database
                        var journal = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.TradeID == onTradeEvent.TradeID && f.ClientID == onTradeEvent.ClientID);

                        // Do null reference check
                        if (journal != null)
                        {
                            journal.Swap += onTradeEvent.Trade.Swap;
                            journal.Commission += onTradeEvent.Trade.Commission;
                            journal.Pnl += onTradeEvent.Trade.Pnl;

                            // Log
                            var log = new Log()
                            {
                                SignalID = journal.SignalID,
                                ClientID = onTradeEvent.ClientID,
                                Description = onTradeEvent.Log.Description,
                                ErrorType = onTradeEvent.Log.ErrorType,
                                Message = onTradeEvent.Log.Message,
                                Time = onTradeEvent.Log.Time,
                                Type = onTradeEvent.Log.Type,
                            };
                            await dbContext.Log.AddAsync(log);
                        }
                    }
                }
            };

            await client.ListeningToServerAsync();
        }
    }
}
