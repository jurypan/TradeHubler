using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class Log
    {
        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }


        public DateTime Time { get; set; }
        public string? Type { get; set; }
        public string? Message { get; set; }
        public string? ErrorType { get; set; }
        public string? Description { get; set; }

        public long ClientID { get; set; }
        public Client? Client { get; set; }

        public long? TradeJournalID { get; set; }
        public TradeJournal? TradeJournal { get; set; }

    }
}