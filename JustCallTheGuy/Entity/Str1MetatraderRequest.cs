namespace JustCallTheGuy
{
    public class Str1MetatraderRequest
    {
        public Str1MetatraderRequest() 
        {
            Instrument = string.Empty;
            TradingviewTicker = string.Empty;
        }

        public int AccountID { get; set; }
        public int ClientID { get; set; }
        public string Instrument { get; set; }
        public double Price { get; set; }
        public string TradingviewTicker { get; set; }

        public static Str2MetatraderRequest Parse(string input)
        {
            var parts = input.Split(',');

            if (parts.Length != 5)
            {
                throw new ArgumentException("Input string does not have the correct format.");
            }

            var mtRequest = new Str2MetatraderRequest();

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

            // Parsing Price
            if (!double.TryParse(parts[3], out double price))
            {
                throw new ArgumentException("Invalid Price format.");
            }
            mtRequest.Price = price;


            // Parsing Tradingview Ticker
            mtRequest.TradingviewTicker = parts[4].ToUpper();


            return mtRequest;
        }
    }
}
