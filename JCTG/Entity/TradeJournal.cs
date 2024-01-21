using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class TradeJournal
    {
        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }

        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public Client? Client { get; set; }
        public long ClientID { get; set; }

        public int Magic { get; set; }

        public long TicketId { get; set; }

        public decimal Risk { get; set; }

        public required string Instrument { get; set; }

        public decimal Lots { get; set; }

        public required string Type { get; set; }

        public DateTime OpenTime { get; set; }

        public DateTime? CloseTime { get; set; }

        public decimal OpenPrice { get; set; }

        public decimal? ClosePrice { get; set; }

        public decimal OpenSL { get; set; }

        public decimal OpenTP { get; set; }

        public decimal Spread { get; set; }

        public decimal SL { get; set; }

        public decimal TP { get; set; }

        public double Pnl { get; set; }

        public double Commission { get; set; }

        public double Swap { get; set; }

        public string? Comment { get; set; }

        public StrategyType StrategyType { get; set; }

        public required string Timeframe { get; set; }
        public decimal RR { get; set; }
    }
}
