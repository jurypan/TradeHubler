using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JustCallTheGuy.STR1
{
    public class Tradingview
    {
        private readonly ILogger _logger;
        private readonly JCTGDbContext _dbContext;

        public Tradingview(ILoggerFactory loggerFactory, JCTGDbContext dbContext)
        {
            _logger = loggerFactory.CreateLogger<Tradingview>();
            _dbContext = dbContext;
        }

        [Function("Strategy1_Tradingview")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            // Read body from request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Log item
            _logger.LogDebug($"Strategy1_Tradingview || Request body : {requestBody}");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            try
            {
                // Parse into a Trade Alert object
                var order = TradingviewAlert.Parse(requestBody);

                // Log item
                _logger.LogInformation($"Strategy1_Tradingview || Parse obejct to TradingviewAlert : AccountID={order.AccountID}, Type={order.OrderType}, Instrument={order.Instrument}, Price={order.Price}, SL={order.StopLoss}, TP={order.TakeProfit}, Comment={order.Comment}", order);

                // Save into the database
                order.StrategyType = StrategyType.Strategy1;
                await _dbContext.TradingviewAlert.AddAsync(order);
                await _dbContext.SaveChangesAsync();

                // Add log
                _logger.LogInformation($"Strategy1_Tradingview || Added to database in table TradingviewAlert with ID : {order.ID}", order);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Strategy1_Tradingview ||\nMessage: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
            }

            return response;
        }
    }
}
