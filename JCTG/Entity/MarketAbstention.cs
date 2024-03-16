using JCTG.Models;
using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class MarketAbstention
    {
        public MarketAbstention()
        {
            DateCreated = DateTime.UtcNow;
            DateLastUpdated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastUpdated { get; set; }
        public long ClientID { get; set; }
        public Client? Client { get; set; }


        public long SignalID { get; set; }
        public Signal Signal { get; set; }

        public required string Symbol { get; set; }
        public required string Type { get; set; }

        public int Magic { get; set; }

        public string LogMessage { get; set; }

        public MarketAbstentionType MarketAbstentionType { get; set; }
    }
}
