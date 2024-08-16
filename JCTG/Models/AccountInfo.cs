using Newtonsoft.Json;
using System.Web;

namespace JCTG.Models
{
    public class AccountInfo
    {
        public string Name { get; set; }
        public int Number { get; set; }
        public string Currency { get; set; }
        public int Leverage { get; set; }
        [JsonProperty("free_margin")]
        public double FreeMargin { get; set; }
        public double Balance { get; set; }
        public double Equity { get; set; }
        [JsonProperty("tmz")]
        public double TimezoneOffset { get; set; }

        public string ToQueryString()
        {
            var queryParameters = new List<string>
            {
                $"Name={HttpUtility.UrlEncode(Name)}",
                $"Number={Number}",
                $"Currency={HttpUtility.UrlEncode(Currency)}",
                $"Leverage={Leverage}",
                $"FreeMargin={FreeMargin}",
                $"Balance={Balance}",
                $"Equity={Equity}",
                $"TimezoneOffset={TimezoneOffset}"
            };

            return string.Join("&", queryParameters);
        }
    }
}
