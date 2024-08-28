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
            MetaTraderDirPath = string.Empty;
            Name = string.Empty;
        }

        public long ClientId { get; set; }
        public string Name { get; set; }
        public string MetaTraderDirPath { get; set; }
        public bool IsEnable { get; set; }
        public List<Pairs> Pairs { get; set; } = [];
        public double StartBalance { get; set; }
        public List<Risk> Risk { get; set; } = [];

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
        public TimeSpan TimeframeAsTimespan
        { 
            get 
            {
                return Timeframe switch
                {
                    "M1" => TimeSpan.FromMinutes(1),
                    "M2" => TimeSpan.FromMinutes(2),
                    "M3" => TimeSpan.FromMinutes(3),
                    "M4" => TimeSpan.FromMinutes(4),
                    "M5" => TimeSpan.FromMinutes(5),
                    "M6" => TimeSpan.FromMinutes(6),
                    "M10" => TimeSpan.FromMinutes(10),
                    "M12" => TimeSpan.FromMinutes(12),
                    "M15" => TimeSpan.FromMinutes(15),
                    "M20" => TimeSpan.FromMinutes(20),
                    "M30" => TimeSpan.FromMinutes(30),
                    "H1" => TimeSpan.FromHours(1),
                    "H2" => TimeSpan.FromHours(2),
                    "H3" => TimeSpan.FromHours(3),
                    "H4" => TimeSpan.FromHours(4),
                    "H6" => TimeSpan.FromHours(6),
                    "H8" => TimeSpan.FromHours(8),
                    "H12" => TimeSpan.FromHours(12),
                    "D" => TimeSpan.FromDays(1),
                    _ => TimeSpan.Zero
                };
            }
        }
        public long StrategyID { get; set; }
        public decimal RiskLong { get; set; }
        public decimal RiskShort { get; set; }
        public double SLtoBEafterR { get; set; }
        public decimal MaxSpread { get; set; }
        public int AdaptPassiveOrdersBeforeEntryInSeconds { get; set; }
        public double SLMultiplier { get; set; }
        public int MaxLotSize { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public OrderExecType OrderExecType { get; set; }
        public bool CancelStopOrLimitOrderWhenNewSignal { get; set; }
        public bool ExecuteMarketOrderOnEntryIfNoPendingOrders { get; set; }
        public int NumberOfHistoricalBarsRequested { get; set; }
        public TimeSpan? CloseAllTradesAt { get; set; }
        public int? CloseTradeWithinXBars { get; set; }
        public int? DoNotOpenTradeXMinutesBeforeClose { get; set; }
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
        Subtract = 1,
        TwiceAdd = 10,
        TwiceSubtract = 11
    }

    public enum OrderExecType
    {
        Active = 0,
        Passive = 1,
    }
}
