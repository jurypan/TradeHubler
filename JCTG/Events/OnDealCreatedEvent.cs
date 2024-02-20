using JCTG.Models;

namespace JCTG.Events
{
    public class OnDealCreatedEvent
    {
        public long ClientID { get; set; }
        public long MtDealID { get; set; }
        public required Deal Deal { get; set; }
        public required Log Log { get; set; }
        public double? AccountBalance { get; set; }
        public double? AccountEquity { get; set; }



        public double Price { get; set; }
        public double Spread { get; set; }
        public double SpreadCost { get; set; }
    }
}
