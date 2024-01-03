using System.Diagnostics;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace JCTG.AzureFunction
{
    public class Tradingview(ILoggerFactory loggerFactory, JCTGDbContext dbContext)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<Tradingview>();
        private readonly JCTGDbContext _dbContext = dbContext;

        [Function("Tradingview")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            // Read body from request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // TradeJournal item
            _logger.LogDebug($"Request body : {requestBody}");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            try
            {
                // Parse into a SignalExecuted Alert object
                var order = Signal.Parse(requestBody);

                // TradeJournal item
                _logger.LogInformation($"Parse obejct to Signal : AccountID={order.AccountID}, Type={order.OrderType}, TickerInMetatrader={order.Instrument}, CurrentPrice={order.CurrentPrice}, SL={order.StopLoss}, TP={order.TakeProfit}, Magic={order.Magic}", order);

                // Save into the database
                if(order.OrderType != "SLHIT" && order.OrderType != "TPHIT")
                {
                    await _dbContext.Signal.AddAsync(order);
                    await _dbContext.SaveChangesAsync();

                    // Add log
                    _logger.LogInformation($"Added to database in table Signal with ID : {order.ID}", order);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
            }

            return response;
        }
    }
}
