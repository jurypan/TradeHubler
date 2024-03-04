using System.ComponentModel.DataAnnotations;
using JCTG.Models;

namespace JCTG.Entity
{
    public class Signal
    {
        public Signal()
        {
            DateCreated = DateTime.UtcNow;
            DateLastUpdated = DateTime.UtcNow;
            Logs = new List<Log>();
            StrategyType = StrategyType.None;
            TradingviewStateType = TradingviewStateType.Init;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastUpdated { get; set; }
        public StrategyType StrategyType { get; set; }
        public Account Account { get; set; }
        public int AccountID { get; set; }
        public long Magic { get; set; }
        public string OrderType { get; set; }
        public string Instrument { get; set; }


        // Optional BUY or SELL
        public double? CurrentPrice { get; set; }
        public double? EntryPrice { get; set; }
        public double? StopLoss { get; set; }
        public double? TakeProfit { get; set; }


        // Optional BUYSTOP or SELLSTOP
        public string? EntryExpression { get; set; }
        public double? Risk { get; set; }
        public double? RiskRewardRatio { get; set; }


        // Tradingview State
        public TradingviewStateType TradingviewStateType { get; set; }


        // Links
        public List<Order> Orders { get; set; }
        public List<Log> Logs { get; set; }
        public List<TradingviewAlert> TradingviewAlerts { get; set; }



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
                { "entryprice", value => signal.EntryPrice = double.Parse(value) },
                { "currentprice", value => signal.CurrentPrice = double.Parse(value) },
                { "sl", value => signal.StopLoss = double.Parse(value) },
                { "tp", value => signal.TakeProfit = double.Parse(value) },
                { "magic", value => signal.Magic = long.Parse(value) },
                { "strategytype", value => signal.StrategyType = Enum.Parse<StrategyType>(value) },
                { "entryexpr", value => signal.EntryExpression = value },
                { "risk", value => signal.Risk = double.Parse(value) },
                { "rr", value => signal.RiskRewardRatio = double.Parse(value)  },
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
                signal.TradingviewStateType = TradingviewStateType.Entry;
            }

            if (signal.StrategyType == StrategyType.None)
            {
                throw new ArgumentException("Insufficient data. StrategyType is mandatory.");
            }

            return signal;
        }
    }

    public enum TradingviewStateType
    {
        Init = 0,
        TpHit = 1,
        SlHit = 2,
        BeHit = 3,
        Entry = 10,
        Cancel = 20,
    }
}
