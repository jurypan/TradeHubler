using System;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JustCallTheGuy.STR2
{
    public class Metatrader
    {
        private readonly ILogger _logger;
        private readonly JCTGDbContext _dbContext;

        public Metatrader(ILoggerFactory loggerFactory, JCTGDbContext dbContext)
        {
            _logger = loggerFactory.CreateLogger<Metatrader>();
            _dbContext = dbContext;
        }

        [Function("Strategy2_Metatrader")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            // Read body from request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Log item
            _logger.LogInformation($"Strategy2_Metatrader || Request body : {requestBody}");

            // Make response object
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            try
            {
                // Parse object
                var mt = Str2MetatraderRequest.Parse(requestBody);

                // Log item
                _logger.LogDebug($"Strategy2_Metatrader || Parsed Metatrader object : AccountID={mt.AccountID}, Instrument={mt.Instrument}, ClientID={mt.ClientID}, Price={mt.Price}, TradingviewTicker={mt.TradingviewTicker},",requestBody);

                // Get TradingviewAlert from the database
                var tvAlert = await _dbContext.TradingviewAlert.Include(f => f.Trades).FirstOrDefaultAsync(f => f.AccountID == mt.AccountID && f.Instrument.Equals(mt.TradingviewTicker) && (f.Trades.Count == 0 || f.Trades.Any(g => g.ClientID == mt.ClientID && g.Executed == false)) && f.StrategyType == StrategyType.Strategy2);

                // If ther eis not tradingview tvAlert in the db -> return OK
                if (tvAlert == null)
                {
                    // Log item
                    _logger.LogInformation("Strategy2_Metatrader || Tradingview Alert not found");

                    // Return repsonse
                    response.WriteString("NONE,tp=0.0,sl=0.0,comment=''");
                    return response;
                }

                // Get Trade Alert from database based on AccountID / ClientID / Instrument that is not executed
                var trade = tvAlert.Trades.FirstOrDefault(f => f.Instrument.Equals(mt.Instrument) && f.Executed == false);

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
                         Instrument = mt.Instrument,
                         TradingviewAlertID = tvAlert.ID,
                         Executed = false,
                         Offset = mt.Price - tvAlert.Price,
                         Comment = tvAlert.Comment,
                    })).Entity;
                    await _dbContext.SaveChangesAsync();

                    // Log item
                    _logger.LogInformation($"Strategy2_Metatrader || Metatrader trade not found, created in the database with ID : {trade.ID}", trade);
                }

                // Check if we need to execute the order
                if (mt.Price >= tvAlert.Price - trade.Offset)
                {
                    // Log item
                    _logger.LogWarning($"Strategy2_Metatrader || BUY order is send to Metatrader : BUY,tp={tvAlert.TakeProfit - trade.Offset},sl={tvAlert.StopLoss - trade.Offset}", trade);

                    // Make response
                    await response.WriteStringAsync($"BUY,tp={tvAlert.TakeProfit + trade.Offset},sl={tvAlert.StopLoss + trade.Offset},comment='{tvAlert.Comment}'");

                    // Update database
                    trade.Executed = true;
                    trade.DateExecuted = DateTime.UtcNow;
                    trade.ExecutedPrice = mt.Price;
                    trade.ExecutedSL = tvAlert.StopLoss + trade.Offset;
                    trade.ExecutedTP = tvAlert.TakeProfit + trade.Offset;
                    await _dbContext.SaveChangesAsync();

                    // Return response
                    return response;
                }
                else
                {
                    // Log item
                    _logger.LogInformation("Strategy2_Metatrader || NONE order is send to Metatrader : NONE,tp=0.0,sl=0.0,comment=''");

                    // Make response
                    await response.WriteStringAsync("NONE,tp=0.0,sl=0.0,comment=''");

                    // Return response
                    return response;
                }

            }
            catch (Exception ex)
            {
                // Log item
                _logger.LogError($"Strategy2_Tradingview ||\nMessage: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);

                // Make response
                await response.WriteStringAsync("NONE,tp=0.0,sl=0.0,comment=''");

                // Return response
                return response;
            }
        }
    }
}
