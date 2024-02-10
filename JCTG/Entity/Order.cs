using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class Order
    {
        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public long TradeJournalID { get; set; }
        public required TradeJournal TradeJournal { get; set; }
        public required string Symbol { get; set; }
        public required string Type { get; set; }
        public double Lots { get; set; }
        public decimal OpenPrice { get; set; }
        public DateTime OpenTime { get; set; }
        public decimal? ClosePrice { get; set; }
        public DateTime? CloseTime { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
        public double Pnl { get; set; }
        public double Commission { get; set; }
        public double Swap { get; set; }
        public string? Comment { get; set; }
        public int Magic { get; set; }
    }
}
