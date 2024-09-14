using JCTG.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [Required]
        [StringLength(128, ErrorMessage = "Name is too long.")]
        public string TickerInTradingView { get; set; }
        [Required]
        [StringLength(128, ErrorMessage = "Name is too long.")]
        public string TickerInMetatrader { get; set; }
        [Required]
        [StringLength(16, ErrorMessage = "Name is too long.")]
        public string Timeframe { get; set; }
        public long StrategyID { get; set; }
        public double RiskLong { get; set; }
        [NotMapped]
        public string RiskLongAsString
        {
            get => RiskLong.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    RiskLong = newValue;
                }
            }
        }
        public double RiskShort { get; set; }
        [NotMapped]
        public string RiskShortAsString
        {
            get => RiskShort.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    RiskShort = newValue;
                }
            }
        }
        public double SLtoBEafterR { get; set; }
        [NotMapped]
        public string SLtoBEafterRAsString
        {
            get => SLtoBEafterR.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    SLtoBEafterR = newValue;
                }
            }
        }
        public double MaxSpread { get; set; }
        public int AdaptPassiveOrdersBeforeEntryInSeconds { get; set; }
        [NotMapped]
        public string AdaptPassiveOrdersBeforeEntryInSecondsAsString
        {
            get => AdaptPassiveOrdersBeforeEntryInSeconds.ToString();
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    AdaptPassiveOrdersBeforeEntryInSeconds = newValue;
                }
            }
        }
        public int AdaptSlOrTpAfterEntryInSeconds { get; set; }
        [NotMapped]
        public string AdaptSlOrTpAfterEntryInSecondsAsString
        {
            get => AdaptSlOrTpAfterEntryInSeconds.ToString();
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    AdaptSlOrTpAfterEntryInSeconds = newValue;
                }
            }
        }

        [NotMapped]
        public string MaxSpreadAsString
        {
            get => MaxSpread.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    MaxSpread = newValue;
                }
            }
        }
        public double SLMultiplier { get; set; }
        [NotMapped]
        public string SLMultiplierAsString
        {
            get => SLMultiplier.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    SLMultiplier = newValue;
                }
            }
        }
        public double MaxLotSize { get; set; }
        [NotMapped]
        public string MaxLotSizeAsString
        {
            get => MaxLotSize.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    MaxLotSize = newValue;
                }
            }
        }
        public OrderExecType OrderExecType { get; set; }
        public bool CancelStopOrLimitOrderWhenNewSignal { get; set; }
        public bool ExecuteMarketOrderOnEntryIfNoPendingOrders { get; set; }
        public int NumberOfHistoricalBarsRequested { get; set; }
        [NotMapped]
        public string NumberOfHistoricalBarsRequestedAsString
        {
            get => NumberOfHistoricalBarsRequested.ToString();
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    NumberOfHistoricalBarsRequested = newValue;
                }
            }
        }
        public string? CloseAllTradesAt { get; set; }
        public int? CloseTradeWithinXBars { get; set; }
        [NotMapped]
        public string? CloseTradeWithinXBarsAsString
        {
            get => CloseTradeWithinXBars.HasValue ? CloseTradeWithinXBars.Value.ToString() : null;
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    CloseTradeWithinXBars = newValue;
                }
            }
        }
        public int? DoNotOpenTradeXMinutesBeforeClose { get; set; }
        [NotMapped]
        public string? DoNotOpenTradeXMinutesBeforeCloseAsString
        {
            get => DoNotOpenTradeXMinutesBeforeClose.HasValue ? DoNotOpenTradeXMinutesBeforeClose.Value.ToString() : null;
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    DoNotOpenTradeXMinutesBeforeClose = newValue;
                }
            }
        }
        public int RiskMinXTimesTheSpread { get; set; }
        [NotMapped]
        public string RiskMinXTimesTheSpreadAsString
        {
            get => RiskMinXTimesTheSpread.ToString();
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    RiskMinXTimesTheSpread = newValue;
                }
            }
        }
    }
}
