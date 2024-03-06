using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class ClientRisk
    {
        public ClientRisk()
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public Client? Client { get; set; }
        public long ClientID { get; set; }
        public double Procent { get; set; }
        public double Multiplier { get; set; }

    }
}
