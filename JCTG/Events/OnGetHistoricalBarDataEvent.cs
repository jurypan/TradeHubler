using JCTG.Models;

namespace JCTG.Events
{
    public class OnGetHistoricalBarDataEvent
    {
        public long ClientID { get; set; }
        public int AccountID { get; set; }
        public List<BarData> BarData { get; set; }
        public required Log Log { get; set; }
    }
}
