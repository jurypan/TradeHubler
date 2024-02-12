using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class Account
    {
        public Account() 
        {
            Clients = [];
        }

        [Key]
        public int ID { get; set; }
        public DateTime DateCreated { get; set; }
        public string Name { get; set; }
        public List<Client> Clients { get; set; }
        public List<Signal> Signals { get; set; }
    }
}
