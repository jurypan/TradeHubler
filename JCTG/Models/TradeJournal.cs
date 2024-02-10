﻿using JCTG.Events;

namespace JCTG.Models
{
    public class TradeJournal
    {
        public TradeJournal()
        {
            DateCreated = DateTime.UtcNow;
            Logs = [];
        }

        public DateTime DateCreated { get; set; }
        public Order? Order { get; set; }
        public required OnTradingviewSignalEvent Signal { get; set; }
        public List<Log> Logs { get; set; }
        public bool IsTradeClosed { get; set; }
    }
}
