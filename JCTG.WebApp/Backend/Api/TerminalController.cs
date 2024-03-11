using JCTG.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data.Entity;

namespace JCTG.WebApp.Backend.Api
{
    [ApiController]
    [Route("api")]
    public class TerminalController : ControllerBase
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<TerminalController>();
        private readonly JCTGDbContext _dbContext;

        public TerminalController(JCTGDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("terminalconfig")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TerminalConfig()
        {
            // Security 
            var code = Request.Query["code"];

            if (code != "Ocebxtg1excWosFez5rWMtNp3ZsmIzSFQ0XhqtrfHlMuAzFuQ0OGhA==")
            {
                _logger.Debug("code is not ok");
                return BadRequest();
            }

            // Attempt to parse accountId from the query string as a int
            if (!int.TryParse(Request.Query["accountid"].ToString(), out int accountId))
            {
                _logger.Debug("accountId is not in a valid format.");
                return BadRequest();
            }

            // Init retour
            var retour = new TerminalConfig()
            {
                AccountId = accountId,
                Brokers = _dbContext.Client
                                        .Where(f => f.AccountID == accountId)
                                        .Include(f => f.Pairs)
                                        .Include(f => f.Risks)
                                        .Select(f => new Brokers()
                                        {
                                            ClientId = f.ID,
                                            IsEnable = f.IsEnable,
                                            MetaTraderDirPath = f.MetaTraderDirPath,
                                            Name = f.Name,
                                            Pairs = f.Pairs.OrderBy(f => f.TickerInMetatrader).Select(p => new Pairs()
                                            {
                                                TickerInMetatrader = p.TickerInMetatrader,
                                                TickerInTradingView = p.TickerInTradingView,
                                                Timeframe = p.Timeframe,
                                                CancelStopOrLimitOrderWhenNewSignal = p.CancelStopOrLimitOrderWhenNewSignal,
                                                CloseAllTradesAt = p.CloseAllTradesAt == null ? null : TimeSpan.Parse(p.CloseAllTradesAt),
                                                CorrelatedPairs = new List<string>(),
                                                DoNotOpenTradeXMinutesBeforeClose = p.DoNotOpenTradeXMinutesBeforeClose,
                                                MaxLotSize = Convert.ToInt32(p.MaxLotSize),
                                                MaxSpread = Convert.ToDecimal(p.MaxSpread),
                                                NumberOfHistoricalBarsRequested = p.NumberOfHistoricalBarsRequested,
                                                OrderExecType = p.OrderExecType,
                                                Risk = Convert.ToDecimal(p.Risk),
                                                RiskMinXTimesTheSpread = p.RiskMinXTimesTheSpread,
                                                SLMultiplier = p.SLMultiplier,
                                                SLtoBEafterR = p.SLtoBEafterR,
                                                SpreadEntry = p.SpreadEntry,
                                                SpreadSL = p.SpreadSL,
                                                SpreadTP = p.SpreadTP,
                                                StrategyNr = p.StrategyType
                                            }).ToList(),
                                            Risk = f.Risks.OrderBy(f => f.Procent).Select(r => new Risk()
                                            {
                                                Multiplier = r.Multiplier,
                                                Procent = r.Procent,
                                            }).ToList(),
                                            StartBalance = f.StartBalance,
                                        }).ToList(),
                Debug = true,
                DropLogsInFile = true,
                LoadOrdersFromFile = true,
                MaxRetryCommandSeconds = 10,
                SleepDelay = 250,
            };


            return Ok(retour);
        }
    }
}
