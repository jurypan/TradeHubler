using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
            MarketAbstentions = [];
        }

        [Key]
        public long ID { get; set; }
        [NotMapped]
        public string IDAsString
        {
            get => ID.ToString();
            set
            {
                if (long.TryParse(value, out long newValue))
                {
                    ID = newValue;
                }
            }
        }
        public DateTime DateCreated { get; set; }
        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public required string Name { get; set; }
        public string? Currency { get; set; }
        public int? Leverage { get; set; }
        public double? Balance { get; set; }
        public double? Equity { get; set; }
        public double StartBalance { get; set; }
        [NotMapped]
        public string StartBalanceAsString
        {
            get => StartBalance.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    StartBalance = newValue;
                }
            }
        }
        public bool IsEnable { get; set; }
        public string MetaTraderDirPath { get; set; }

        public List<ClientRisk> Risks { get; set; }
        public List<ClientPair> Pairs { get; set; }

        public List<Order> Orders { get; set; }
        public List<MarketAbstention> MarketAbstentions { get; set; }
        public List<Log> Logs { get; set; }
    }
}
