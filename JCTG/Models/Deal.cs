using Newtonsoft.Json;
using System.Web;

namespace JCTG.Models
{
    public class Deal
    {
        public bool IsMT4 { get; set; }
        public int Magic { get; set; }
        public string Symbol { get; set; }
        public double Price { get; private set; } // open_price (in MT4)  and deal_price (in MT5)


        // Temporary properties to catch the JSON values
        [JsonProperty("open_price")]
        private double OpenPrice
        {
            set { Price = value; }
        }

        [JsonProperty("deal_price")]
        private double DealPrice
        {
            set { Price = value; }
        }



        public double Lots { get; set; }
        public string Type { get; set; }
        public string Entry { get; set; } // In MT4 is always "trade"
        public double Pnl { get; set; }
        public double Commission { get; set; }
        public double Swap { get; set; }

        public string ToQueryString()
        {
            var queryParameters = new List<string>
            {
                $"IsMT4={IsMT4.ToString().ToLower()}",
                $"Magic={Magic}",
                $"Symbol={HttpUtility.UrlEncode(Symbol)}",
                $"Price={Price}",
                $"Lots={Lots}",
                $"Type={HttpUtility.UrlEncode(Type)}",
                $"Entry={HttpUtility.UrlEncode(Entry)}",
                $"Pnl={Pnl}",
                $"Commission={Commission}",
                $"Swap={Swap}"
            };

            return string.Join(",", queryParameters);
        }
    }
}
