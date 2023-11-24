using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace JustCallTheGuy.Strategy2
{
    public class Tradingview
    {
        private readonly ILogger _logger;

        public Tradingview(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Tradingview>();
        }

        [Function("Strategy2_Tradingview")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogDebug(requestBody);

            try
            {
                var order = TradeOrder.Parse(requestBody);
                _logger.LogInformation($"Order ID: {order.Id}, Type: {order.OrderType}, Instrument: {order.Instrument}, Price: {order.Price}, SL: {order.StopLoss}, Risk: {order.Risk}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
            }




            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            return response;
        }
    }
}
