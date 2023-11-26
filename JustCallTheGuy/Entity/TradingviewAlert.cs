using System.ComponentModel.DataAnnotations;

namespace JustCallTheGuy
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
        public double Price { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public string Comment { get; set; }
        public List<Trade> Trades { get; set; }

        public static TradingviewAlert Parse(string input)
        {
            var parts = input.Split(',');

            if (parts.Length != 8)
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

            // Parsing Instrument
            order.Instrument = parts[2].ToUpper();

            // Parsing Price
            var priceParts = parts[3].Split('=');
            if (priceParts.Length != 2 || !double.TryParse(priceParts[1], out double price))
            {
                throw new ArgumentException("Invalid Price format.");
            }
            order.Price = price;

            // Parsing Stop Loss
            var slParts = parts[4].Split('=');
            if (slParts.Length != 2 || !double.TryParse(slParts[1], out double stopLoss))
            {
                throw new ArgumentException("Invalid Stop Loss format.");
            }
            order.StopLoss = stopLoss;

            // Take profit
            var tpParts = parts[5].Split('=');
            if (tpParts.Length != 2 || !double.TryParse(tpParts[1], out double tp))
            {
                throw new ArgumentException("Invalid Risk format.");
            }
            order.TakeProfit = tp;

            // Parsing comment
            var commentPars = parts[6].Split('=');
            if (commentPars.Length != 2)
            {
                throw new ArgumentException("Invalid Comment format.");
            }
            order.Comment = commentPars[1].Replace("\"", "");

            // Parse strategy type
            var strParts = parts[7].Split('=');
            if (strParts.Length != 2 || !StrategyType.TryParse(strParts[1], out StrategyType strategy))
            {
                throw new ArgumentException("Invalid strategytype format.");
            }
            order.StrategyType = strategy;

            return order;
        }
    }
}
