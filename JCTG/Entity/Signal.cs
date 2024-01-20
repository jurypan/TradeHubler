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
        public string OrderType { get; set; }
        public string Instrument { get; set; }
        public double CurrentPrice { get; set; }
        public double EntryPrice { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public long Magic { get; set; }

        public static Signal Parse2(string input)
        {
            var parts = input.Split(',');
            if (parts.Length < 3)
            {
                throw new ArgumentException("Insufficient data. ID, Action, and Symbol are mandatory.");
            }

            var tradeInfo = new Signal
            {
                ID = long.Parse(parts[0]),
                OrderType = parts[1],
                Instrument = parts[2]
            };

            var optionalParams = new Dictionary<string, Action<string>>
            {
                { "entryprice", value => tradeInfo.EntryPrice = double.Parse(value) },
                { "currentprice", value => tradeInfo.CurrentPrice = double.Parse(value) },
                { "sl", value => tradeInfo.StopLoss = double.Parse(value) },
                { "tp", value => tradeInfo.TakeProfit = double.Parse(value) },
                { "magic", value => tradeInfo.Magic = long.Parse(value) },
                { "strategytype", value => tradeInfo.StrategyType = Enum.Parse<StrategyType>(value) }
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

        public static Signal Parse(string input)
        {
            var parts = input.Split(',');

            if (parts.Length < 9)
            {
                throw new ArgumentException("Input string does not have the correct format.");
            }

            var order = new Signal();

            // Parsing ID
            if (!int.TryParse(parts[0], out int id))
            {
                throw new ArgumentException("Invalid ID format.");
            }
            order.AccountID = id;

            // Parsing OrderType
            order.OrderType = parts[1].ToUpper();

            // Parsing TickerInMetatrader
            order.Instrument = parts[2].ToUpper();

            // Parsing EntryPrice
            var entryPriceParts = parts[3].Split('=');
            if (entryPriceParts.Length != 2 || !double.TryParse(entryPriceParts[1], out double entryPrice))
            {
                throw new ArgumentException("Invalid EntryPrice format.");
            }
            order.EntryPrice = entryPrice;

            // Parsing CurrentPrice
            var currentPriceParts = parts[4].Split('=');
            if (currentPriceParts.Length != 2 || !double.TryParse(currentPriceParts[1], out double currentPrice))
            {
                throw new ArgumentException("Invalid CurrentPrice format.");
            }
            order.CurrentPrice = currentPrice;

            // Parsing Stop Loss
            var slParts = parts[5].Split('=');
            if (slParts.Length != 2 || !double.TryParse(slParts[1], out double stopLoss))
            {
                throw new ArgumentException("Invalid Stop Loss format.");
            }
            order.StopLoss = stopLoss;

            // Take profit
            var tpParts = parts[6].Split('=');
            if (tpParts.Length != 2 || !double.TryParse(tpParts[1], out double tp))
            {
                throw new ArgumentException("Invalid Risk format.");
            }
            order.TakeProfit = tp;

            // Parsing magic
            var magicPars = parts[7].Split('=');
            if (magicPars.Length != 2 || !int.TryParse(magicPars[1], out int magic))
            {
                throw new ArgumentException("Invalid Magic format.");
            }
            order.Magic = magic;

            // Parse strategy type
            var strParts = parts[8].Split('=');
            if (strParts.Length != 2 || !int.TryParse(strParts[1], out int strategy))
            {
                throw new ArgumentException("Invalid StrategyType format.");
            }
            order.StrategyType = (StrategyType)strategy;

            return order;
        }
    }
}
