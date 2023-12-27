using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace JCTG.AzureFunction.Functions
{
    public class TicketId(ILoggerFactory loggerFactory, JCTGDbContext dbContext)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<TicketId>();
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
            long response = 0;

            try
            {
                // Parse object
                var item = JsonConvert.DeserializeObject<TicketIdRequest>(jsonString);

                // Do null reference check
                if (item != null)
                {
                    // TradeJournal item
                    _logger.LogInformation($"Parsed ticket id object : AccountID={item.AccountID}, ClientID={item.ClientID}, Symbol={item.Symbol}, Type={item.Type}, OpenPrice={item.OpenPrice}, StrategyType={item.StrategyType}, Magic={item.Magic}, OpenTime={item.OpenTime}", jsonString);

                    var tradeJournal = await _dbContext.TradeJournal.FirstOrDefaultAsync(f => 
                                                                    f.ClientID == item.ClientID
                                                                && f.AccountID == item.AccountID
                                                                && f.Instrument == item.Symbol
                                                                && f.Type == item.Type
                                                                && f.OpenPrice == item.OpenPrice
                                                                && f.OpenTime == item.OpenTime
                                                                && f.StrategyType == item.StrategyType
                                                                && f.Magic == item.Magic
                                                            );

                    // check if item is already in the db
                    if (tradeJournal == null)
                    {
                        // Add to log
                        await _dbContext.Log.AddAsync(new JCTG.Log()
                        {
                            AccountID = item.AccountID,
                            ClientID = item.ClientID,
                            DateCreated = DateTime.UtcNow,
                            Type = "BACKEND - GET TICKET ID",
                            ErrorType = "Can not find ticket id",
                            Message = string.Format($"Symbol={item.Symbol}, Type={item.Type}, OpenPrice={item.OpenPrice}, StrategyType={item.StrategyType}, Magic={item.Magic}, OpenTime={item.OpenTime}"),
                        });
                    }
                    else
                    {
                        // Add to log
                        await _dbContext.Log.AddAsync(new JCTG.Log()
                        {
                            AccountID = item.AccountID,
                            ClientID = item.ClientID,
                            DateCreated = DateTime.UtcNow,
                            Type = "BACKEND - GET TICKET ID",
                            Message = string.Format($"Symbol={item.Symbol}, Type={item.Type}, OpenPrice={item.OpenPrice}, StrategyType={item.StrategyType}, Magic={item.Magic}, OpenTime={item.OpenTime}"),
                        });

                        // Add ticket id to retour
                        response = tradeJournal.TicketId;
                    };
                }
            }
            catch (Exception ex)
            {
                // TradeJournal item
                _logger.LogError($"Message: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
            }

            await httpResponse.WriteAsJsonAsync(response);
            return httpResponse;
        }
    }
}
