using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class Strategy
    {
        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public required string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Type { get; set; } = 1;


        // Links
        public List<Signal> Signals { get; set; } = [];
    }
}
