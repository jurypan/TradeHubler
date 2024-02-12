﻿using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class TradeJournal
    {
        public TradeJournal()
        {
            DateCreated = DateTime.UtcNow;
        }

        [Key]
        public long ID { get; set; }
        public DateTime DateCreated { get; set; }
       
        public bool IsTradeClosed { get; set; }


        public long ClientID { get; set; }
        public Client? Client { get; set; }
        

        public long SignalID { get; set; }
        public Signal Signal { get; set; }

        public long? TradeID { get; set; } // Managed by TRADE
        public required string Symbol { get; set; } // Managed by ORDER
        public required string Type { get; set; } // Managed by ORDER

        public DateTime OpenTime { get; set; } // Managed by TRADE
        public double OpenLots { get; set; } // Managed by TRADE
        public double OpenPrice { get; set; } // Managed by ORDER
        public double? OpenStopLoss { get; set; } // Managed by ORDER
        public double? OpenTakeProfit { get; set; } // Managed by ORDER


        public DateTime? CloseTime { get; set; } // Managed by TRADE
        public double? CloseLots { get; set; }  // Managed by TRADE
        public double? ClosePrice { get; set; } // Managed by ORDER
        public double? CloseStopLoss { get; set; } // Managed by ORDER
        public double? CloseTakeProfit { get; set; } // Managed by ORDER


        public double Pnl { get; set; } // Managed by TRADE
        public double Commission { get; set; } // Managed by TRADE
        public double Swap { get; set; } // Managed by TRADE
        public string? Comment { get; set; } // Managed by ORDER
        public int Magic { get; set; } // Managed by ORDER
    }
}
