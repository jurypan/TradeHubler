using JCTG.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [NotMapped]
        public string ProcentAsString
        {
            get => Procent.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Procent = newValue;
                }
            }
        }
        public double Multiplier { get; set; }
        [NotMapped]
        public string MultiplierAsString
        {
            get => Multiplier.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Multiplier = newValue;
                }
            }
        }
    }
}
