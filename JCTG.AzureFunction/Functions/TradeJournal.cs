using System.Net;
using JCTG.Entity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
                        _logger.LogInformation($"Parsed Metatrader object : AccountID={item.AccountID}, ClientID={item.ClientID}, Symbol={item.Symbol}, ClosePrice={item.ClosePrice}, SL={item.SL}, TP={item.TP}, Magic={item.Magic}, StrategyType={item.StrategyType}", jsonString);

                        // check if item is already in the db
                        if (!_dbContext.TradeJournal.Any(f => f.ClientID == item.ClientID
                                                                && f.AccountID == item.AccountID
                                                                && f.Instrument == item.Symbol
                                                                && f.OpenTime == item.OpenTime
                                                                && f.CloseTime == item.CloseTime
                                                                )
                            )
                        {
                            // Add item to the database
                            var tradeJournal = new Entity.TradeJournal()
                            {
                                ClientID = item.ClientID,
                                AccountID = item.AccountID,
                                Instrument = item.Symbol,
                                OpenTime = item.OpenTime,
                                CloseTime = item.CloseTime,
                                ClosePrice = item.ClosePrice,
                                Comment = !string.IsNullOrEmpty(item.Comment) ? item.Comment : null,
                                Commission = item.Commission,
                                DateCreated = DateTime.UtcNow,
                                Lots = item.Lots,
                                Magic = item.Magic,
                                OpenPrice = item.OpenPrice,
                                Pnl = item.Pnl,
                                SL = item.SL,
                                Swap = item.Swap,
                                TP = item.TP,
                                Type = item.Type.ToUpper(),
                                StrategyType = item.StrategyType,
                                Timeframe = item.Timeframe,
                            };

                            // Add to database
                            await _dbContext.TradeJournal.AddAsync(tradeJournal);
                            await _dbContext.SaveChangesAsync();
                        }
                    }
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
