using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class Account
    {
        [Key]
        public int ID { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public required string Name { get; set; } = string.Empty;
        public List<Client> Clients { get; set; } = [];
        public List<Signal> Signals { get; set; } = [];
        public List<Strategy> Strategies { get; set; } = [];
        public List<TradingviewAlert> TradingviewAlerts { get; set; } = [];
    }
}
