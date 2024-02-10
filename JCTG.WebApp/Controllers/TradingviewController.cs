using Azure.Core;
using Azure.Messaging.WebPubSub;
using JCTG.Entity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;


namespace JCTG.WebApp.Controllers
{
    [ApiController]
    [Route("API")]
    public class TradingviewController : ControllerBase
    {
        private readonly ILogger<TradingviewController> _logger;
        private readonly JCTGDbContext _dbContext;

        public TradingviewController(ILogger<TradingviewController> logger, JCTGDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        [HttpGet("Tradingview")]
        [HttpPost("Tradingview")]
        public async Task<IActionResult> TradingView()
        {
            var source = Request.Query["source"];

            if (source == "timer")
            {
                _logger.LogDebug("Call received from Timer Triggered function");
                return Ok("Timer source detected");
            }
            else
            {
                // Read body from request
                string requestBody;
                using (var reader = new StreamReader(Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                _logger.LogDebug($"Request body: {requestBody}");

                try
                {
                    var signal = Signal.Parse(requestBody);

                    _logger.LogInformation($"Parsed object to Signal : {JsonConvert.SerializeObject(signal)}", signal);

                    await _dbContext.Signal.AddAsync(signal);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation($"Added to database in table Signal with ID: {signal.ID}", signal);

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

                    var resp = await serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<TradingviewSignal>()
                    {
                        Data = model,
                        DataType = Constants.WebsocketMessageDatatype_JSON,
                        From = Constants.WebsocketMessageFrom_Server,
                        Type = Constants.WebsocketMessageType_Message,
                        TypeName = nameof(TradingviewSignal),
                    }), ContentType.ApplicationJson);

                    _logger.LogInformation($"Sent to Azure Web PubSub with response client request id: {resp.ClientRequestId}", resp);

                    return Ok("Processed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
                    return StatusCode(500, "Internal server error");
                }
            }
        }
    }
}
