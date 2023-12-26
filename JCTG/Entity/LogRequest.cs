namespace JCTG
{
    public class LogRequest
    {
        public LogRequest()
        {

        }

        public int AccountID { get; set; }
        public long ClientID { get; set; }
        public string? Type { get; set; }
        public string? ErrorType { get; set; }
        public string? Message { get; set; }


    }
}
