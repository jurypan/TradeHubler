using System.Net;
using Google.Protobuf.WellKnownTypes;
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

                        // Get current price
                        var price = await new FinancialModelingPrepApiClient().GetPriceAsync(mtTrade.TickerInFMP);

                        // Caulcate offset
                        var offset = Math.Round(price - mtTrade.Ask, 4, MidpointRounding.AwayFromZero);

                        // Caulcate mtSpread
                        var spread = Math.Round(Math.Abs(mtTrade.Ask - mtTrade.Bid), 4, MidpointRounding.AwayFromZero);

                        // Get Signal BUY or SELL orders from the database
                        foreach (var signal in await _dbContext.Signal.Where(f =>
                                                            f.AccountID == mtTrade.AccountID
                                                            && f.Trades.Count(g => g.SignalID == f.ID && g.ClientID == mtTrade.ClientID && g.StrategyType == mtTrade.StrategyType) == 0
                                                            && f.Instrument.Equals(mtTrade.TickerInTradingview)
                                                            && f.StrategyType == mtTrade.StrategyType
                                                            && (f.DateExecuted == null || f.DateExecuted >= DateTime.UtcNow.AddMinutes(-10))
                                                            && (f.OrderType.Contains("BUY") || f.OrderType.Contains("SELL"))
                                                            ).ToListAsync())
                        {

                            // Check if we need to execute the order
                            if (signal.OrderType.Equals("BUY") || (signal.OrderType.Equals("BUYSTOP") && price > 0.0 && price > signal.EntryPrice))
                            {
                                // Caculate SL Price
                                var slPrice = CalculateSLForLong(
                                            mtPrice: mtTrade.Ask,
                                            mtATR: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, signal.StrategyType),
                                            mtSpread: spread,
                                            mtTickSize: mtTrade.TickSize,
                                            signalEntryPrice: signal.EntryPrice,
                                            signalSlPrice: signal.StopLoss,
                                            signalATR: GetAtr(signal.Atr5M, signal.Atr15M, signal.Atr1H, signal.AtrD, signal.StrategyType)
                                            );

                                // Caculate TP Price
                                var tpPrice = CalculateTPForLong(
                                            mtPrice: mtTrade.Ask,
                                            mtATR: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, signal.StrategyType),
                                            mtSpread: spread,
                                            mtTickSize: mtTrade.TickSize,
                                            signalEntryPrice: signal.EntryPrice,
                                            signalTpPrice: signal.TakeProfit,
                                            signalATR: GetAtr(signal.Atr5M, signal.Atr15M, signal.Atr1H, signal.AtrD, signal.StrategyType)
                                            );

                                // Add to log
                                await _dbContext.Log.AddAsync(new Log()
                                {
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    DateCreated = DateTime.UtcNow,
                                    Type = "BACKEND - STOPLOSS CALCULATION",
                                    Message = string.Format($"Type=BUY,StrategypType={signal.StrategyType},MtAsk={mtTrade.Ask},MtAtr={GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, signal.StrategyType)},TvEntryPrice={signal.EntryPrice},TvSlPrice={signal.StopLoss},TvAtr={GetAtr(signal.Atr5M, signal.Atr15M, signal.Atr1H, signal.AtrD, signal.StrategyType)},Offset={offset},Tp={tpPrice},Sl={slPrice},Magic={signal.Magic}"),
                                });

                                // TradeJournal item
                                _logger.LogWarning($"BUY order is send to Metatrader : BUY,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tpPrice},sl={slPrice},magic={signal.Magic}", signal);

                                // Update database
                                signal.Executed = true;
                                signal.DateExecuted = DateTime.UtcNow;
                                await _dbContext.Executed.AddAsync(new Executed
                                {
                                    DateCreated = DateTime.UtcNow,
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    StrategyType = signal.StrategyType,
                                    Instrument = mtTrade.TickerInMetatrader,
                                    SignalID = signal.ID,
                                    Offset = offset, // offset in regards of tradingview. If negative amount -> TV price is lower
                                    Spread = spread,
                                    Magic = signal.Magic,
                                    ExecutedPrice = mtTrade.Ask,
                                    ExecutedSL = slPrice,
                                    ExecutedTP = tpPrice,
                                });


                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "BUY",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tpPrice,
                                    StopLoss = slPrice,
                                    Magic = signal.Magic,
                                    StrategyType = signal.StrategyType,
                                });
                            }
                            else if (signal.OrderType.Equals("SELL") || (signal.OrderType.Equals("SELLSTOP") && price > 0.0 && price < signal.EntryPrice))
                            {
                                // Caculate SL Price
                                var slPrice = CalculateSLForShort(
                                            mtPrice: mtTrade.Ask,
                                            mtAtr: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, signal.StrategyType),
                                            mtSpread: spread,
                                            mtTickSize: mtTrade.TickSize,
                                            signalEntryPrice: signal.EntryPrice,
                                            signalSlPrice: signal.StopLoss,
                                            signalATR: GetAtr(signal.Atr5M, signal.Atr15M, signal.Atr1H, signal.AtrD, signal.StrategyType)
                                           );

                                // Caculate TP Price
                                var tpPrice = CalculateTPForShort(
                                            mtPrice: mtTrade.Ask,
                                            mtAtr: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, signal.StrategyType),
                                            mtSpread: spread,
                                            mtTickSize: mtTrade.TickSize,
                                            signalEntryPrice: signal.EntryPrice,
                                            signalTpPrice: signal.TakeProfit,
                                            signalATR: GetAtr(signal.Atr5M, signal.Atr15M, signal.Atr1H, signal.AtrD, signal.StrategyType)
                                           );

                                // Add to log
                                await _dbContext.Log.AddAsync(new Log()
                                {
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    DateCreated = DateTime.UtcNow,
                                    Type = "BACKEND - STOPLOSS CALCULATION",
                                    Message = string.Format($"Type=SELL,StrategypType={signal.StrategyType},MtAsk={mtTrade.Ask},MtAtr={GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, signal.StrategyType)},TvEntryPrice={signal.EntryPrice},TvSlPrice={signal.StopLoss},TvAtr={GetAtr(signal.Atr5M, signal.Atr15M, signal.Atr1H, signal.AtrD, signal.StrategyType)},Offset={offset},Tp={tpPrice},Sl={slPrice},Magic={signal.Magic}"),
                                });

                                // TradeJournal item
                                _logger.LogWarning($"SELL order is send to Metatrader : SELL,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tpPrice},sl={slPrice},magic={signal.Magic}", signal);

                                // Update database
                                signal.Executed = true;
                                signal.DateExecuted = DateTime.UtcNow;
                                await _dbContext.Executed.AddAsync(new Executed
                                {
                                    DateCreated = DateTime.UtcNow,
                                    AccountID = mtTrade.AccountID,
                                    ClientID = mtTrade.ClientID,
                                    StrategyType = signal.StrategyType,
                                    Instrument = mtTrade.TickerInMetatrader,
                                    SignalID = signal.ID,
                                    Offset = offset, // offset in regards of tradingview. If negative amount -> TV price is lower
                                    Spread = spread,
                                    Magic = signal.Magic,
                                    ExecutedPrice = mtTrade.Ask,
                                    ExecutedSL = slPrice,
                                    ExecutedTP = tpPrice,
                                });

                                // Add repsonse
                                response.Add(new MetatraderResponse()
                                {
                                    Action = "SELL",
                                    AccountId = mtTrade.AccountID,
                                    ClientId = mtTrade.ClientID,
                                    TickerInMetatrader = mtTrade.TickerInMetatrader,
                                    TickerInTradingview = mtTrade.TickerInTradingview,
                                    TakeProfit = tpPrice,
                                    StopLoss = slPrice,
                                    Magic = signal.Magic,
                                    StrategyType = signal.StrategyType,
                                });
                            }
                        }

                        // Get Signal CANCEL, MODIFY SL orders from the database
                        foreach (var signal in await _dbContext.Signal.Where(f =>
                                                            f.AccountID == mtTrade.AccountID
                                                            && f.Trades.Count(g => g.SignalID == f.ID && g.ClientID == mtTrade.ClientID) == 0
                                                            && f.Instrument.Equals(mtTrade.TickerInTradingview)
                                                            && f.StrategyType == mtTrade.StrategyType
                                                            && (f.DateExecuted == null || f.DateExecuted >= DateTime.UtcNow.AddMinutes(-10))
                                                            && (f.OrderType.Equals("MODIFYSLTOBE") || f.OrderType.Equals("MODIFYSL") || f.OrderType.Equals("CLOSE"))
                                                            ).ToListAsync())
                        {

                            // Get the trade from the tradejournal db
                            var tradeJournal = await _dbContext.TradeJournal.FirstOrDefaultAsync(f =>
                                                               f.ClientID == mtTrade.ClientID
                                                           && f.AccountID == mtTrade.AccountID
                                                           && f.Instrument == mtTrade.TickerInMetatrader
                                                           && f.StrategyType == mtTrade.StrategyType
                                                           && f.Magic == signal.Magic);

                            // Do null reference check
                            if (tradeJournal != null && (tradeJournal.Type == "BUY" || tradeJournal.Type == "SELL"))
                            {
                                // Check if we need to modify the stop loss to break event
                                if (signal.OrderType.Equals("MODIFYSLTOBE"))
                                {
                                    // TradeJournal item
                                    _logger.LogWarning($"MODIFY SL TO BE order is send to Metatrader : MODIFYSLTOBE,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tradeJournal.TP},sl={tradeJournal.OpenPrice},magic={signal.Magic}");

                                    // Update database
                                    signal.Executed = true;
                                    signal.DateExecuted = DateTime.UtcNow;
                                    await _dbContext.Executed.AddAsync(new Executed
                                    {
                                        DateCreated = DateTime.UtcNow,
                                        AccountID = mtTrade.AccountID,
                                        ClientID = mtTrade.ClientID,
                                        StrategyType = signal.StrategyType,
                                        Instrument = mtTrade.TickerInMetatrader,
                                        SignalID = signal.ID,
                                        Offset = offset, // offset in regards of tradingview. If negative amount -> TV price is lower
                                        Spread = spread,
                                        Magic = signal.Magic,
                                        ExecutedPrice = mtTrade.Ask,
                                        ExecutedSL = tradeJournal.OpenPrice,
                                        ExecutedTP = tradeJournal.TP,
                                    });

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
                                        Magic = signal.Magic,
                                        StrategyType = signal.StrategyType,
                                        TicketId = tradeJournal.TicketId
                                    }); ;
                                }

                                // Check if we need to modify the stop loss to break event
                                else if (signal.OrderType.Equals("MODIFYSL"))
                                {
                                    // Caculate SL Price
                                    var slPrice = tradeJournal.Type == "BUY" ? CalculateSLForLong(
                                                mtPrice: mtTrade.Ask,
                                                mtATR: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, signal.StrategyType),
                                                mtSpread: spread,
                                                mtTickSize: mtTrade.TickSize,
                                                signalEntryPrice: signal.EntryPrice,
                                                signalSlPrice: signal.StopLoss,
                                                signalATR: GetAtr(signal.Atr5M, signal.Atr15M, signal.Atr1H, signal.AtrD, signal.StrategyType)
                                                ) : CalculateSLForShort(
                                                mtPrice: mtTrade.Ask,
                                                mtAtr: GetAtr(mtTrade.Atr5M, mtTrade.Atr15M, mtTrade.Atr1H, mtTrade.AtrD, signal.StrategyType),
                                                mtSpread: spread,
                                                mtTickSize: mtTrade.TickSize,
                                                signalEntryPrice: signal.EntryPrice,
                                                signalSlPrice: signal.StopLoss,
                                                signalATR: GetAtr(signal.Atr5M, signal.Atr15M, signal.Atr1H, signal.AtrD, signal.StrategyType)
                                                );

                                    // TradeJournal item
                                    _logger.LogWarning($"MODIFY SL order is send to Metatrader : MODIFYSL,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tradeJournal.TP},sl={slPrice},magic={signal.Magic}");

                                    // Update database
                                    signal.Executed = true;
                                    signal.DateExecuted = DateTime.UtcNow;
                                    await _dbContext.Executed.AddAsync(new Executed
                                    {
                                        DateCreated = DateTime.UtcNow,
                                        AccountID = mtTrade.AccountID,
                                        ClientID = mtTrade.ClientID,
                                        StrategyType = signal.StrategyType,
                                        Instrument = mtTrade.TickerInMetatrader,
                                        SignalID = signal.ID,
                                        Offset = offset, // offset in regards of tradingview. If negative amount -> TV price is lower
                                        Spread = spread,
                                        Magic = signal.Magic,
                                        ExecutedPrice = mtTrade.Ask,
                                        ExecutedSL = slPrice,
                                        ExecutedTP = tradeJournal.TP,
                                    });

                                    // Add repsonse
                                    response.Add(new MetatraderResponse()
                                    {
                                        Action = "MODIFYSL",
                                        AccountId = mtTrade.AccountID,
                                        ClientId = mtTrade.ClientID,
                                        TickerInMetatrader = mtTrade.TickerInMetatrader,
                                        TickerInTradingview = mtTrade.TickerInTradingview,
                                        TakeProfit = tradeJournal.TP,
                                        StopLoss = slPrice,
                                        Magic = signal.Magic,
                                        StrategyType = signal.StrategyType,
                                        TicketId = tradeJournal.TicketId
                                    }); ;
                                }


                                // Check if we need to execute the order
                                else if (signal.OrderType.Equals("CLOSE"))
                                {
                                    // TradeJournal item
                                    _logger.LogWarning($"CLOSE order is send to Metatrader : CLOSE,instrument={mtTrade.TickerInMetatrader},mtAskPrice={mtTrade.Ask},tp={tradeJournal.TP},sl={tradeJournal.SL},magic={signal.Magic}");

                                    // Update database
                                    signal.Executed = true;
                                    signal.DateExecuted = DateTime.UtcNow;
                                    await _dbContext.Executed.AddAsync(new Executed
                                    {
                                        DateCreated = DateTime.UtcNow,
                                        AccountID = mtTrade.AccountID,
                                        ClientID = mtTrade.ClientID,
                                        StrategyType = signal.StrategyType,
                                        Instrument = mtTrade.TickerInMetatrader,
                                        SignalID = signal.ID,
                                        Offset = offset, // offset in regards of tradingview. If negative amount -> TV price is lower
                                        Spread = spread,
                                        Magic = signal.Magic,
                                        ExecutedPrice = mtTrade.Ask,
                                        ExecutedSL = tradeJournal.SL,
                                        ExecutedTP = tradeJournal.TP,
                                    });

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
                                        Magic = signal.Magic,
                                        StrategyType = signal.StrategyType,
                                        TicketId = tradeJournal.TicketId
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
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="mtSpread">Spread value</param>
        /// <param name="mtTickSize">Tick size</param>
        /// <param name="signalEntryPrice">Signal ENTRY price</param>
        /// <param name="signalSlPrice">Signal SL price</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Stop loss price</returns>
        public static double CalculateSLForLong(double mtPrice, double mtATR, double mtSpread, double mtTickSize, double signalEntryPrice, double signalSlPrice, double signalATR)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            var atrMultiplier = mtATR > 0 && signalATR > 0 ? mtATR / signalATR : 1.0;

            // Calculate SL price using MetaTrader price minus risk to take
            var slPrice = mtPrice - ((signalEntryPrice - signalSlPrice) * atrMultiplier);

            // Calculate the number of ticks
            var numberOfTicks = Math.Floor(slPrice / mtTickSize);

            // Multiply back to get the rounded value
            slPrice = numberOfTicks * mtTickSize;

            // Check if SL is not higher then entry price
            if (slPrice > mtPrice)
                slPrice = mtPrice;

            // Return SL Price minus spread
            return slPrice - mtSpread;
        }


        /// <summary>
        /// Calculate the stop loss price for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK price</param>
        /// <param name="mtAtr">MetaTrader Atr5M</param>
        /// <param name="mtSpread">The spread value</param>
        /// <param name="signalEntryPrice">Signal ENTRY price</param>
        /// <param name="signalSlPrice">Signal SL price</param>
        /// <param name="signalATR">Signal Atr5M</param>
        /// <returns>Stop loss price</returns>
        public static double CalculateSLForShort(double mtPrice, double mtAtr, double mtSpread, double mtTickSize, double signalEntryPrice, double signalSlPrice, double signalATR)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            var atrMultiplier = mtAtr > 0 && signalATR > 0 ? mtAtr / signalATR : 1.0;

            // Calculate SL price using MetaTrader price minus risk to take
            var slPrice = mtPrice + ((signalSlPrice - signalEntryPrice) * atrMultiplier);

            // Calculate the number of ticks
            var numberOfTicks = Math.Ceiling(slPrice / mtTickSize);

            // Multiply back to get the rounded value
            slPrice = numberOfTicks * mtTickSize;

            // Check if SL is not lower then entry price
            if (slPrice < mtPrice)
                slPrice = mtPrice;

            // Return SL Price plus spread
            return slPrice + mtSpread;
        }

        /// <summary>
        /// Calculate the take profit price for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK price</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="mtSpread">Spread value</param>
        /// <param name="mtTickSize">Tick size</param>
        /// <param name="signalEntryPrice">Signal ENTRY price</param>
        /// <param name="signalTpPrice">Signal TP price</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Take profit price</returns>
        public static double CalculateTPForLong(double mtPrice, double mtATR, double mtSpread, double mtTickSize, double signalEntryPrice, double signalTpPrice, double signalATR)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            var atrMultiplier = mtATR > 0 && signalATR > 0 ? mtATR / signalATR : 1.0;

            // Calculate TP price using MetaTrader price minus risk to take
            var tpPrice = mtPrice + ((signalTpPrice - signalEntryPrice) * atrMultiplier);

            // Calculate the number of ticks
            var numberOfTicks = Math.Floor(tpPrice / mtTickSize);

            // Multiply back to get the rounded value
            tpPrice = numberOfTicks * mtTickSize;

            // Return SL Price minus spread
            return tpPrice - mtSpread;
        }

        /// <summary>
        /// Calculate the take profit price for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK price</param>
        /// <param name="mtAtr">MetaTrader ATR</param>
        /// <param name="mtSpread">The spread value</param>
        /// <param name="signalEntryPrice">Signal ENTRY price</param>
        /// <param name="signalTpPrice">Signal TP price</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Take profit price</returns>
        public static double CalculateTPForShort(double mtPrice, double mtAtr, double mtSpread, double mtTickSize, double signalEntryPrice, double signalTpPrice, double signalATR)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            var atrMultiplier = mtAtr > 0 && signalATR > 0 ? mtAtr / signalATR : 1.0;

            // Calculate TP price using MetaTrader price minus risk to take
            var tpPrice = mtPrice - ((signalEntryPrice - signalTpPrice) * atrMultiplier);

            // Calculate the number of ticks
            var numberOfTicks = Math.Ceiling(tpPrice / mtTickSize);

            // Multiply back to get the rounded value
            tpPrice = numberOfTicks * mtTickSize;

            // Return SL Price plus spread
            return tpPrice + mtSpread;
        }

        private double GetAtr(double atr5M, double atr15M, double atr1H, double atrD, StrategyType type)
        {
            if (type == StrategyType.Strategy1)
                return atr1H; // 4H
            else if (type == StrategyType.Strategy2)
                return atr1H;
            else if (type == StrategyType.Strategy3)
                return atr5M;
            else if (type == StrategyType.Strategy4)
                return atr5M;
            else if (type == StrategyType.Strategy5)
                return atr15M;
            return atr15M;
        }
    }
}
