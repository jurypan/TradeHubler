using System.Diagnostics;
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
                if(items != null) 
                {
                    // Ittirate through the list
                    foreach (var mtTrade in items)
                    {
                        // TradeJournal item
                        _logger.LogDebug($"Parsed Metatrader object : AccountID={mtTrade.AccountID}, TickerInMetatrader={mtTrade.TickerInMetatrader}, ClientID={mtTrade.ClientID}, CurrentPrice={mtTrade.Ask}, TickerInTradingview={mtTrade.TickerInTradingview}, Strategy={mtTrade.StrategyType}", jsonString);

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
                             .OrderBy(f => Math.Abs(f.EntryPrice - mtTrade.Ask))
                             .FirstOrDefaultAsync();


                        // If there is a tradingview alert in the db
                        if (tvAlert == null)
                        {
                            // TradeJournal item
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
                                    Offset = Math.Round(tvAlert.CurrentPrice - mtTrade.Ask, 4, MidpointRounding.AwayFromZero),
                                    Spread = Math.Round(Math.Abs(mtTrade.Ask - mtTrade.Bid), 4, MidpointRounding.AwayFromZero),
                                    Magic = tvAlert.ID,
                                })).Entity;
                                await _dbContext.SaveChangesAsync();

                                // TradeJournal item
                                _logger.LogInformation($"Ticker {mtTrade.TickerInMetatrader} not found, created in the database with ID : {trade.ID}", trade);
                            }

                            // Check if we need to execute the order
                            if (tvAlert.OrderType.Equals("BUY") 
                                || (tvAlert.OrderType.Equals("BUYSTOP") && mtTrade.Ask + trade.Offset + trade.Spread >= tvAlert.EntryPrice)
                                || (tvAlert.OrderType.Equals("BUYLIMIT") && mtTrade.Ask + trade.Offset + trade.Spread <= tvAlert.EntryPrice)
                                )
                            {
                                // I used the ASK price, just to make sure the spread is calculated

                                // Caculate SL Price
                                var slPrice = CalculateSLForLong(
                                            mtPrice: mtTrade.Ask,
                                            mtAtr: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, trade.StrategyType),
                                            tvPrice: tvAlert.EntryPrice,
                                            tvSlPrice: tvAlert.StopLoss,
                                            tvAtr: GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, trade.StrategyType),
                                            offset: trade.Offset);

                                // Add to log
                                await _dbContext.Log.AddAsync(new Log()
                                {
                                     AccountID = mtTrade.AccountID,
                                     ClientID = mtTrade.ClientID,
                                     DateCreated = DateTime.UtcNow,
                                     Type = "BACKEND - STOPLOSS CALCULATION",
                                     Message = string.Format($"Type=BUY,StrategypType={trade.StrategyType},MtAsk={mtTrade.Ask},Atr={GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, trade.StrategyType)},TvEntryPrice={tvAlert.EntryPrice},TvSlPrice={tvAlert.StopLoss},TvAtr={GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, trade.StrategyType)},Offset={trade.Offset}"),
                                });

                                // TradeJournal item
                                _logger.LogWarning($"BUY order is send to Metatrader : BUY,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tvAlert.TakeProfit - trade.Offset},sl={slPrice},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mtTrade.Ask;
                                trade.ExecutedSL = slPrice;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "BUY",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset,
                                    StopLoss = slPrice,
                                    Magic = tvAlert.ID,
                                    StrategyType = trade.StrategyType,
                                });
                            }

                            else if (tvAlert.OrderType.Equals("SELL")
                                    || (tvAlert.OrderType.Equals("SELLSTOP") && mtTrade.Bid + trade.Offset - trade.Spread <= tvAlert.EntryPrice)
                                    || (tvAlert.OrderType.Equals("SELLLIMIT") && mtTrade.Bid + trade.Offset - trade.Spread >= tvAlert.EntryPrice)
                                    )
                            {
                                // Caculate SL Price
                                var slPrice = CalculateSLForShort(
                                            mtPrice: mtTrade.Bid,
                                            mtAtr: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, trade.StrategyType),
                                            tvPrice: tvAlert.EntryPrice,
                                            tvSlPrice: tvAlert.StopLoss,
                                            tvAtr: GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, trade.StrategyType),
                                            offset: trade.Offset,
                                            spread: trade.Spread);

                                // Add to log
                                await _dbContext.Log.AddAsync(new Log()
                                {
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    DateCreated = DateTime.UtcNow,
                                    Type = "BACKEND - STOPLOSS CALCULATION",
                                    Message = string.Format($"Type=SELL,StrategypType={trade.StrategyType},MtAsk={mtTrade.Ask},Atr={GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, trade.StrategyType)},TvEntryPrice={tvAlert.EntryPrice},TvSlPrice={tvAlert.StopLoss},TvAtr={GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, trade.StrategyType)},Offset={trade.Offset},Spread={trade.Spread}"),
                                });

                                // TradeJournal item
                                _logger.LogWarning($"SELL order is send to Metatrader : SELL,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tvAlert.TakeProfit - trade.Offset},sl={slPrice},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mtTrade.Bid;
                                trade.ExecutedSL = slPrice;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "SELL",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset,
                                    StopLoss = slPrice,
                                    Magic = tvAlert.ID,
                                    StrategyType = trade.StrategyType,
                                });
                            }

                            // Check if we need to modify the stop loss to break event
                            else if (tvAlert.OrderType.Equals("MODIFYSLTOBE"))
                            {
                                // TradeJournal item
                                _logger.LogWarning($"MODIFY SL order is send to Metatrader : MODIFYSLTOBE,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tvAlert.TakeProfit - trade.Offset},sl={mtTrade.Ask},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mtTrade.Ask;
                                trade.ExecutedSL = mtTrade.Ask;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "MODIFYSLTOBE",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset,
                                    StopLoss = mtTrade.Ask,
                                    Magic = tvAlert.ID,
                                    StrategyType = trade.StrategyType,
                                });
                            }
                            // Check if we need to modify the order
                            else if (tvAlert.OrderType.Equals("MODIFYSL"))
                            {

                                // Get the BUY or SELL order from the database
                                var orgTvAlert = await _dbContext.TradingviewAlert
                                                                     .Include(f => f.Trades)
                                                                     .Where(f => f.AccountID == mtTrade.AccountID
                                                                                 && f.Instrument.Equals(mtTrade.TickerInTradingview)
                                                                                 && f.StrategyType == mtTrade.StrategyType
                                                                                 && f.Magic == tvAlert.Magic
                                                                                 && (f.OrderType.Contains("BUY") || f.OrderType.Contains("SELL"))
                                                                                 )
                                                                     .OrderBy(f => Math.Abs(f.EntryPrice - mtTrade.Ask))
                                                                     .FirstOrDefaultAsync();

                                // Do null reference check
                                if (orgTvAlert != null) 
                                {
                                    // BUY ORDER
                                    if(orgTvAlert.OrderType.Contains("BUY"))
                                    {
                                        // Caculate SL Price
                                        var slPrice = CalculateSLForLong(
                                                    mtPrice: mtTrade.Ask,
                                                    mtAtr: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, trade.StrategyType),
                                                    tvPrice: tvAlert.EntryPrice,
                                                    tvSlPrice: tvAlert.StopLoss,
                                                    tvAtr: GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, trade.StrategyType),
                                                    offset: trade.Offset);


                                        // Add to log
                                        await _dbContext.Log.AddAsync(new Log()
                                        {
                                            AccountID = mtTrade.AccountID,
                                            ClientID = mtTrade.ClientID,
                                            DateCreated = DateTime.UtcNow,
                                            Type = "BACKEND - STOPLOSS CALCULATION",
                                            Message = string.Format($"Type=MODIFYSL,StrategypType={trade.StrategyType},MtAsk={mtTrade.Ask},Atr={GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, trade.StrategyType)},TvEntryPrice={tvAlert.EntryPrice},TvSlPrice={tvAlert.StopLoss},TvAtr={GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, trade.StrategyType)},Offset={trade.Offset},Spread={trade.Spread}"),
                                        });

                                        // TradeJournal item
                                        _logger.LogWarning($"MODIFY SL order is send to Metatrader : MODIFYSL,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tvAlert.TakeProfit - trade.Offset},sl={slPrice},magic={trade.Magic}", trade);

                                        // Update database
                                        trade.Executed = true;
                                        trade.DateExecuted = DateTime.UtcNow;
                                        trade.ExecutedPrice = mtTrade.Ask;
                                        trade.ExecutedSL = slPrice;
                                        trade.ExecutedTP = orgTvAlert.TakeProfit;
                                        await _dbContext.SaveChangesAsync();

                                        // Add repsonse
                                        response.Add(new MetatraderResponse()
                                        {
                                            Action = "MODIFYSL",
                                            AccountId = mtTrade.AccountID,
                                            ClientId = mtTrade.ClientID,
                                            TickerInMetatrader = mtTrade.TickerInMetatrader,
                                            TickerInTradingview = mtTrade.TickerInTradingview,
                                            TakeProfit = orgTvAlert.TakeProfit,
                                            StopLoss = tvAlert.EntryPrice - trade.Offset,
                                            Magic = tvAlert.Magic,
                                            StrategyType = trade.StrategyType,
                                        });
                                    }
                                    else 
                                    {
                                        // Caculate SL Price
                                        var slPrice = CalculateSLForShort(
                                                    mtPrice: mtTrade.Bid,
                                                    mtAtr: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, trade.StrategyType),
                                                    tvPrice: tvAlert.EntryPrice,
                                                    tvSlPrice: tvAlert.StopLoss,
                                                    tvAtr: GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, trade.StrategyType),
                                                    offset: trade.Offset,
                                                    spread: trade.Spread);

                                        // Add to log
                                        await _dbContext.Log.AddAsync(new Log()
                                        {
                                            AccountID = mtTrade.AccountID,
                                            ClientID = mtTrade.ClientID,
                                            DateCreated = DateTime.UtcNow,
                                            Type = "BACKEND - STOPLOSS CALCULATION",
                                            Message = string.Format($"Type=MODIFYSL,StrategypType={trade.StrategyType},MtAsk={mtTrade.Ask},Atr={GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, trade.StrategyType)},TvEntryPrice={tvAlert.EntryPrice},TvSlPrice={tvAlert.StopLoss},TvAtr={GetAtr(tvAlert.Atr5M, tvAlert.Atr15M, tvAlert.Atr1H, tvAlert.AtrD, trade.StrategyType)},Offset={trade.Offset},Spread={trade.Spread}"),
                                        });

                                        // TradeJournal item
                                        _logger.LogWarning($"MODIFY SL order is send to Metatrader : MODIFYSL,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tvAlert.TakeProfit - trade.Offset},sl={slPrice},magic={trade.Magic}", trade);

                                        // Update database
                                        trade.Executed = true;
                                        trade.DateExecuted = DateTime.UtcNow;
                                        trade.ExecutedPrice = mtTrade.Ask;
                                        trade.ExecutedSL = slPrice;
                                        trade.ExecutedTP = orgTvAlert.TakeProfit;
                                        await _dbContext.SaveChangesAsync();

                                        // Add repsonse
                                        response.Add(new MetatraderResponse()
                                        {
                                            Action = "MODIFYSL",
                                            AccountId = mtTrade.AccountID,
                                            ClientId = mtTrade.ClientID,
                                            TickerInMetatrader = mtTrade.TickerInMetatrader,
                                            TickerInTradingview = mtTrade.TickerInTradingview,
                                            TakeProfit = orgTvAlert.TakeProfit,
                                            StopLoss = slPrice,
                                            Magic = tvAlert.ID,
                                            StrategyType = trade.StrategyType,
                                        });
                                    }
                                }
                                else
                                {
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
                            // Check if we need to execute the order
                            else if (tvAlert.OrderType.Equals("CLOSE"))
                            {
                                // TradeJournal item
                                _logger.LogWarning($"CLOSE order is send to Metatrader : CLOSE,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tvAlert.TakeProfit - trade.Offset - trade.Spread},sl={tvAlert.StopLoss - trade.Offset - trade.Spread},magic={trade.Magic}", trade);

                                // Update database
                                trade.Executed = true;
                                trade.DateExecuted = DateTime.UtcNow;
                                trade.ExecutedPrice = mtTrade.Ask;
                                trade.ExecutedSL = tvAlert.StopLoss - trade.Offset;
                                trade.ExecutedTP = tvAlert.TakeProfit - trade.Offset;
                                await _dbContext.SaveChangesAsync();

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "CLOSE",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tvAlert.TakeProfit - trade.Offset,
                                    StopLoss = tvAlert.StopLoss - trade.Offset,
                                    Magic = tvAlert.ID,
                                    StrategyType = trade.StrategyType,
                                });
                            }
                            // Else
                            else
                            {
                                // TradeJournal item
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
            return atr5M;
        }
    }
}
