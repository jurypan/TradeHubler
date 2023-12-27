using System.ComponentModel.DataAnnotations;

namespace JCTG
{
    public class Log
    {
        public Log()
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public Account? Account { get; set; }
        public int AccountID { get; set; }

        public Client? Client { get; set; }
        public long ClientID { get; set; }
        public string? Type { get; set; }
        public string? ErrorType { get; set; }
        public string? Message { get; set; }
    }
}
