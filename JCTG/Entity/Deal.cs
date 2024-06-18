using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [NotMapped]
        public string MtDealIdAsString
        {
            get => MtDealId.ToString();
            set
            {
                if (long.TryParse(value, out long newValue))
                {
                    MtDealId = newValue;
                }
            }
        }

        public int Magic { get; set; }
        [NotMapped]
        public string MagicAsString
        {
            get => Magic.ToString();
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    MtDealId = newValue;
                }
            }
        }
        public string Symbol { get; set; }
        public double Lots { get; set; }
        [NotMapped]
        public string LotsAsString
        {
            get => Lots.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Lots = newValue;
                }
            }
        }
        public double Price { get; set; }
        [NotMapped]
        public string PriceAsString
        {
            get => Price.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Price = newValue;
                }
            }
        }
        public string Type { get; set; }
        public string Entry { get; set; }
        public double Pnl { get; set; }
        [NotMapped]
        public string PnlAsString
        {
            get => Pnl.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Pnl = newValue;
                }
            }
        }
        public double Commission { get; set; }
        [NotMapped]
        public string CommissionAsString
        {
            get => Commission.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Commission = newValue;
                }
            }
        }
        public double Swap { get; set; }
        [NotMapped]
        public string SwapAsString
        {
            get => Swap.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Swap = newValue;
                }
            }
        }
        public double? AccountBalance { get; set; }
        [NotMapped]
        public string? AccountBalanceAsString
        {
            get => AccountBalance.HasValue ? AccountBalance.Value.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    AccountBalance = newValue;
                }
            }
        }
        public double? AccountEquity { get; set; }
        [NotMapped]
        public string? AccountEquityAsString
        {
            get => AccountEquity.HasValue ? AccountEquity.Value.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    AccountEquity = newValue;
                }
            }
        }

        public double Spread { get; set; }
        [NotMapped]
        public string SpreadAsString
        {
            get => Spread.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Spread = newValue;
                }
            }
        }
        public double SpreadCost { get; set; }
        [NotMapped]
        public string SpreadCostAsString
        {
            get => SpreadCost.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    SpreadCost = newValue;
                }
            }
        }
    }
}
