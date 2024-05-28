using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JCTG.Entity
{
    public class Client
    {
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
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Currency { get; set; }
        public int? Leverage { get; set; }
        [NotMapped]
        public string? LeverageAsString
        {
            get => Leverage?.ToString();
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    Leverage = newValue;
                }
            }
        }
        public double? Balance { get; set; }
        [NotMapped]
        public string? BalanceAsString
        {
            get => Balance?.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Balance = newValue;
                }
            }
        }
        public double? Equity { get; set; }
        [NotMapped]
        public string? EquityAsString
        {
            get => Equity?.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Equity = newValue;
                }
            }
        }
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
        public  string MetaTraderDirPath { get; set; } = string.Empty;

        public List<ClientRisk> Risks { get; set; } = [];
        public List<ClientPair> Pairs { get; set; } = [];

        public List<Order> Orders { get; set; } = [];
        public List<MarketAbstention> MarketAbstentions { get; set; } = [];
        public List<Log> Logs { get; set; } = [];
    }
}
