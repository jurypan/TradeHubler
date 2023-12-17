using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class TradeJournal
    {
        [Key]
        public int ID { get; set; }
        public DateTime DateCreated { get; set; }

        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public Client? Client { get; set; }
        public long ClientID { get; set; }

        public int Magic { get; set; }

        public string Instrument { get; set; }

        public double Lots { get; set; }

        public string Type { get; set; }

        public DateTimeOffset OpenTime { get; set; }

        public DateTimeOffset CloseTime { get; set; }

        public double OpenPrice { get; set; }

        public double ClosePrice { get; set; }

        public double SL { get; set; }

        public double TP { get; set; }

        public double Pnl { get; set; }

        public double Commission { get; set; }

        public double Swap { get; set; }

        public string Comment { get; set; }

        public StrategyType StrategyType { get; set; }

        public string Timeframe { get; set; }
    }
}
