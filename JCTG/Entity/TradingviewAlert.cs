using JCTG.Entity;
using System.ComponentModel.DataAnnotations;

namespace JCTG
{
    public class TradingviewAlert
    {
        public TradingviewAlert()
        {
            DateCreated = DateTime.UtcNow;
            Trades = [];
        }

        [Key]
        public int ID { get; set; }
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
        public int Magic { get; set; }
        public double Atr5M { get; set; }
        public double Atr15M { get; set; }
        public double Atr1H { get; set; }
        public double AtrD { get; set; }


        public List<Trade> Trades { get; set; }
        public List<TradeJournal> TradeJournals { get; set; }

        public static TradingviewAlert Parse(string input)
        {
            var parts = input.Split(',');

            if (parts.Length != 13)
            {
                throw new ArgumentException("Input string does not have the correct format.");
            }

            var order = new TradingviewAlert();

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
            if (tpParts.Length != 2 || !int.TryParse(strParts[1], out int strategy))
            {
                throw new ArgumentException("Invalid StrategyType format.");
            }
            order.StrategyType = (StrategyType)strategy;

            // Parse ATR 5 minute
            var atr5MParts = parts[6].Split('=');
            if (atr5MParts.Length != 2 || !double.TryParse(atr5MParts[1], out double atr5M))
            {
                throw new ArgumentException("Invalid ATR 5 minute format.");
            }
            order.Atr5M = atr5M;

            // Parse ATR 15 minute
            var atr15MParts = parts[6].Split('=');
            if (atr5MParts.Length != 2 || !double.TryParse(atr15MParts[1], out double atr15M))
            {
                throw new ArgumentException("Invalid ATR 15 minute format.");
            }
            order.Atr15M = atr15M;

            // Parse ATR 1 hour
            var atr1HParts = parts[6].Split('=');
            if (atr5MParts.Length != 2 || !double.TryParse(atr1HParts[1], out double atr1H))
            {
                throw new ArgumentException("Invalid ATR 1 hour format.");
            }
            order.Atr1H = atr1H;

            // Parse ATR day
            var atrDParts = parts[6].Split('=');
            if (atr5MParts.Length != 2 || !double.TryParse(atr1HParts[1], out double atrD))
            {
                throw new ArgumentException("Invalid ATR day format.");
            }
            order.AtrD = atrD;

            return order;
        }
    }
}
