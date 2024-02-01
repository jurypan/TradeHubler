using System.ComponentModel.DataAnnotations;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace JCTG
{
    public class Signal
    {
        public Signal()
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public StrategyType StrategyType { get; set; }
        public Account Account { get; set; }
        public int AccountID { get; set; }
        public long Magic { get; set; }
        public string OrderType { get; set; }
        public string Instrument { get; set; }
        public decimal? CurrentPrice { get; set; }

        // Optional BUY or SELL
        public decimal? EntryPrice { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }


        // Optional BUYSTOP or SELLSTOP
        public string? EntryExpression { get; set; }
        public decimal? Risk { get; set; }
        public decimal? RiskRewardRatio { get; set; }


        public static Signal Parse(string input)
        {
            var parts = input.Split(',');
            if (parts.Length < 3)
            {
                throw new ArgumentException("Insufficient data. AccountID, Order Type, and Symbol are mandatory.");
            }

            var tradeInfo = new Signal
            {
                AccountID = int.Parse(parts[0]),
                OrderType = parts[1].ToUpper(),
                Instrument = parts[2].ToUpper()
            };

            var optionalParams = new Dictionary<string, Action<string>>
            {
                { "entryprice", value => tradeInfo.EntryPrice = decimal.Parse(value) },
                { "currentprice", value => tradeInfo.CurrentPrice = decimal.Parse(value) },
                { "sl", value => tradeInfo.StopLoss = decimal.Parse(value) },
                { "tp", value => tradeInfo.TakeProfit = decimal.Parse(value) },
                { "magic", value => tradeInfo.Magic = long.Parse(value) },
                { "strategytype", value => tradeInfo.StrategyType = Enum.Parse<StrategyType>(value) },
                { "entryexpr", value => tradeInfo.EntryExpression = value },
                { "risk", value => tradeInfo.Risk = decimal.Parse(value) },
                { "rr", value => tradeInfo.RiskRewardRatio = decimal.Parse(value)  },
            };

            foreach (var part in parts[3..])
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2 && optionalParams.TryGetValue(keyValue[0], out var setter))
                {
                    setter(keyValue[1]);
                }
            }

            return tradeInfo;
        }
    }
}
