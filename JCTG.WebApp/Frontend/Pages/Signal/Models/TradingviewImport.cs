using JCTG.Models;

namespace JCTG.WebApp.Frontend.Pages.Signal.Models
{
    public class TradingviewImport
    {

        public long StrategyID { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public double TakeProfit { get; set; }
        public string OrderType { get; set; }
        public string TakeProfitAsString
        {
            get => TakeProfit.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    TakeProfit = newValue;
                }
            }
        }
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
