using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class Deal
    {
        public Deal()
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }

        public long OrderID { get; set; }
        public Order Order { get; set; }


        public long MtDealId { get; set; }

        public int Magic { get; set; }
        public string Symbol { get; set; }
        public double Lots { get; set; }
        public double Price { get; set; }
        public string Type { get; set; }
        public string Entry { get; set; }
        public double Pnl { get; set; }
        public double Commission { get; set; }
        public double Swap { get; set; }
        public double? AccountBalance { get; set; }
        public double? AccountEquity { get; set; }

        public double Spread { get; set; }
        public double SpreadCost { get; set; }
    }
}
