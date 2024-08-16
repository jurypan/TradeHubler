using Newtonsoft.Json;
using System.Web;

namespace JCTG.Models
{
    public class Order
    {
        public string? Symbol { get; set; }

        public decimal Lots { get; set; }

        public string? Type { get; set; }

        [JsonProperty("open_price")]
        public decimal OpenPrice { get; set; }


        [JsonProperty("open_time")]
        public DateTime OpenTime { get; set; }

        [JsonProperty("close_time")]
        public DateTime? CloseTime { get; set; }


        [JsonProperty("sl")]
        public decimal StopLoss { get; set; }

        [JsonProperty("tp")]
        public decimal TakeProfit { get; set; }

        public double Pnl { get; set; }

        public double Commission { get; set; }

        public double Swap { get; set; }

        public string? Comment { get; set; }

        public int Magic { get; set; }

        public string ToQueryString()
        {
            var queryParameters = new List<string>();

            if (!string.IsNullOrEmpty(Symbol))
                queryParameters.Add($"Symbol={HttpUtility.UrlEncode(Symbol)}");

            queryParameters.Add($"Lots={Lots}");

            if (!string.IsNullOrEmpty(Type))
                queryParameters.Add($"Type={HttpUtility.UrlEncode(Type)}");

            queryParameters.Add($"open_price={OpenPrice}");
            queryParameters.Add($"open_time={HttpUtility.UrlEncode(OpenTime.ToString("o"))}");

            if (CloseTime.HasValue)
                queryParameters.Add($"close_time={HttpUtility.UrlEncode(CloseTime.Value.ToString("o"))}");

            queryParameters.Add($"sl={StopLoss}");
            queryParameters.Add($"tp={TakeProfit}");
            queryParameters.Add($"Pnl={Pnl}");
            queryParameters.Add($"Commission={Commission}");
            queryParameters.Add($"Swap={Swap}");

            if (!string.IsNullOrEmpty(Comment))
                queryParameters.Add($"Comment={HttpUtility.UrlEncode(Comment)}");

            queryParameters.Add($"Magic={Magic}");

            return string.Join(",", queryParameters);
        }
    }
}
