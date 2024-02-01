using Newtonsoft.Json;

namespace JCTG.Client
{
    public class HistoricBarData
    {
        public HistoricBarData() 
        {
            this.BarData = [];
        }

        public List<BarData> BarData { get; set; }
    }
}
