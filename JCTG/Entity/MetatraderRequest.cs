namespace JCTG
{
    public class MetatraderRequest
    {
        public  MetatraderRequest() 
        {
            Instrument = string.Empty;
            TradingviewTicker = string.Empty;
        }

        public int AccountID { get; set; }
        public int ClientID { get; set; }
        public string Instrument { get; set; }
        public double Price { get; set; }
        public string TradingviewTicker { get; set; }
        public StrategyType StrategyType { get; set; }

        public static MetatraderRequest Parse(string input)
        {
            var parts = input.Split(',');

            if (parts.Length != 6)
            {
                throw new ArgumentException("Input string does not have the correct format.");
            }

            var mtRequest = new MetatraderRequest();

            // Parsing Account ID
            if (!int.TryParse(parts[0], out int accountId))
            {
                throw new ArgumentException("Invalid Account ID format.");
            }
            mtRequest.AccountID = accountId;

            // Parsing Client ID
            if (!int.TryParse(parts[1], out int clientId))
            {
                throw new ArgumentException("Invalid Client ID format.");
            }
            mtRequest.ClientID = clientId;


            // Parsing Instrument
            mtRequest.Instrument = parts[2].ToUpper();

            // Parsing CurrentPrice
            if (!double.TryParse(parts[3], out double price))
            {
                throw new ArgumentException("Invalid CurrentPrice format.");
            }
            mtRequest.Price = price;


            // Parsing Tradingview Ticker
            mtRequest.TradingviewTicker = parts[4].ToUpper();

            // Parse strategy type
            if (!int.TryParse(parts[5], out int strategy))
            {
                throw new ArgumentException("Invalid strategytype format.");
            }
            mtRequest.StrategyType = (StrategyType)int.Parse(parts[5]);


            return mtRequest;
        }
    }
}
