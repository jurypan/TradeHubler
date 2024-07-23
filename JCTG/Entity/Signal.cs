using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JCTG.Models;

namespace JCTG.Entity
{
    public class Signal
    {
        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime DateLastUpdated { get; set; } = DateTime.UtcNow;
        public Strategy? Strategy { get; set; }
        [Required]
        public long StrategyID { get; set; }
        [NotMapped]
        public string StrategyIDAsString
        {
            get => StrategyID.ToString();
            set
            {
                if (long.TryParse(value, out long newValue))
                {
                    StrategyID = newValue;
                }
            }
        }
        public Account Account { get; set; }
        public int AccountID { get; set; }
        [Required]
        public long Magic { get; set; }
        [NotMapped]
        public string MagicAsString
        {
            get => Magic.ToString();
            set
            {
                if (long.TryParse(value, out long newValue))
                {
                    Magic = newValue;
                }
            }
        }
        [Required]
        [StringLength(16, ErrorMessage = "Name is too long.")]
        public string OrderType { get; set; }
        [Required]
        [StringLength(32, ErrorMessage = "Name is too long.")]
        public string Instrument { get; set; }


        // Optional BUY or SELL
        public double? EntryPrice { get; set; }
        [NotMapped]
        public string? EntryPriceAsString
        {
            get => EntryPrice.HasValue ? EntryPrice.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    EntryPrice = newValue;
                }
            }
        }
        public double? StopLoss { get; set; }
        [NotMapped]
        public string? StopLossAsString
        {
            get => StopLoss.HasValue ? StopLoss.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    StopLoss = newValue;
                }
            }
        }
        public double? TakeProfit { get; set; }
        [NotMapped]
        public string? TakeProfitAsString
        {
            get => TakeProfit.HasValue ? TakeProfit.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    TakeProfit = newValue;
                }
            }
        }


        // Optional BUYSTOP or SELLSTOP
        public string? EntryExpression { get; set; }
        public double Risk { get; set; }
        [NotMapped]
        public string RiskAsString
        {
            get => Risk.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Risk = newValue;
                }
            }
        }
        public double RiskRewardRatio { get; set; }
        [NotMapped]
        public string RiskRewardRatioAsString
        {
            get => RiskRewardRatio.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    RiskRewardRatio = newValue;
                }
            }
        }


        // Tradingview State
        public SignalStateType SignalStateType { get; set; } = SignalStateType.Init;
        public double? ExitRiskRewardRatio { get; set; }
        [NotMapped]
        public string? ExitRiskRewardRatioAsString
        {
            get => ExitRiskRewardRatio.HasValue ? ExitRiskRewardRatio.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    ExitRiskRewardRatio = newValue;
                }
            }
        }


        // Links
        public List<Order> Orders { get; set; } = [];
        public List<Log> Logs { get; set; } = [];
        public List<TradingviewAlert> TradingviewAlerts { get; set; } = [];
        public List<MarketAbstention> MarketAbstentions { get; set; } = [];



        public static Signal Parse(string input)
        {
            var parts = input.Split(',');
            if (parts.Length < 3)
            {
                throw new ArgumentException("Insufficient data. AccountID, Order Type, and Symbol are mandatory.");
            }

            var signal = new Signal
            {
                AccountID = int.Parse(parts[0]),
                OrderType = parts[1].ToUpper(),
                Instrument = parts[2].ToUpper()
            };

            var optionalParams = new Dictionary<string, Action<string>>
            {
                { "entryprice", value => signal.EntryPriceAsString = value },
                { "sl", value => signal.StopLossAsString = value },
                { "tp", value => signal.TakeProfitAsString = value },
                { "magic", value => signal.MagicAsString = value },
                { "strategytype", value => signal.StrategyIDAsString = value },
                { "strategy", value => signal.StrategyIDAsString = value },
                { "entryexpr", value => signal.EntryExpression = value },
                { "risk", value => signal.RiskAsString = value },
                { "rr", value => signal.RiskRewardRatioAsString = value  },
                { "exitrr", value => signal.ExitRiskRewardRatioAsString = value  },
            };

            foreach (var part in parts[3..])
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2 && optionalParams.TryGetValue(keyValue[0], out var setter))
                {
                    setter(keyValue[1]);
                }
            }

            if (signal.OrderType.Equals("buy", StringComparison.CurrentCultureIgnoreCase) || signal.OrderType.Equals("sell", StringComparison.CurrentCultureIgnoreCase))
            {
                signal.SignalStateType = SignalStateType.Entry;
            }


            return signal;
        }
    }

    public enum SignalStateType
    {
        Init = 0,
        TpHit = 1,
        SlHit = 2,
        BeHit = 3,
        Entry = 10,
        CancelOrder = 20,
        CloseAll = 21
    }
}
