using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class TradeJournalDeal
    {
        public TradeJournalDeal()
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }

        public long TradeJournalID { get; set; }
        public TradeJournal TradeJournal { get; set; }


        public long DealId { get; set; }

        public int Magic { get; set; }
        public string Symbol { get; set; }
        public double Lots { get; set; }
        public string Type { get; set; }
        public string Entry { get; set; }
        public double Pnl { get; set; }
        public double Commission { get; set; }
        public double Swap { get; set; }
    }
}
