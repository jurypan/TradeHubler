using JCTG.Models;

namespace JCTG.WebApp.Frontend.Pages.Signal.Models
{
    public class TradingviewImport
    {

        public StrategyType StrategyType { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string Ticker { get; set; } = string.Empty;
        public List<TradingviewImportItem> Items { get; set; } = [];
    }

    public class TradingviewImportItem
    {
        public int ID { get; set; }
        public double? ExitRR { get; set; }
        public DateTime Date { get; set; }
        public int Magic { get; set; }
        public string Comment { get; set; }
    }
}
