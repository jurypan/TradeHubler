using System.Diagnostics;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Messaging.WebPubSub;
using Azure.Core;
using Newtonsoft.Json;
using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.AzureFunction
{
    public class Tradingview(ILoggerFactory loggerFactory, JCTGDbContext dbContext)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<Tradingview>();
        private readonly JCTGDbContext _dbContext = dbContext;

        [Function("Tradingview")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            // Check for the custom query parameter
            var queryParameters = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var source = queryParameters["source"];

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            if (source == "timer")
            {
                _logger.LogDebug("Call received from Timer Triggered function");
            }
            else
            {
                // Read body from request
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                // TradeJournal item
                _logger.LogDebug($"Request body : {requestBody}");

                try
                {
                    // Parse into a SignalExecuted Alert object
                    var signal = Signal.Parse(requestBody);

                    // TradeJournal item
                    _logger.LogInformation($"Parse object to Signal : AccountID={signal.AccountID}, Type={signal.OrderType}, TickerInMetatrader={signal.Instrument}, CurrentPrice={signal.CurrentPrice}, SL={signal.StopLoss}, TP={signal.TakeProfit}, EntryExpr={signal.EntryExpression}, Risk={signal.Risk}, RRR={signal.RiskRewardRatio}, Magic={signal.Magic}", signal);

                    // Save into the database
                    await _dbContext.Signal.AddAsync(signal);
                    await _dbContext.SaveChangesAsync();

                    // Add log
                    _logger.LogInformation($"Added to database in table Signal with ID : {signal.ID}", signal);

                    // Init Azure Web PubSub
                    var serviceClient = new WebPubSubServiceClient("Endpoint=https://justcalltheguy.webpubsub.azure.com;AccessKey=BdxAvvoxX7+nkCq/lQDNe2LAy41lwDfJD8bCPiNuY/k=;Version=1.0;", "a" + signal.AccountID.ToString());

                    // Create model
                    var model = new MetatraderMessage()
                    {
                        SignalID = signal.ID,
                        AccountID = signal.AccountID,
                        Instrument = signal.Instrument,
                        Magic = signal.Magic,
                        OrderType = signal.OrderType,
                        StrategyType = signal.StrategyType,
                        MarketOrder = signal.OrderType == "BUY" || signal.OrderType == "SELL" ? new MetatraderMessageMarketOrder()
                        {
                            StopLoss = signal.StopLoss,
                            Price = signal.EntryPrice,
                            TakeProfit = signal.TakeProfit,
                        } : null,
                        PassiveOrder = signal.OrderType == "BUYSTOP" || signal.OrderType == "SELLSTOP" ? new MetatraderMessagePassiveOrder()
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
            }

            return response;
        }
    }
}
