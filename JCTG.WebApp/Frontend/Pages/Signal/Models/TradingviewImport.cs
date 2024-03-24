using JCTG.Models;
using JCTG.Entity;

namespace JCTG.WebApp.Frontend.Pages.Signal.Models
{
    public class TradingviewImport
    {

        public StrategyType StrategyType { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string Ticker { get; set; } = string.Empty;
        public string Timeframe { get; set; } = string.Empty;
        public List<TradingviewImportItem> Items { get; set; } = [];
    }

    public class TradingviewImportItem
    {
        public long ID { get; set; }
        public double? ExitRR { get; set; }
        public DateTime Date { get; set; }
        public int Magic { get; set; }
        public string Comment { get; set; } = string.Empty;
        public CrudState Action { get; set; } = CrudState.None;
        public Entity.Signal? Signal { get; set; }
    }
}
