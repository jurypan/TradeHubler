using Newtonsoft.Json;
using System.Globalization;

namespace JCTG.Models
{
    public class MarketData
    {
        private decimal _ask;
        private decimal _bid;

        [JsonProperty("ask")]
        public decimal Ask
        {
            get { return Math.Round(_ask, Digits, MidpointRounding.AwayFromZero); }
            set { _ask = value; }
        }

        [JsonProperty("bid")]
        public decimal Bid
        {
            get { return Math.Round(_bid, Digits, MidpointRounding.AwayFromZero); }
            set { _bid = value; }
        }


        [JsonProperty("tick_value")]
        public decimal TickValue { get; set; }


        [JsonProperty("min_lot_size")]
        public double MinLotSize { get; set; }

        [JsonProperty("point")]
        public double PointSize { get; set; }

        [JsonProperty("contract_size")]
        public double ContractSize { get; set; }


        [JsonProperty("max_lot_size")]
        public double MaxLotSize { get; set; }


        [JsonProperty("volume_step")]
        public double LotStep { get; set; }


        [JsonProperty("tick_size")]
        public decimal TickSize { get; set; }

        public int Digits
        {
            get
            {
                return CountSignificantDigits(TickSize);
            }
        }

        [JsonProperty("atr_M5")]
        public decimal ATR5M { get; set; }


        [JsonProperty("atr_M15")]
        public decimal ATR15M { get; set; }


        [JsonProperty("atr_H1")]
        public decimal ATR1H { get; set; }


        [JsonProperty("atr_D")]
        public decimal ATRD { get; set; }


        [JsonProperty("magic")]
        public int Magic { get; set; }

        public static int CountSignificantDigits(decimal number)
        {
            string numberAsString = number.ToString(CultureInfo.InvariantCulture).TrimEnd('0');

            int decimalPointIndex = numberAsString.IndexOf('.');
            if (decimalPointIndex == -1)
            {
                // No decimal point, so no significant digits after the decimal.
                return 0;
            }

            return numberAsString.Length - decimalPointIndex - 1;
        }
    }
}
