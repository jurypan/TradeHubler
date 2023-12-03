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
        public int ClientID { get; set; }
        public string TickerInMetatrader { get; set; }
        public double Price { get; set; }
        public string TickerInTradingview { get; set; }
        public StrategyType StrategyType { get; set; }

    }
}
