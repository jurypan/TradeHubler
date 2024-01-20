using Newtonsoft.Json;

namespace JCTG.Client
{
    public class MarketData
    {
        [JsonProperty("ask")]
        public double Ask { get; set; }


        [JsonProperty("bid")]
        public double Bid { get; set; }


        [JsonProperty("tick_value")]
        public double TickValue { get; set; }


        [JsonProperty("min_lot_size")]
        public double MinLotSize { get; set; }


        [JsonProperty("max_lot_size")]
        public double MaxLotSize { get; set; }


        [JsonProperty("volume_step")]
        public double LotStep { get; set; }


        [JsonProperty("tick_size")]
        public double TickSize { get; set; }

        public int Digits 
        {
            get
            {
                return CountSignificantDigits(this.TickSize);
            }
        }

        [JsonProperty("atr_M5")]
        public double ATR5M { get; set; }


        [JsonProperty("atr_M15")]
        public double ATR15M { get; set; }


        [JsonProperty("atr_H1")]
        public double ATR1H { get; set; }


        [JsonProperty("atr_D")]
        public double ATRD { get; set; }


        [JsonProperty("magic")]
        public int Magic { get; set; }

        public static int CountSignificantDigits(double number)
        {
            string numberAsString = number.ToString().TrimEnd('0');

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
