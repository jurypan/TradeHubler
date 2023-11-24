namespace JustCallTheGuy.Strategy2
{
    public class TradeOrder
    {
        public long Id { get; set; }
        public string? OrderType { get; set; }
        public string? Instrument { get; set; }
        public decimal Price { get; set; }
        public decimal StopLoss { get; set; }
        public decimal Risk { get; set; }

        public static TradeOrder Parse(string input)
        {
            var parts = input.Split(',');

            if (parts.Length != 6)
            {
                throw new ArgumentException("Input string does not have the correct format.");
            }

            var order = new TradeOrder();

            // Parsing ID
            if (!long.TryParse(parts[0], out long id))
            {
                throw new ArgumentException("Invalid ID format.");
            }
            order.Id = id;

            // Parsing OrderType
            order.OrderType = parts[1];

            // Parsing Instrument
            order.Instrument = parts[2];

            // Parsing Price
            var priceParts = parts[3].Split('=');
            if (priceParts.Length != 2 || !decimal.TryParse(priceParts[1], out decimal price))
            {
                throw new ArgumentException("Invalid Price format.");
            }
            order.Price = price;

            // Parsing Stop Loss
            var slParts = parts[4].Split('=');
            if (slParts.Length != 2 || !decimal.TryParse(slParts[1], out decimal stopLoss))
            {
                throw new ArgumentException("Invalid Stop Loss format.");
            }
            order.StopLoss = stopLoss;

            // Parsing Risk
            var riskParts = parts[5].Split('=');
            if (riskParts.Length != 2 || !decimal.TryParse(riskParts[1], out decimal risk))
            {
                throw new ArgumentException("Invalid Risk format.");
            }
            order.Risk = risk;

            return order;
        }
    }
}
