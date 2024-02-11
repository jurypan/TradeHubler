﻿using JCTG.Entity;
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

                if (onOrderCreate != null)
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
                        OpenPrice = onOrderCreate.Order.OpenPrice,
                        OpenTime = onOrderCreate.Order.OpenTime,
                        OpenStopLoss = onOrderCreate.Order.StopLoss,
                        OpenTakeProfit = onOrderCreate.Order.TakeProfit,
                        Pnl = onOrderCreate.Order.Pnl,
                        Commission = onOrderCreate.Order.Commission,
                        Swap = onOrderCreate.Order.Swap,
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
            };

            client.OnOrderUpdateEvent += async (onOrderUpdate) =>
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<JCTGDbContext>();

                // Do null reference check
                if (onOrderUpdate != null && onOrderUpdate.Order != null)
                {
                    // Get the trade journal from the database
                    var journal = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onOrderUpdate.SignalID && f.ClientID == onOrderUpdate.ClientID);

                    // Do null reference check
                    if (journal != null)
                    {
                        journal.Swap = onOrderUpdate.Order.Swap;
                        journal.Commission = onOrderUpdate.Order.Commission;
                        journal.Comment = onOrderUpdate.Order.Comment;
                        journal.Pnl = onOrderUpdate.Order.Pnl;
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
                if (onOrderClose != null)
                {
                    // Get the trade journal from the database
                    var journal = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onOrderClose.SignalID && f.ClientID == onOrderClose.ClientID);

                    // Do null reference check
                    if (journal != null)
                    {
                        // Set tradingjournal as close
                        journal.IsTradeClosed = true;

                        // Update costs
                        journal.Swap = onOrderClose.Order.Swap;
                        journal.Commission = onOrderClose.Order.Commission;
                        journal.Pnl = onOrderClose.Order.Pnl;
                        journal.Comment = onOrderClose.Order.Comment;

                        // Update close properties
                        journal.CloseTime = DateTime.UtcNow;
                        journal.CloseLots = decimal.ToDouble(onOrderClose.Order.Lots);
                        journal.ClosePrice = onOrderClose.ClosePrice;
                        journal.CloseStopLoss = onOrderClose.Order.StopLoss;
                        journal.CloseTakeProfit = onOrderClose.Order.TakeProfit;

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
                if (onOrderAutoMoveSlToBe != null)
                {
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
                if (onItsTimeToCloseTheOrder != null)
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
                        SignalID = onLog.SignalID,
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

            await client.ListeningToServerAsync();
        }
    }
}
