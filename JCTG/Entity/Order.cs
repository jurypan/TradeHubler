using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class Order
    {
        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public long TradeJournalID { get; set; }
        public TradeJournal? TradeJournal { get; set; }


        public required string Symbol { get; set; }
        public required string Type { get; set; }
        
        public DateTime OpenTime { get; set; }
        public double OpenLots { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal? OpenStopLoss { get; set; }
        public decimal? OpenTakeProfit { get; set; }


        public DateTime? CloseTime { get; set; }
        public double CloseLots { get; set; }
        public decimal? ClosePrice { get; set; }
        public decimal? CloseStopLoss { get; set; }
        public decimal? CloseTakeProfit { get; set; }
        
       
        public double Pnl { get; set; }
        public double Commission { get; set; }
        public double Swap { get; set; }
        public string? Comment { get; set; }
        public int Magic { get; set; }

    }
}
