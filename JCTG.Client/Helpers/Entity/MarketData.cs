using Newtonsoft.Json;
using System.Globalization;

namespace JCTG.Client
{
    public class MarketData
    {
        [JsonProperty("ask")]
        public decimal Ask { get; set; }


        [JsonProperty("bid")]
        public decimal Bid { get; set; }


        [JsonProperty("tick_value")]
        public decimal TickValue { get; set; }


        [JsonProperty("min_lot_size")]
        public double MinLotSize { get; set; }


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
                return CountSignificantDigits(this.TickSize);
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
