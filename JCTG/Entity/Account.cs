using System.ComponentModel.DataAnnotations;
using JCTG.Entity;

namespace JCTG
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
