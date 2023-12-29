using System.Net;
using JCTG.AzureFunction.Helpers;
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

            // TradeJournal item
            _logger.LogInformation($"Request body : {jsonString}");

            // Create httpResponse object
            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            var response = new List<MetatraderResponse>();

            try
            {
                // Parse object
                var items = JsonConvert.DeserializeObject<List<MetatraderRequest>>(jsonString);

                // Do null reference check
                if (items != null)
                {
                    // Ittirate through the list
                    foreach (var mtTrade in items)
                    {
                        // TradeJournal item
                        _logger.LogDebug($"Parsed Metatrader object : AccountID={mtTrade.AccountID}, TickerInMetatrader={mtTrade.TickerInMetatrader}, ClientID={mtTrade.ClientID}, CurrentPrice={mtTrade.Ask}, TickerInTradingview={mtTrade.TickerInTradingview}, Strategy={mtTrade.StrategyType}", jsonString);

                        // Get TradingviewAlert BUY or SELL orders from the database
                        foreach (var tvAlert in await _dbContext.TradingviewAlert.Where(f =>
                                                            f.AccountID == mtTrade.AccountID
                                                            && f.Executed == false
                                                            && (f.OrderType.Contains("BUY") || f.OrderType.Contains("SELL"))
                                                            ).ToListAsync())
                        {
                            // Get current price
                            var price = await new FinancialModelingPrepApiClient().GetPriceAsync(mtTrade.TickerInFMP);

                            // Caulcate offset
                            var offset = Math.Round(price - mtTrade.Ask, 4, MidpointRounding.AwayFromZero);

                            // Caulcate spread
                            var spread = Math.Round(Math.Abs(mtTrade.Ask - mtTrade.Bid), 4, MidpointRounding.AwayFromZero);

                            // Check if we need to execute the order
                            if ((tvAlert.OrderType.Equals("BUY") || tvAlert.OrderType.Equals("BUYSTOP")) && price > 0.0 && price >= tvAlert.EntryPrice)
                            {
                                // Caculate SL Price
                                var slPrice = CalculateSLForLong(
                                            mtPrice: mtTrade.Ask,
                                            mtAtr: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, tvAlert.StrategyType),
                                            tvPrice: tvAlert.EntryPrice,
                                            tvSlPrice: tvAlert.StopLoss,
                                            tvAtr: GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, tvAlert.StrategyType),
                                            offset: offset);

                                // Add to log
                                await _dbContext.Log.AddAsync(new Log()
                                {
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    DateCreated = DateTime.UtcNow,
                                    Type = "BACKEND - STOPLOSS CALCULATION",
                                    Message = string.Format($"Type=BUY,StrategypType={tvAlert.StrategyType},MtAsk={mtTrade.Ask},Atr={GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, tvAlert.StrategyType)},TvEntryPrice={tvAlert.EntryPrice},TvSlPrice={tvAlert.StopLoss},TvAtr={GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, tvAlert.StrategyType)},Offset={offset}"),
                                });

                                // TradeJournal item
                                _logger.LogWarning($"BUY order is send to Metatrader : BUY,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tvAlert.TakeProfit - offset},sl={slPrice},magic={tvAlert.Magic}", tvAlert);

                                // Update database
                                tvAlert.Executed = true;
                                tvAlert.DateExecuted = DateTime.UtcNow;
                                await _dbContext.Trade.AddAsync(new Trade
                                {
                                    DateCreated = DateTime.UtcNow,
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    StrategyType = tvAlert.StrategyType,
                                    Instrument = mtTrade.TickerInMetatrader,
                                    TradingviewAlertID = tvAlert.ID,
                                    Executed = true,
                                    DateExecuted = DateTime.UtcNow,
                                    Offset = offset, // offset in regards of tradingview. If negative amount -> TV price is lower
                                    Spread = Math.Round(Math.Abs(mtTrade.Ask - mtTrade.Bid), 4, MidpointRounding.AwayFromZero),
                                    Magic = tvAlert.Magic,
                                    ExecutedPrice = mtTrade.Ask,
                                    ExecutedSL = slPrice,
                                    ExecutedTP = tvAlert.TakeProfit - offset,
                                });


                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "BUY",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - offset,
                                    StopLoss = slPrice,
                                    Magic = tvAlert.Magic,
                                    StrategyType = tvAlert.StrategyType,
                                });
                            }
                            else if ((tvAlert.OrderType.Equals("SELL") || tvAlert.OrderType.Equals("SELLSTOP")) && price > 0.0 && price <= tvAlert.EntryPrice)
                            {
                                // Caculate SL Price
                                var slPrice = CalculateSLForShort(
                                            mtPrice: mtTrade.Bid,
                                            mtAtr: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, tvAlert.StrategyType),
                                            tvPrice: tvAlert.EntryPrice,
                                            tvSlPrice: tvAlert.StopLoss,
                                            tvAtr: GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, tvAlert.StrategyType),
                                            offset: offset,
                                            spread: spread);

                                // Add to log
                                await _dbContext.Log.AddAsync(new Log()
                                {
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    DateCreated = DateTime.UtcNow,
                                    Type = "BACKEND - STOPLOSS CALCULATION",
                                    Message = string.Format($"Type=SELL,StrategypType={tvAlert.StrategyType},MtAsk={mtTrade.Ask},Atr={GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, tvAlert.StrategyType)},TvEntryPrice={tvAlert.EntryPrice},TvSlPrice={tvAlert.StopLoss},TvAtr={GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, tvAlert.StrategyType)},Offset={offset},Spread={spread}"),
                                });

                                // TradeJournal item
                                _logger.LogWarning($"SELL order is send to Metatrader : SELL,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tvAlert.TakeProfit - offset},sl={slPrice},magic={tvAlert.Magic}", tvAlert);

                                // Update database
                                tvAlert.Executed = true;
                                tvAlert.DateExecuted = DateTime.UtcNow;
                                await _dbContext.Trade.AddAsync(new Trade
                                {
                                    DateCreated = DateTime.UtcNow,
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    StrategyType = tvAlert.StrategyType,
                                    Instrument = mtTrade.TickerInMetatrader,
                                    TradingviewAlertID = tvAlert.ID,
                                    Executed = true,
                                    DateExecuted = DateTime.UtcNow,
                                    Offset = offset, // offset in regards of tradingview. If negative amount -> TV price is lower
                                    Spread = spread,
                                    Magic = tvAlert.Magic,
                                    ExecutedPrice = mtTrade.Ask,
                                    ExecutedSL = slPrice,
                                    ExecutedTP = tvAlert.TakeProfit - offset,
                                });

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "SELL",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - offset,
                                    StopLoss = slPrice,
                                    Magic = tvAlert.Magic,
                                    StrategyType = tvAlert.StrategyType,
                                });
                            }
                        }

                        // Get TradingviewAlert CANCEL, MODIFY SL orders from the database
                        foreach (var tvAlert in await _dbContext.TradingviewAlert.Where(f =>
                                                            f.AccountID == mtTrade.AccountID
                                                            && f.Executed == false
                                                            && (f.OrderType.Equals("MODIFYSLTOBE") || f.OrderType.Equals("MODIFYSL") || f.OrderType.Equals("CLOSE"))
                                                            ).ToListAsync())
                        {

                            // Get the trade from the tradejournal db
                            var tradeJournal = await _dbContext.TradeJournal.FirstOrDefaultAsync(f =>
                                                               f.ClientID == mtTrade.ClientID
                                                           && f.AccountID == mtTrade.AccountID
                                                           && f.Instrument == mtTrade.TickerInMetatrader
                                                           && f.StrategyType == mtTrade.StrategyType
                                                           && f.Magic == tvAlert.Magic);

                            // Do null reference check
                            if (tradeJournal != null)
                            {
                                // Check if we need to modify the stop loss to break event
                                if (tvAlert.OrderType.Equals("MODIFYSLTOBE") || tvAlert.OrderType.Equals("MODIFYSL"))
                                {
                                    // TradeJournal item
                                    _logger.LogWarning($"MODIFY SL order is send to Metatrader : MODIFYSLTOBE,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tradeJournal.TP},sl={tradeJournal.OpenPrice},magic={tvAlert.Magic}");

                                    // Update database
                                    tvAlert.Executed = true;
                                    tvAlert.DateExecuted = DateTime.UtcNow;

                                    // Add repsonse
                                    response.Add(new MetatraderResponse()
                                    {
                                        Action = "MODIFYSLTOBE",
                                        AccountId = mtTrade.AccountID,
                                        ClientId = mtTrade.ClientID,
                                        TickerInMetatrader = mtTrade.TickerInMetatrader,
                                        TickerInTradingview = mtTrade.TickerInTradingview,
                                        TakeProfit = tradeJournal.TP,
                                        StopLoss = tradeJournal.OpenPrice,
                                        Magic = tvAlert.Magic,
                                        StrategyType = tvAlert.StrategyType,
                                        TicketId = tradeJournal.TicketId
                                    }); ;
                                }

                                // Check if we need to execute the order
                                else if (tvAlert.OrderType.Equals("CLOSE"))
                                {
                                    // TradeJournal item
                                    _logger.LogWarning($"CLOSE order is send to Metatrader : CLOSE,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tradeJournal.TP},sl={tradeJournal.SL},magic={tvAlert.Magic}");

                                    // Update database
                                    tvAlert.Executed = true;
                                    tvAlert.DateExecuted = DateTime.UtcNow;

                                    // Add repsonse
                                    response.Add(new MetatraderResponse()
                                    {
                                        Action = "CLOSE",
                                        AccountId = mtTrade.AccountID,
                                        ClientId = mtTrade.ClientID,
                                        TickerInMetatrader = mtTrade.TickerInMetatrader,
                                        TickerInTradingview = mtTrade.TickerInTradingview,
                                        TakeProfit = tradeJournal.TP,
                                        StopLoss = tradeJournal.SL,
                                        Magic = tvAlert.Magic,
                                        StrategyType = tvAlert.StrategyType,
                                    });
                                }
                            }
                        }
                    }

                    // Save DB
                    await _dbContext.SaveChangesAsync();

                    // Foreach item in the list, if there is no command, add "NONE" as command
                    foreach (var mtTrade in items)
                    {
                        if(!response.Any(f => f.TickerInMetatrader == mtTrade.TickerInMetatrader))
                        {
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
            catch (Exception ex)
            {
                // TradeJournal item
                _logger.LogError($"Message: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
            }


            await httpResponse.WriteAsJsonAsync(response);
            return httpResponse;
        }


        /// <summary>
        /// Calculate the stop loss price for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK price</param>
        /// <param name="mtAtr">MetaTrader Atr5M</param>
        /// <param name="tvPrice">TradingView ENTRY price</param>
        /// <param name="tvSlPrice">TradingView SL price</param>
        /// <param name="tvAtr">TradingView Atr5M</param>
        /// <param name="offset">Offset value</param>
        /// <returns>Stop loss price</returns>
        public static double CalculateSLForLong(double mtPrice, double mtAtr, double tvPrice, double tvSlPrice, double tvAtr, double offset)
        {
            // Calculate the Atr5M multiplier based on the difference between MetaTrader's Atr5M and TradingView's Atr5M
            var atrMultiplier = mtAtr > 0 && tvAtr > 0 && mtAtr > tvAtr ? mtAtr / tvAtr : 1.0;

            // Calculate the stop loss price
            var slPrice = tvSlPrice - offset;

            // Check if Atr5M is equal to 1
            if (atrMultiplier > 1.0)
            {
                // Calculate SL price using MetaTrader price minus risk to take
                slPrice = mtPrice - ((tvPrice - tvSlPrice) * atrMultiplier);
            }

            return Math.Round(slPrice, 4, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Calculate the stop loss price for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK price</param>
        /// <param name="mtAtr">MetaTrader Atr5M</param>
        /// <param name="tvPrice">TradingView ENTRY price</param>
        /// <param name="tvSlPrice">TradingView SL price</param>
        /// <param name="tvAtr">TradingView Atr5M</param>
        /// <param name="offset">Offset value</param>
        /// <param name="spread">The spread value</param>
        /// <returns>Stop loss price</returns>
        public static double CalculateSLForShort(double mtPrice, double mtAtr, double tvPrice, double tvSlPrice, double tvAtr, double offset, double spread)
        {
            // Calculate the Atr5M multiplier based on the difference between MetaTrader's Atr5M and TradingView's Atr5M
            var atrMultiplier = mtAtr > 0 && tvAtr > 0 && mtAtr > tvAtr ? mtAtr / tvAtr : 1.0;

            // Calculate the stop loss price
            var slPrice = tvSlPrice - offset;

            // Check if Atr5M is equal to 1
            if (atrMultiplier > 1.0)
            {
                // Calculate SL price using MetaTrader price minus risk to take
                slPrice = mtPrice + ((tvSlPrice - tvPrice) * atrMultiplier);
            }

            // Caculate the spread on top of it
            slPrice = slPrice + spread;

            return Math.Round(slPrice, 4, MidpointRounding.AwayFromZero);
        }

        private double GetAtr(double atr5M, double atr15M, double atr1H, double atrD, StrategyType type)
        {
            if (type == StrategyType.Strategy1)
                return atr1H;
            else if (type == StrategyType.Strategy2)
                return atr1H;
            return atr15M;
        }
    }
}
