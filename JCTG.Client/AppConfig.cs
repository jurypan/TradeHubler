namespace JCTG.Client
{
    public class AppConfig
    {
        public AppConfig() 
        {
            PairsToWatch = new List<PairsToWatch>();
            BrokersToWatch = new List<BrokersToWatch>();
        }


        public int AccountId { get; set; }
        public required List<PairsToWatch> PairsToWatch { get; set; }
        public required List<BrokersToWatch> BrokersToWatch { get; set; }
       
        public int SleepDelay { get; set; }
        public int MaxRetryCommandSeconds { get; set; }
        public bool LoadOrdersFromFile { get; set; }
        public bool Verbose { get; set; }

    }
    public class PairsToWatch
    {
        public int ClientId { get; set; }
        public required string TickerInTradingView { get; set; }
        public required string TickerInMetatrader { get; set; }
        public required string Timeframe { get; set; }
        public StrategyType StrategyNr { get; set; }
        public double Risk { get; set; }
    }

    public class BrokersToWatch
    {
        public int ClientId { get; set; }
        public string Name { get; set; }
        public required string MetaTraderDirPath { get; set; }
    }
}
