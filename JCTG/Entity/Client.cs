using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class Client
    {
        public Client()
        {
            DateCreated = DateTime.UtcNow;
            Orders = [];
            Logs = [];
            Risks = [];
            Pairs = [];
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public required string Name { get; set; }
        public string? Currency { get; set; }
        public int? Leverage { get; set; }
        public double? Balance { get; set; }
        public double? Equity { get; set; }
        public double StartBalance { get; set; }

        public bool IsEnable { get; set; }
        public string MetaTraderDirPath { get; set; }

        public List<ClientRisk> Risks { get; set; }
        public List<ClientPair> Pairs { get; set; }

        public List<Order> Orders { get; set; }
        public List<Log> Logs { get; set; }
    }
}
