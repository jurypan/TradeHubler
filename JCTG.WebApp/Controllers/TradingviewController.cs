using JCTG.Entity;
using JCTG.Events;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;


namespace JCTG.WebApp.Controllers
{
    [ApiController]
    [Route("api")]
    public class TradingviewController : ControllerBase
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<TradingviewController>();
        private readonly JCTGDbContext _dbContext;
        private readonly AzurePubSubServer _server;

        public TradingviewController(JCTGDbContext dbContext, AzurePubSubServer server)
        {
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
                _logger.Debug("code is not ok");
                return BadRequest();
            }

            // Read body from request
            string requestBody;
            using (var reader = new StreamReader(Request.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            _logger.Debug($"Request body: {requestBody}");


            if(!string.IsNullOrEmpty(requestBody)) 
            {
                try
                {
                    var signal = Signal.Parse(requestBody);

                    _logger.Information($"Parsed object to Signal : {JsonConvert.SerializeObject(signal)}", signal);

                    await _dbContext.Signal.AddAsync(signal);
                    await _dbContext.SaveChangesAsync();

                    _logger.Information($"Added to database in table Signal with ID: {signal.ID}", signal);

                    // Create model
                    var id = await _server.SendOnTradingviewSignalEventAsync(signal.AccountID, new OnReceivingTradingviewSignalEvent()
                    {
                        SignalID = signal.ID,
                        AccountID = signal.AccountID,
                        Instrument = signal.Instrument,
                        Magic = signal.ID,
                        OrderType = signal.OrderType,
                        StrategyType = signal.StrategyType,
                        MarketOrder = signal.OrderType == "BUY" || signal.OrderType == "SELL" ? new OnReceivingTradingviewSignalEventMarketOrder()
                        {
                            StopLoss = Convert.ToDecimal(signal.StopLoss),
                            Price = Convert.ToDecimal(signal.EntryPrice),
                            TakeProfit = Convert.ToDecimal(signal.TakeProfit),
                        } : null,
                        PassiveOrder = signal.OrderType == "BUYSTOP" || signal.OrderType == "SELLSTOP" ? new OnReceivingTradingviewSignalEventPassiveOrder()
                        {
                            EntryExpression = signal.EntryExpression,
                            Risk = Convert.ToDecimal(signal.Risk),
                            RiskRewardRatio = Convert.ToDecimal(signal.RiskRewardRatio),
                        } : null,
                    });

                    _logger.Information($"Sent to Azure Web PubSub with response client request id: {id}", id);

                   
                }
                catch (Exception ex)
                {
                    _logger.Error($"Exception: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
                    return StatusCode(500, "Internal server error");
                }
            }

            return Ok("Processed successfully");
        }
    }
}
