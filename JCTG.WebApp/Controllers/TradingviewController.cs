using Azure.Core;
using Azure.Messaging.WebPubSub;
using JCTG.Entity;
using JCTG.Events;
using JCTG.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Mime;


namespace JCTG.WebApp.Controllers
{
    [ApiController]
    [Route("api")]
    public class TradingviewController : ControllerBase
    {
        private readonly ILogger<TradingviewController> _logger;
        private readonly JCTGDbContext _dbContext;
        private readonly AzurePubSubServer _server;

        public TradingviewController(ILogger<TradingviewController> logger, JCTGDbContext dbContext, AzurePubSubServer server)
        {
            _logger = logger;
            _dbContext = dbContext;
            _server = server;
        }

        [HttpGet("tradingview")]
        [HttpPost("tradingview")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TradingView()
        {
            var code = Request.Query["code"];

            if (code != "Ocebxtg1excWosFez5rWMtNp3ZsmIzSFQ0XhqtrfHlMuAzFuQ0OGhA==")
            {
                _logger.LogDebug("code is not ok");
                return BadRequest();
            }

            // Read body from request
            string requestBody;
            using (var reader = new StreamReader(Request.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            _logger.LogDebug($"Request body: {requestBody}");


            if(!string.IsNullOrEmpty(requestBody)) 
            {
                try
                {
                    var signal = Signal.Parse(requestBody);

                    _logger.LogInformation($"Parsed object to Signal : {JsonConvert.SerializeObject(signal)}", signal);

                    await _dbContext.Signal.AddAsync(signal);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation($"Added to database in table Signal with ID: {signal.ID}", signal);

                    // Create model
                    var id = await _server.SendOnTradingviewSignalEventAsync(signal.AccountID, new OnReceivingTradingviewSignalEvent()
                    {
                        SignalID = signal.ID,
                        AccountID = signal.AccountID,
                        Instrument = signal.Instrument,
                        Magic = signal.Magic,
                        OrderType = signal.OrderType,
                        StrategyType = signal.StrategyType,
                        MarketOrder = signal.OrderType == "BUY" || signal.OrderType == "SELL" ? new OnReceivingTradingviewSignalEventMarketOrder()
                        {
                            StopLoss = signal.StopLoss,
                            Price = signal.EntryPrice,
                            TakeProfit = signal.TakeProfit,
                        } : null,
                        PassiveOrder = signal.OrderType == "BUYSTOP" || signal.OrderType == "SELLSTOP" ? new OnReceivingTradingviewSignalEventPassiveOrder()
                        {
                            EntryExpression = signal.EntryExpression,
                            Risk = signal.Risk,
                            RiskRewardRatio = signal.RiskRewardRatio,
                        } : null,
                    });

                    _logger.LogInformation($"Sent to Azure Web PubSub with response client request id: {id}", id);

                   
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
                    return StatusCode(500, "Internal server error");
                }
            }

            return Ok("Processed successfully");
        }
    }
}
