namespace JCTG
{
    public class MetatraderRequest
    {
        public MetatraderRequest()
        {
            TickerInMetatrader = string.Empty;
            TickerInTradingview = string.Empty;
        }

        public int AccountID { get; set; }
        public long ClientID { get; set; }
        public string TickerInMetatrader { get; set; }
        public double Ask { get; set; }
        public double Bid { get; set; }
        public string TickerInTradingview { get; set; }
        public string TickerInFMP { get; set; }
        public StrategyType StrategyType { get; set; }
        public double Atr5M { get; set; }
        public double Atr15M { get; set; }
        public double Atr1H { get; set; }
        public double AtrD { get; set; }

    }
}
