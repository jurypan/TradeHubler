using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace JCTG.AzureFunction
{
    public class Metatrader(ILoggerFactory loggerFactory, JCTGDbContext dbContext)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<Metatrader>();
        private readonly JCTGDbContext _dbContext = dbContext;

        [Function("Metatrader")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            // Read body from request
            string jsonString = await new StreamReader(req.Body).ReadToEndAsync();

            // Log item
            _logger.LogInformation($"Request body : {jsonString}");

            // Create httpResponse object
            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            var response = new List<MetatraderResponse>();

            try
            {
                // Parse object
                var items = JsonConvert.DeserializeObject<List<MetatraderRequest>>(jsonString);

                // Do null reference check
                if(items != null) 
                {
                    // Ittirate through the list
                    foreach (var mt in items)
                    {
                        // Log item
                        _logger.LogDebug($"Parsed Metatrader object : AccountID={mt.AccountID}, TickerInMetatrader={mt.TickerInMetatrader}, ClientID={mt.ClientID}, CurrentPrice={mt.Price}, TickerInTradingview={mt.TickerInTradingview}, Strategy={mt.StrategyType}", jsonString);

                        // Get TradingviewAlert from the database
                        var tvAlert = await _dbContext.TradingviewAlert
                                                             .Include(f => f.Trades)
                                                             .Where(f => f.AccountID == mt.AccountID
                                                                         && f.Instrument.Equals(mt.TickerInTradingview)
                                                                         && (f.Trades.Count(g => g.ClientID == mt.ClientID) == 0 || f.Trades.Any(g => g.ClientID == mt.ClientID && g.Executed == false))
                                                                         && f.StrategyType == mt.StrategyType
                                                                         )
                                                             .OrderBy(f => Math.Abs(f.EntryPrice - mt.Price))
                                                             .FirstOrDefaultAsync();

                        // If there is a tradingview alert in the db
                        if (tvAlert == null)
                        {
                            // Log item
                            _logger.LogInformation($"No Tradingview alerts found for ticker {mt.TickerInTradingview}", mt);

                            // Add repsonse
                            response.Add(new MetatraderResponse()
                            {
                                 Action = "NONE",
                                 AccountId = mt.AccountID,
                                 ClientId = mt.ClientID,
                                 TickerInMetatrader = mt.TickerInMetatrader,
                                 TickerInTradingview = mt.TickerInTradingview,
                            });
                        }
                        else
                        {
                            // Get Trade Alert from database based on AccountID / ClientID / TickerInMetatrader that is not executed
                            var trade = tvAlert.Trades.FirstOrDefault(f => f.Instrument.Equals(mt.TickerInMetatrader) && f.Executed == false);

                            // if Not exist
                            if (trade == null)
                            {
                                // Create trade in the database
                                trade = (await _dbContext.Trade.AddAsync(new Trade
                                {
                                    DateCreated = DateTime.UtcNow,
                                    AccountID = mt.AccountID,
                                    ClientID = mt.ClientID,
                                    StrategyType = tvAlert.StrategyType,
                                    Instrument = mt.TickerInMetatrader,
                                    TradingviewAlertID = tvAlert.ID,
                                    Executed = false,
                                    Offset = Math.Round(tvAlert.CurrentPrice - mt.Price, 4, MidpointRounding.AwayFromZero),
                                    Magic = tvAlert.Magic,
                                })).Entity;
                                await _dbContext.SaveChangesAsync();

                                // Log item
                                _logger.LogInformation($"Ticker {mt.TickerInMetatrader} not found, created in the database with ID : {trade.ID}", trade);
                            }

                            // Check if we need to execute the order
                            if (tvAlert.OrderType.Equals("BUY") || (tvAlert.OrderType.Equals("BUYSTOP") && mt.Price + trade.Offset >= tvAlert.EntryPrice))
                            {
                                // Log item
                                _logger.LogWarning($"BUY order is send to Metatrader : BUY,instrument={mt.TickerInMetatrader},price={mt.Price},tp={tvAlert.TakeProfit - trade.Offset},sl={tvAlert.StopLoss - trade.Offset},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mt.Price;
                                trade.ExecutedSL = tvAlert.StopLoss - trade.Offset;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "BUY",
                                    AccountId = mt.AccountID,
                                    ClientId = mt.ClientID,
                                    TickerInMetatrader = mt.TickerInMetatrader,
                                    TickerInTradingview = mt.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset,
                                    StopLoss = tvAlert.StopLoss - trade.Offset,
                                    Magic = tvAlert.Magic,
                                    StrategyType = trade.StrategyType,
                                }); ;
                            }

                            // Check if we need to modify the stop loss to break event
                            else if (tvAlert.OrderType.Equals("MODIFYSLTOBE"))
                            {
                                // Log item
                                _logger.LogWarning($"MODIFY SL order is send to Metatrader : MODIFYSLTOBE,instrument={mt.TickerInMetatrader},price={mt.Price},tp={tvAlert.TakeProfit - trade.Offset},sl={mt.Price - trade.Offset},magic={trade.Magic}", trade);

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "MODIFYSLTOBE",
                                    AccountId = mt.AccountID,
                                    ClientId = mt.ClientID,
                                    TickerInMetatrader = mt.TickerInMetatrader,
                                    TickerInTradingview = mt.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset,
                                    StopLoss = mt.Price - trade.Offset,
                                    Magic = tvAlert.Magic,
                                    StrategyType = trade.StrategyType,
                                });
                            }

                            // Check if we need to modify the order
                            else if (tvAlert.OrderType.Equals("MODIFYSL"))
                            {
                                // Log item
                                _logger.LogWarning($"MODIFY SL order is send to Metatrader : MODIFYSL,instrument={mt.TickerInMetatrader},price={mt.Price},tp={tvAlert.TakeProfit - trade.Offset},sl={mt.Price - trade.Offset},magic={trade.Magic}", trade);

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "MODIFYSL",
                                    AccountId = mt.AccountID,
                                    ClientId = mt.ClientID,
                                    TickerInMetatrader = mt.TickerInMetatrader,
                                    TickerInTradingview = mt.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset,
                                    StopLoss = mt.Price - trade.Offset,
                                    Magic = tvAlert.Magic,
                                    StrategyType = trade.StrategyType,
                                });
                            }

                            // Check if we need to execute the order
                            else if (tvAlert.OrderType.Equals("CLOSE"))
                            {
                                // Log item
                                _logger.LogWarning($"CLOSE order is send to Metatrader : CLOSE,instrument={mt.TickerInMetatrader},price={mt.Price},tp={tvAlert.TakeProfit - trade.Offset},sl={tvAlert.StopLoss - trade.Offset},magic={trade.Magic}", trade);

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "CLOSE",
                                    AccountId = mt.AccountID,
                                    ClientId = mt.ClientID,
                                    TickerInMetatrader = mt.TickerInMetatrader,
                                    TickerInTradingview = mt.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset,
                                    StopLoss = tvAlert.StopLoss - trade.Offset,
                                    Magic = tvAlert.Magic,
                                    StrategyType = trade.StrategyType,
                                });
                            }
                            else
                            {
                                // Log item
                                _logger.LogInformation($"No metatrader trade found for ticker {mt.TickerInTradingview}", mt);

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "NONE",
                                    AccountId = mt.AccountID,
                                    ClientId = mt.ClientID,
                                    TickerInMetatrader = mt.TickerInMetatrader,
                                    TickerInTradingview = mt.TickerInTradingview,
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log item
                _logger.LogError($"Tradingview ||\nMessage: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
            }


            await httpResponse.WriteAsJsonAsync(response);
            return httpResponse;
        }
    }
}
