using Newtonsoft.Json;

namespace JCTG.Models
{
    public class HistoricBarData
    {
        public HistoricBarData()
        {
            BarData = [];
        }

        public List<BarData> BarData { get; set; }
    }
}
