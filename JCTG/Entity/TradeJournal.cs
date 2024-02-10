using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class TradeJournal
    {
        public TradeJournal()
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public Order? Order { get; set; }
        public List<Log> Logs { get; set; }
        public bool IsTradeClosed { get; set; }



        public Signal Signal { get; set; }
    }
}
