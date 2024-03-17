using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JCTG.Entity
{
    public class Order
    {
        public Order()
        {
            DateCreated = DateTime.UtcNow;
            DateLastUpdated = DateTime.UtcNow;
            Symbol = string.Empty;
            Type = string.Empty;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastUpdated { get; set; }

        public bool IsTradeClosed { get; set; } // Managed by DEAL


        public long ClientID { get; set; }
        public Client? Client { get; set; }
        

        public long SignalID { get; set; }
        public Signal Signal { get; set; }


        public required string Symbol { get; set; } // Managed by ORDER
        public required string Type { get; set; } // Managed by ORDER

        public DateTime OpenTime { get; set; } // Managed by ORDER
        [NotMapped]
        public string OpenTimeAsString
        {
            get => OpenTime.ToString();
            set
            {
                if (DateTime.TryParse(value, out DateTime newValue))
                {
                    OpenTime = newValue;
                }
            }
        }
        public double OpenLots { get; set; } // Managed by ORDER
        [NotMapped]
        public string OpenLotsAsString
        {
            get => OpenLots.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    OpenLots = newValue;
                }
            }
        }
        public double OpenPrice { get; set; } // Managed by ORDER
        [NotMapped]
        public string OpenPriceAsString
        {
            get => OpenPrice.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    OpenPrice = newValue;
                }
            }
        }
        public double? OpenStopLoss { get; set; } // Managed by ORDER
        [NotMapped]
        public string? OpenStopLossAsString
        {
            get => OpenStopLoss.HasValue ? OpenStopLoss.Value.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    OpenStopLoss = newValue;
                }
            }
        }
        public double? OpenTakeProfit { get; set; } // Managed by ORDER
        [NotMapped]
        public string? OpenTakeProfitAsString
        {
            get => OpenTakeProfit.HasValue ? OpenTakeProfit.Value.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    OpenTakeProfit = newValue;
                }
            }
        }


        public DateTime? CloseTime { get; set; } // Managed by DEAL
        [NotMapped]
        public string? CloseTimeAsString
        {
            get => CloseTime.HasValue ? CloseTime.Value.ToString() : null;
            set
            {
                if (DateTime.TryParse(value, out DateTime newValue))
                {
                    CloseTime = newValue;
                }
            }
        }
        public double? ClosePrice { get; set; } // Managed by ORDER
        [NotMapped]
        public string? ClosePriceAsString
        {
            get => ClosePrice.HasValue ? ClosePrice.Value.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    ClosePrice = newValue;
                }
            }
        }
        public double? CloseStopLoss { get; set; } // Managed by ORDER
        [NotMapped]
        public string? CloseStopLossAsString
        {
            get => CloseStopLoss.HasValue ? CloseStopLoss.Value.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    CloseStopLoss = newValue;
                }
            }
        }
        public double? CloseTakeProfit { get; set; } // Managed by ORDER
        [NotMapped]
        public string? CloseTakeProfitAsString
        {
            get => CloseTakeProfit.HasValue ? CloseTakeProfit.Value.ToString() : null;
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    CloseTakeProfit = newValue;
                }
            }
        }


        public double Pnl { get; set; } // Managed by DEAL
        [NotMapped]
        public string PnlAsString
        {
            get =>  Pnl.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    Pnl = newValue;
                }
            }
        }
        public double Commission { get; set; } // Managed by DEAL
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
        public double Swap { get; set; } // Managed by DEAL
        [NotMapped]
        public string? SwapAsString
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
        public double SpreadCost { get; set; } // Managed by DEAL
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
        public string? Comment { get; set; } // Managed by ORDER
        public int Magic { get; set; } // Managed by ORDER
        [NotMapped]
        public string MagicAsString
        {
            get => Magic.ToString();
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    Magic = newValue;
                }
            }
        }


        public List<Deal>  Deals { get; set; } // Managed by TRADE
    }
}
