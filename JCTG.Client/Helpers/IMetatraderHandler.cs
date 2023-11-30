using Newtonsoft.Json.Linq;

namespace JCTG.Client
{
    public interface IMetatraderHandler
    {

        public void Start(Client metatrader);

        public void OnTick(Client metatrader, string symbol, double bid, double ask);

        public void OnBarData(Client metatrader, string symbol, string timeFrame, string time, double open, double high, double low, double close, int tickVolume);

        public void OnHistoricData(Client metatrader, string symbol, string timeFrame, JObject data);

        public void OnHistoricTrades(Client metatrader);

        public void OnMessage(Client metatrader, JObject message);

        public void OnOrderEvent(Client metatrader);

        public void OnTimer(Client metatrader);
    }
}