using System.Net;
using JCTG.Entity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace JCTG.AzureFunction.Functions
{
    public class TradeJournal(ILoggerFactory loggerFactory, JCTGDbContext dbContext)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<TradeJournal>();
        private readonly JCTGDbContext _dbContext = dbContext;

        [Function("TradeJournal")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            // Read body from request
            string jsonString = await new StreamReader(req.Body).ReadToEndAsync();

            // TradeJournal item
            _logger.LogDebug($"Request body : {jsonString}");

            // Create httpResponse object
            var httpResponse = req.CreateResponse(HttpStatusCode.OK);

            try
            {
                // Parse object
                var items = JsonConvert.DeserializeObject<List<TradeJournalRequest>>(jsonString);

                // Do null reference check
                if (items != null)
                {
                    // Foreach item
                    foreach (var item in items)
                    {
                        // TradeJournal item
                        _logger.LogInformation($"Parsed Metatrader object : AccountID={item.AccountID}, ClientID={item.ClientID}, Symbol={item.Symbol}, CurrentPrice={item.CurrentPrice}, SL={item.SL}, TP={item.TP}, Magic={item.Magic}, StrategyType={item.StrategyType}", jsonString);

                        var tradeJournal = await _dbContext.TradeJournal.FirstOrDefaultAsync(f => 
                                                                   f.ClientID == item.ClientID
                                                                && f.AccountID == item.AccountID
                                                                && f.TradingviewAlertID == item.TradingviewAlertID
                                                                && f.Magic == item.Magic
                                                                );

                        // check if item is already in the db
                        if (tradeJournal == null)
                        {
                            // Add item to the database
                            tradeJournal = new Entity.TradeJournal
                            {
                                AccountID = item.AccountID,
                                ClientID = item.ClientID,
                                TradingviewAlertID = item.TradingviewAlertID,
                                Comment = item.Comment,
                                Commission = item.Commission,
                                ClosePrice = item.CurrentPrice,
                                Lots = item.Lots,
                                Magic = item.Magic,
                                OpenPrice = item.OpenPrice,
                                OpenTime = item.OpenTime,
                                Pnl =item.Pnl,
                                SL = item.SL,
                                StrategyType = item.StrategyType,
                                Swap = item.Swap,
                                Instrument = item.Symbol,
                                TicketId = item.TicketId,
                                Timeframe = item.Timeframe,
                                TP = item.TP,
                                Type = item.Type,
                                Risk = item.Risk,
                                RR = item.OpenPrice / item.SL * item.CurrentPrice,
                            };

                            // Add to database
                            await _dbContext.TradeJournal.AddAsync(tradeJournal);
                        }
                        else
                        {
                            // Add item to the database
                            tradeJournal.ClosePrice = item.CurrentPrice;
                            tradeJournal.Commission = item.Commission;
                            tradeJournal.Pnl = item.Pnl;
                            tradeJournal.Swap = item.Swap;
                            tradeJournal.CloseTime = DateTime.UtcNow;
                            tradeJournal.RR = item.OpenPrice / item.SL * item.CurrentPrice;
                        };
                    }

                    // Update database
                    await _dbContext.SaveChangesAsync();
                }

            }
            catch (Exception ex)
            {
                // TradeJournal item
                _logger.LogError($"Message: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
            }

            return httpResponse;
        }
    }
}
