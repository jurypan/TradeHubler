using JCTG.Entity;
using System.ComponentModel.DataAnnotations;

namespace JCTG
{
    public class Account
    {
        public Account() 
        {
            Clients = [];
            Trades = [];
        }

        [Key]
        public int ID { get; set; }
        public DateTime DateCreated { get; set; }
        public string Name { get; set; }
        public List<Client> Clients { get; set; }
        public List<SignalExecuted> Trades { get; set; }
        public List<Signal> Signals { get; set; }
        public List<TradeJournal> TradeJournals { get; set; }
        public List<Log> Logs { get; set; }
    }
}
