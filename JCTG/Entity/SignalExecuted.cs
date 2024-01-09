using System.ComponentModel.DataAnnotations;

namespace JCTG
{
    public class SignalExecuted
    {
        public SignalExecuted() 
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
        public Signal? Signal { get; set; }
        public long SignalID { get; set; }
        public StrategyType StrategyType { get; set; }
        public double Offset { get; set; }
        public double Spread { get; set; }
        public required string Instrument { get; set; }
        public long Magic { get; set; }
        public double ExecutedPrice { get; set; }
        public double ExecutedSL { get; set; }
        public double ExecutedTP { get; set; }
        public double Atr5M { get; set; }
        public double Atr15M { get; set; }
        public double Atr1H { get; set; }
        public double AtrD { get; set; }
    }
}
