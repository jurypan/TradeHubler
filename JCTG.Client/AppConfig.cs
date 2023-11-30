namespace JCTG.Client
{
    public class AppConfig
    {
        public AppConfig() 
        {
            PairsToWatch = new List<PairsToWatch>();
        }

        public int AccountId { get; set; }
        public int ClientId { get; set; }
        public required List<PairsToWatch> PairsToWatch { get; set; }
        public required string MetaTraderDirPath { get; set; }
        public int SleepDelay { get; set; }
        public int MaxRetryCommandSeconds { get; set; }
        public bool LoadOrdersFromFile { get; set; }
        public bool Verbose { get; set; }

    }
    public class PairsToWatch
    {
        public required string TickerInTradingView { get; set; }
        public required string TickerInMetatrader { get; set; }
        public required string Timeframe { get; set; }
        public StrategyType StrategyNr { get; set; }
        public double Risk { get; set; }
    }
}
