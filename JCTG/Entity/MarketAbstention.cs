using JCTG.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        public string Symbol { get; set; }
        public string Type { get; set; }

        public int Magic { get; set; }
        [NotMapped]
        public string MagicAsString
        {
            get => Magic.ToString();
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    Magic = newValue;
                }
            }
        }
        public string? Command { get; set; }
        public string Description { get; set; }

        public MarketAbstentionType MarketAbstentionType { get; set; }
    }
}
