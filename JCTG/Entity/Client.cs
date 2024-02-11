using JCTG.Entity;
using System.ComponentModel.DataAnnotations;

namespace JCTG
{
    public class Client
    {
        public Client()
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public required string Name { get; set; }

        public List<TradeJournal> TradeJournals { get; set; }
    }
}
