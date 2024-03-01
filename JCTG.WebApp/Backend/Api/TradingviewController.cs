using JCTG.Command;
using JCTG.Entity;
using JCTG.Events;
using JCTG.WebApp.Backend.Websocket;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;


namespace JCTG.WebApp.Backend.Api
{
    [ApiController]
    [Route("api")]
    public class TradingviewController : ControllerBase
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<TradingviewController>();
        private readonly JCTGDbContext _dbContext;
        private readonly AzurePubSubClient _server;

        public TradingviewController(JCTGDbContext dbContext, AzurePubSubClient server)
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


            if (!string.IsNullOrEmpty(requestBody))
            {
                try
                {
                    var signal = Signal.Parse(requestBody);

                    _logger.Information($"Parsed object to Signal : {JsonConvert.SerializeObject(signal)}", signal);

                    // Check the order type of the signal
                    switch (signal.OrderType.ToLower())
                    {
                        case "buy":
                        case "buystop":
                        case "buylimit":
                        case "sell":
                        case "selllimit":
                        case "sellstop":
                            // If the order type is one of the order types, add the signal to the database
                            await _dbContext.Signal.AddAsync(signal);

                            // Save to the database
                            await _dbContext.SaveChangesAsync();

                            // Add to the tradingview alert
                            await _dbContext.TradingviewAlert.AddAsync(new TradingviewAlert()
                            {
                                DateCreated = DateTime.UtcNow,
                                RawMessage = requestBody,
                                SignalID = signal.ID,
                                Type = TradingviewAlert.ParseTradingviewAlertTypeOrDefault(signal.OrderType.ToLower()),
                            });

                            // Save to the database
                            await _dbContext.SaveChangesAsync();

                            // Add log
                            _logger.Information($"Added to database in table Signal with ID: {signal.ID}", signal);

                            // Create model and send to the client
                            var id = await _server.SendOnTradingviewSignalCommandAsync(signal.AccountID, new OnSendTradingviewSignalCommand()
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

                            // Add log
                            _logger.Information($"Sent to Azure Web PubSub with response client request id: {id}", id);
                            break;
                        case "entry":
                        case "cancelorder":
                        case "movesltobe":
                        case "tphit":
                        case "slhit":
                        case "behit":
                            // Implement the logic to update the database based on instrument, client, and magic number.
                            // This is a placeholder for your actual update logic.
                            var existingSignals = _dbContext.Signal.Where(s => s.Instrument == signal.Instrument && s.AccountID == signal.AccountID && s.Magic == signal.Magic && s.StrategyType == signal.StrategyType);

                            foreach (var existingSignal in existingSignals)
                            {
                                // Update properties based on your logic
                                // For example: existingSignal.Status = "Updated";
                                if (signal.OrderType.Equals("tphit", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.TradingviewStateType = TradingviewStateType.TpHit;
                                else if (signal.OrderType.Equals("slhit", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.TradingviewStateType = TradingviewStateType.SlHit;
                                else if (signal.OrderType.Equals("behit", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.TradingviewStateType = TradingviewStateType.BeHit;
                                else if (signal.OrderType.Equals("entry", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.TradingviewStateType = TradingviewStateType.Entry;
                                else if (signal.OrderType.Equals("cancelorder", StringComparison.CurrentCultureIgnoreCase))
                                    existingSignal.TradingviewStateType = TradingviewStateType.Cancel;
                                _dbContext.Signal.Update(existingSignal);

                                // Add to the tradingview alert
                                await _dbContext.TradingviewAlert.AddAsync(new TradingviewAlert()
                                {
                                    DateCreated = DateTime.UtcNow,
                                    RawMessage = requestBody,
                                    SignalID = existingSignal.ID,
                                    Type = TradingviewAlert.ParseTradingviewAlertTypeOrDefault(signal.OrderType.ToLower()),
                                });
                            }

                            // Save to the database
                            await _dbContext.SaveChangesAsync();

                            break;
                        default:
                            // Optionally, handle unknown order types
                            _logger.Warning($"Unknown or not used ordertype: {signal.OrderType}");
                            break;
                    }


                   
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
