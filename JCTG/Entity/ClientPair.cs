using JCTG.Models;
using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class ClientPair
    {
        public ClientPair()
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public Client? Client { get; set; }
        public long ClientID { get; set; }
        public string TickerInTradingView { get; set; }
        public string TickerInMetatrader { get; set; }
        public string Timeframe { get; set; }
        public StrategyType StrategyType { get; set; }
        public double Risk { get; set; }
        public double SLtoBEafterR { get; set; }
        public double MaxSpread { get; set; }
        public double SLMultiplier { get; set; }
        public double MaxLotSize { get; set; }
        public OrderExecType OrderExecType { get; set; }
        public bool CancelStopOrLimitOrderWhenNewSignal { get; set; }
        public int NumberOfHistoricalBarsRequested { get; set; }
        public string? CloseAllTradesAt { get; set; }
        public int? DoNotOpenTradeXMinutesBeforeClose { get; set; }
        public SpreadExecType SpreadEntry { get; set; }
        public SpreadExecType SpreadSL { get; set; }
        public SpreadExecType SpreadTP { get; set; }
        public int RiskMinXTimesTheSpread { get; set; }

    }
}
