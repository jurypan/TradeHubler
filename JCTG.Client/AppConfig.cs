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
       
        public int MaxRetryCommandSeconds { get; set; }
        public bool LoadOrdersFromFile { get; set; }
        public int SleepDelay { get; set; }
        public bool Verbose { get; set; }

    }
    public class Brokers
    {
        public Brokers()
        {
            Pairs = [];
            Risk = [];
            MetaTraderDirPath = string.Empty;
            Name = string.Empty;
        }

        public long ClientId { get; set; }
        public string Name { get; set; }
        public string MetaTraderDirPath { get; set; }
        public List<Pairs> Pairs { get; set; }
        public double StartBalance { get; set; }
        public List<Risk> Risk { get; set; }
        
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
        public decimal Risk { get; set; }
        public double SLtoBEafterR { get; set; }
        public decimal MaxSpread { get; set; }
        public double SLMultiplier { get; set; }
        public int MaxLotSize { get; set; }
    }

    public class Risk
    {
        public Risk()
        {

        }
        public double Procent { get; set; }
        public double Multiplier { get; set; }
    }
}
