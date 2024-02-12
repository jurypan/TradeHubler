using System.ComponentModel.DataAnnotations;
using JCTG.Models;

namespace JCTG.Entity
{
    public class Signal
    {
        public Signal()
        {
            DateCreated = DateTime.UtcNow;
            Logs = new List<Log>();
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


        // Optional BUY or SELL
        public double? CurrentPrice { get; set; }
        public double? EntryPrice { get; set; }
        public double? StopLoss { get; set; }
        public double? TakeProfit { get; set; }


        // Optional BUYSTOP or SELLSTOP
        public string? EntryExpression { get; set; }
        public double? Risk { get; set; }
        public double? RiskRewardRatio { get; set; }



        public List<TradeJournal> TradeJournals { get; set; }
        public List<Log> Logs { get; set; }



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
                { "entryprice", value => tradeInfo.EntryPrice = double.Parse(value) },
                { "currentprice", value => tradeInfo.CurrentPrice = double.Parse(value) },
                { "sl", value => tradeInfo.StopLoss = double.Parse(value) },
                { "tp", value => tradeInfo.TakeProfit = double.Parse(value) },
                { "magic", value => tradeInfo.Magic = long.Parse(value) },
                { "strategytype", value => tradeInfo.StrategyType = Enum.Parse<StrategyType>(value) },
                { "entryexpr", value => tradeInfo.EntryExpression = value },
                { "risk", value => tradeInfo.Risk = double.Parse(value) },
                { "rr", value => tradeInfo.RiskRewardRatio = double.Parse(value)  },
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
