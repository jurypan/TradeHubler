namespace JCTG.Entity
{
    public class Log
    {
        public DateTime Time { get; set; }
        public string? Type { get; set; }
        public string? Message { get; set; }
        public string? ErrorType { get; set; }
        public string? Description { get; set; }

    }
}