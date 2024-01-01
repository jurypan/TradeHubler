using JCTG.Entity;
using System.ComponentModel.DataAnnotations;

namespace JCTG
{
    public class Client
    {
        public Client()
        {
            DateCreated = DateTime.UtcNow;
            Trades = [];
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public required string Name { get; set; }
        public List<SignalExecuted> Trades { get; set; }
        public List<TradeJournal> TradeJournals { get; set; }
        public List<Log> Logs { get; set; }
    }
}
