using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace JCTG.Models
{
    public class TerminalConfig
    {
        public TerminalConfig() 
        {
            Brokers = new List<Brokers>();
        }


        public int AccountId { get; set; }

        public List<Brokers> Brokers { get; set; }
       
        public int MaxRetryCommandSeconds { get; set; }
        public bool LoadOrdersFromFile { get; set; }
        public int SleepDelay { get; set; }
        public bool DropLogsInFile { get; set; }
        public bool Debug { get; set; }
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
        public bool IsEnable { get; set; }
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
            CorrelatedPairs = [];
        }

        public required string TickerInTradingView { get; set; }
        public required string TickerInMetatrader { get; set; }
        public required string Timeframe { get; set; }
        public long StrategyID { get; set; }
        public decimal Risk { get; set; }
        public double SLtoBEafterR { get; set; }
        public decimal MaxSpread { get; set; }
        public double SLMultiplier { get; set; }
        public int MaxLotSize { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public OrderExecType OrderExecType { get; set; }
        public bool CancelStopOrLimitOrderWhenNewSignal { get; set; }
        public int NumberOfHistoricalBarsRequested { get; set; }
        public TimeSpan? CloseAllTradesAt { get; set; }
        public int? DoNotOpenTradeXMinutesBeforeClose { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public SpreadExecType? SpreadEntry { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public SpreadExecType? SpreadSL { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public SpreadExecType? SpreadTP { get; set; }
        public int RiskMinXTimesTheSpread { get; set; }
        public List<string> CorrelatedPairs { get; set; } = [];
    }

    public class Risk
    {
        public Risk()
        {

        }
        public double Procent { get; set; }
        public double Multiplier { get; set; }
    }

    public enum SpreadExecType
    {
        Add = 0,
        Subtract = 1
    }

    public enum OrderExecType
    {
        Active = 0,
        Passive = 1,
    }
}
