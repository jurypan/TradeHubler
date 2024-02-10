using System.Net;
using Azure.Core;
using Azure.Messaging.WebPubSub;
using JCTG.Entity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace JCTG.AzureFunction.Functions
{
    public class RetrySignal(ILoggerFactory loggerFactory, JCTGDbContext dbContext)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<RetrySignal>();
        private readonly JCTGDbContext _dbContext = dbContext;


        [Function("RetrySignal")]
        public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            // Check for the custom query parameter
            var queryParameters = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var source = queryParameters["id"];

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");


            try
            {
                // Get id
                var id = int.Parse(source);

                // Parse into a SignalExecuted Alert object
                var signal = await _dbContext.Signal.FirstAsync(f => f.ID == id);

                // TradeJournal item
                _logger.LogInformation($"Parse object to Signal : AccountID={signal.AccountID}, Type={signal.OrderType}, TickerInMetatrader={signal.Instrument}, CurrentPrice={signal.CurrentPrice}, SL={signal.StopLoss}, TP={signal.TakeProfit}, EntryExpr={signal.EntryExpression}, Risk={signal.Risk}, RRR={signal.RiskRewardRatio}, Magic={signal.Magic}", signal);

                // Add log
                _logger.LogInformation($"Get from database Signal with ID : {signal.ID}", signal);

                // Init Azure Web PubSub
                var serviceClient = new WebPubSubServiceClient("Endpoint=https://justcalltheguy.webpubsub.azure.com;AccessKey=BdxAvvoxX7+nkCq/lQDNe2LAy41lwDfJD8bCPiNuY/k=;Version=1.0;", "a" + signal.AccountID.ToString());

                // Create model
                var model = new TradingviewSignal()
                {
                    SignalID = signal.ID,
                    AccountID = signal.AccountID,
                    Instrument = signal.Instrument,
                    Magic = signal.Magic,
                    OrderType = signal.OrderType,
                    StrategyType = signal.StrategyType,
                    MarketOrder = signal.OrderType == "BUY" || signal.OrderType == "SELL" ? new TradingviewSignalMarketOrder()
                    {
                        StopLoss = signal.StopLoss,
                        Price = signal.EntryPrice,
                        TakeProfit = signal.TakeProfit,
                    } : null,
                    PassiveOrder = signal.OrderType == "BUYSTOP" || signal.OrderType == "SELLSTOP" ? new TradingviewSignalPassiveOrder()
                    {
                        EntryExpression = signal.EntryExpression,
                        Risk = signal.Risk,
                        RiskRewardRatio = signal.RiskRewardRatio,
                    } : null,
                };

                // Send to all clients
                var resp = await serviceClient.SendToAllAsync(JsonConvert.SerializeObject(model), ContentType.ApplicationJson);

                // Add log
                _logger.LogInformation($"Send to Azure Web PubSub with response client request id : {resp.ClientRequestId}", resp);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
            }

            return response;
        }
    }
}
