using Newtonsoft.Json;

namespace JCTG.Client
{
    public class AppConfig
    {
        public AppConfig() 
        {
            Brokers = new List<Brokers>();
        }


        public int AccountId { get; set; }

        public List<Brokers> Brokers { get; set; }
       
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
            MetaTraderDirPath = string.Empty;
            Name = string.Empty;
        }

        public long ClientId { get; set; }
        public string Name { get; set; }
        public string MetaTraderDirPath { get; set; }
        public List<Pairs> Pairs { get; set; }
    }
    public class Pairs
    {
        public Pairs() 
        {
            TickerInTradingView = string.Empty;
            TickerInMetatrader = string.Empty;
            Timeframe = string.Empty;
        }

        public required string TickerInTradingView { get; set; }
        public required string TickerInMetatrader { get; set; }
        public required string Timeframe { get; set; }
        public StrategyType StrategyNr { get; set; }
        public double Risk { get; set; }
        public bool OverrideSession { get; set; }
        public Dictionary<string, SessionTimes> Sessions { get; set; }
    }
    public class SessionTimes
    {
        public TimeSpan? Open { get; set; }
        public TimeSpan? Close { get; set; }
    }
}
