using System.ComponentModel.DataAnnotations;

namespace JCTG
{
    public class Trade
    {
        public Trade() 
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public int ID { get; set; }
        public DateTime DateCreated { get; set; }
        
        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public Client? Client { get; set; }
        public long ClientID { get; set; }
        public TradingviewAlert? TradingviewAlert { get; set; }
        public long TradingviewAlertID { get; set; }
        public StrategyType StrategyType { get; set; }
        public double Offset { get; set; }
        public double Spread { get; set; }
        public required string Instrument { get; set; }
        public int Magic { get; set; }
        public bool Executed { get; set; }
        public DateTime? DateExecuted { get; set; }
        public double? ExecutedPrice { get; set; }
        public double? ExecutedSL { get; set; }
        public double? ExecutedTP { get; set; }
    }
}
