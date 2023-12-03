namespace JCTG.Client
{
    public class AppConfig
    {
        public AppConfig() 
        {
            Brokers = [];
        }


        public int AccountId { get; set; }

        public required List<Brokers> Brokers { get; set; }
       
        public int SleepDelay { get; set; }
        public int MaxRetryCommandSeconds { get; set; }
        public bool LoadOrdersFromFile { get; set; }
        public bool Verbose { get; set; }

    }
    public class Brokers
    {
        public Brokers()
        {
            Pairs = [];
        }

        public int ClientId { get; set; }
        public string Name { get; set; }
        public required string MetaTraderDirPath { get; set; }
        public required List<Pairs> Pairs { get; set; }
    }
    public class Pairs
    {
        public required string TickerInTradingView { get; set; }
        public required string TickerInMetatrader { get; set; }
        public required string Timeframe { get; set; }
        public StrategyType StrategyNr { get; set; }
        public double Risk { get; set; }
    }


}
