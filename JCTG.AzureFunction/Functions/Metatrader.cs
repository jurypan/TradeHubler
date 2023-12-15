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
                    foreach (var mtTrade in items)
                    {
                        // Log item
                        _logger.LogDebug($"Parsed Metatrader object : AccountID={mtTrade.AccountID}, TickerInMetatrader={mtTrade.TickerInMetatrader}, ClientID={mtTrade.ClientID}, CurrentPrice={mtTrade.Price}, TickerInTradingview={mtTrade.TickerInTradingview}, Strategy={mtTrade.StrategyType}", jsonString);

                        // Get TradingviewAlert from the database
                        var tvAlert = await _dbContext.TradingviewAlert
                             .Include(f => f.Trades)
                             .Where(f => f.AccountID == mtTrade.AccountID
                                         && f.Instrument.Equals(mtTrade.TickerInTradingview)
                                         && (
                                             (f.Trades.Count(g => g.ClientID == mtTrade.ClientID) == 0 && f.DateCreated > DateTime.Now.AddMinutes(-10))
                                             || f.Trades.Any(g => g.ClientID == mtTrade.ClientID && g.Executed == false)
                                            )
                                         && f.StrategyType == mtTrade.StrategyType
                                         )
                             .OrderBy(f => Math.Abs(f.EntryPrice - mtTrade.Price))
                             .FirstOrDefaultAsync();


                        // If there is a tradingview alert in the db
                        if (tvAlert == null)
                        {
                            // Log item
                            _logger.LogInformation($"No Tradingview alerts found for ticker {mtTrade.TickerInTradingview}", mtTrade);

                            // Add repsonse
                            response.Add(new MetatraderResponse()
                            {
                                 Action = "NONE",
                                 AccountId = mtTrade.AccountID,
                                 ClientId = mtTrade.ClientID,
                                 TickerInMetatrader = mtTrade.TickerInMetatrader,
                                 TickerInTradingview = mtTrade.TickerInTradingview,
                            });
                        }
                        else
                        {
                            // Get Trade Alert from database based on AccountID / ClientID / TickerInMetatrader that is not executed
                            var trade = tvAlert.Trades.FirstOrDefault(f => f.Instrument.Equals(mtTrade.TickerInMetatrader) && f.Executed == false);

                            // if Not exist
                            if (trade == null)
                            {
                                // Create trade in the database
                                trade = (await _dbContext.Trade.AddAsync(new Trade
                                {
                                    DateCreated = DateTime.UtcNow,
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    StrategyType = tvAlert.StrategyType,
                                    Instrument = mtTrade.TickerInMetatrader,
                                    TradingviewAlertID = tvAlert.ID,
                                    Executed = false,
                                    Offset = Math.Round(tvAlert.CurrentPrice - mtTrade.Price, 4, MidpointRounding.AwayFromZero),
                                    Spread = Math.Round(mtTrade.Spread, 4, MidpointRounding.AwayFromZero),
                                    Magic = tvAlert.Magic,
                                })).Entity;
                                await _dbContext.SaveChangesAsync();

                                // Log item
                                _logger.LogInformation($"Ticker {mtTrade.TickerInMetatrader} not found, created in the database with ID : {trade.ID}", trade);
                            }

                            // Check if we need to execute the order
                            if (tvAlert.OrderType.Equals("BUY") 
                                || (tvAlert.OrderType.Equals("BUYSTOP") && mtTrade.Price + trade.Offset >= tvAlert.EntryPrice)
                                || (tvAlert.OrderType.Equals("BUYLIMIT") && mtTrade.Price + trade.Offset <= tvAlert.EntryPrice)
                                )
                            {
                                // Log item
                                _logger.LogWarning($"BUY order is send to Metatrader : BUY,instrument={mtTrade.TickerInMetatrader},price={mtTrade.Price},tp={tvAlert.TakeProfit - trade.Offset - trade.Spread},sl={tvAlert.StopLoss - trade.Offset - trade.Spread},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mtTrade.Price;
                                trade.ExecutedSL = tvAlert.StopLoss - trade.Offset - trade.Spread;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset - trade.Spread;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "BUY",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset - trade.Spread,
                                    StopLoss = tvAlert.StopLoss - trade.Offset - trade.Spread,
                                    Magic = tvAlert.Magic,
                                    StrategyType = trade.StrategyType,
                                });
                            }

                            else if (tvAlert.OrderType.Equals("SELL")
                                    || (tvAlert.OrderType.Equals("SELLSTOP") && mtTrade.Price + trade.Offset <= tvAlert.EntryPrice)
                                    || (tvAlert.OrderType.Equals("SELLLIMIT") && mtTrade.Price + trade.Offset >= tvAlert.EntryPrice)
                                    )
                            {
                                // Log item
                                _logger.LogWarning($"SELL order is send to Metatrader : SELL,instrument={mtTrade.TickerInMetatrader},price={mtTrade.Price},tp={tvAlert.TakeProfit - trade.Offset - trade.Spread},sl={tvAlert.StopLoss - trade.Offset - trade.Spread},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mtTrade.Price;
                                trade.ExecutedSL = tvAlert.StopLoss - trade.Offset - trade.Spread;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset - trade.Spread;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "SELL",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset - trade.Spread,
                                    StopLoss = tvAlert.StopLoss - trade.Offset - trade.Spread,
                                    Magic = tvAlert.Magic,
                                    StrategyType = trade.StrategyType,
                                });
                            }

                            // Check if we need to modify the stop loss to break event
                            else if (tvAlert.OrderType.Equals("MODIFYSLTOBE"))
                            {
                                // Log item
                                _logger.LogWarning($"MODIFY SL order is send to Metatrader : MODIFYSLTOBE,instrument={mtTrade.TickerInMetatrader},price={mtTrade.Price},tp={tvAlert.TakeProfit - trade.Offset - trade.Spread},sl={mtTrade.Price},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mtTrade.Price;
                                trade.ExecutedSL = mtTrade.Price;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset - trade.Spread;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "MODIFYSLTOBE",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset - trade.Spread,
                                    StopLoss = mtTrade.Price,
                                    Magic = tvAlert.Magic,
                                    StrategyType = trade.StrategyType,
                                });
                            }

                            // Check if we need to modify the order
                            else if (tvAlert.OrderType.Equals("MODIFYSL"))
                            {
                                // Log item
                                _logger.LogWarning($"MODIFY SL order is send to Metatrader : MODIFYSL,instrument={mtTrade.TickerInMetatrader},price={mtTrade.Price},tp={tvAlert.TakeProfit - trade.Offset - trade.Spread},sl={mtTrade.Price - trade.Offset - trade.Spread},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mtTrade.Price;
                                trade.ExecutedSL = tvAlert.StopLoss - trade.Offset - trade.Spread;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset - trade.Spread;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "MODIFYSL",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset - trade.Spread,
                                    StopLoss = mtTrade.Price - trade.Offset - trade.Spread,
                                    Magic = tvAlert.Magic,
                                    StrategyType = trade.StrategyType,
                                });
                            }

                            // Check if we need to execute the order
                            else if (tvAlert.OrderType.Equals("CLOSE"))
                            {
                                // Log item
                                _logger.LogWarning($"CLOSE order is send to Metatrader : CLOSE,instrument={mtTrade.TickerInMetatrader},price={mtTrade.Price},tp={tvAlert.TakeProfit - trade.Offset - trade.Spread},sl={tvAlert.StopLoss - trade.Offset - trade.Spread},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mtTrade.Price;
                                trade.ExecutedSL = tvAlert.StopLoss - trade.Offset - trade.Spread;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset - trade.Spread;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "CLOSE",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset - trade.Spread,
                                    StopLoss = tvAlert.StopLoss - trade.Offset - trade.Spread,
                                    Magic = tvAlert.Magic,
                                    StrategyType = trade.StrategyType,
                                });
                            }

                            else
                            {
                                // Log item
                                _logger.LogInformation($"No metatrader trade found for ticker {mtTrade.TickerInTradingview}", mtTrade);

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "NONE",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
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
