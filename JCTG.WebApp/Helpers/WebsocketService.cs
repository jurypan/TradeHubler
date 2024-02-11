using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Helpers
{
    public class WebsocketService(AzurePubSubClient client, JCTGDbContext dbContext)
    {
        public void Run()
        {
            client.OnOrderCreateEvent += async (onOrderCreate) =>
            {
                if (onOrderCreate != null)
                {
                    // Trade journal
                    var tj = new TradeJournal()
                    {
                        DateCreated = DateTime.UtcNow,
                        IsTradeClosed = false,
                        SignalID = onOrderCreate.SignalID,
                        ClientID = onOrderCreate.ClientID,
                        Order = new Order()
                        {
                            DateCreated = DateTime.UtcNow,
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
                        }
                    };
                    await dbContext.TradeJournal.AddAsync(tj);

                    // Log
                    var log = new Log()
                    {
                        TradeJournalID = tj.ID,
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
                // Do null reference check
                if (onOrderUpdate != null)
                {
                    // Get the trade journal from the database
                    var tj = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onOrderUpdate.SignalID && f.ClientID == onOrderUpdate.ClientID);

                    // Do null reference check
                    if (tj != null)
                    {
                        // Get the trade journal from the database
                        var order = await dbContext.Order.FirstOrDefaultAsync(f => f.TradeJournalID == tj.ID);

                        // Do null reference check
                        if(order != null && onOrderUpdate.Order != null)
                        {
                            order.Swap = onOrderUpdate.Order.Swap;
                            order.Commission = onOrderUpdate.Order.Commission;
                            order.Comment = onOrderUpdate.Order.Comment;
                            order.Pnl = onOrderUpdate.Order.Pnl;
                        }

                        // Log
                        var log = new Log()
                        {
                            TradeJournalID = tj.ID,
                            Description = onOrderUpdate.Log.Description,
                            ErrorType = onOrderUpdate.Log.ErrorType,
                            Message = onOrderUpdate.Log.Message,
                            Time = onOrderUpdate.Log.Time,
                            Type = onOrderUpdate.Log.Type,
                        }
                        ;
                        await dbContext.Log.AddAsync(log);

                        // Save
                        await dbContext.SaveChangesAsync();
                    }
                }
            };

            client.OnOrderCloseEvent += async (onOrderClose) =>
            {
                if (onOrderClose != null)
                {
                    // Do null reference check
                    if (onOrderClose != null)
                    {
                        // Get the trade journal from the database
                        var tj = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onOrderClose.SignalID && f.ClientID == onOrderClose.ClientID);

                        // Do null reference check
                        if (tj != null)
                        {
                            // Set tradingjournal as close
                            tj.IsTradeClosed = true;

                            // Get the trade journal from the database
                            var order = await dbContext.Order.FirstOrDefaultAsync(f => f.TradeJournalID == tj.ID);

                            // Do null reference check
                            if (order != null && onOrderClose.Order != null)
                            {
                                // Update costs
                                order.Swap = onOrderClose.Order.Swap;
                                order.Commission = onOrderClose.Order.Commission;
                                order.Pnl = onOrderClose.Order.Pnl;
                                order.Comment = onOrderClose.Order.Comment;

                                // Update close properties
                                order.CloseTime = DateTime.UtcNow;
                                order.CloseLots = decimal.ToDouble(onOrderClose.Order.Lots);
                                order.ClosePrice = onOrderClose.ClosePrice;
                                order.CloseStopLoss = onOrderClose.Order.StopLoss;
                                order.CloseTakeProfit = onOrderClose.Order.TakeProfit;
                            }

                            // Log
                            var log = new Log()
                            {
                                TradeJournalID = tj.ID,
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
                }
            };

            client.OnOrderAutoMoveSlToBeEvent += async (onOrderAutoMoveSlToBe) =>
            {
                // Do null reference check
                if (onOrderAutoMoveSlToBe != null)
                {
                    // Get the trade journal from the database
                    var tj = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onOrderAutoMoveSlToBe.SignalID && f.ClientID == onOrderAutoMoveSlToBe.ClientID);

                    // Do null reference check
                    if (tj != null)
                    {
                        // Log
                        var log = new Log()
                        {
                            TradeJournalID = tj.ID,
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
                }
            };

            client.OnItsTimeToCloseTheOrderEvent += async (onItsTimeToCloseTheOrder) =>
            {
                // Do null reference check
                if (onItsTimeToCloseTheOrder != null)
                {
                    // Get the trade journal from the database
                    var tj = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onItsTimeToCloseTheOrder.SignalID && f.ClientID == onItsTimeToCloseTheOrder.ClientID);

                    // Do null reference check
                    if (tj != null)
                    {
                        // Log
                        var log = new Log()
                        {
                            TradeJournalID = tj.ID,
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
                }
            };

            client.OnLogEvent += async (onLog) =>
            {
                // Do null reference check
                if (onLog != null)
                {
                    // Get the trade journal from the database
                    var tj = await dbContext.TradeJournal.FirstOrDefaultAsync(f => f.SignalID == onLog.SignalID && f.ClientID == onLog.ClientID);

                    // Log
                    var log = new Log()
                    {
                        ClientID = onLog.ClientID,
                        TradeJournalID = tj?.ID,
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

            client.ListeningToServer();
        }
    }
}
