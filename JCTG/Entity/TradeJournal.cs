using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class TradeJournal
    {
        public TradeJournal()
        {
            DateCreated = DateTime.UtcNow;
            Logs = new List<Log>();
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        
        public List<Log> Logs { get; set; }
        public bool IsTradeClosed { get; set; }


        public long ClientID { get; set; }
        public Client? Client { get; set; }
        

        public long? OrderID { get; set; }
        public Order? Order { get; set; }

        public long SignalID { get; set; }
        public Signal Signal { get; set; }
    }
}
