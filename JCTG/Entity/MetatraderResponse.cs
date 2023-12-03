using Newtonsoft.Json;

namespace JCTG
{
    public class MetatraderResponse
    {
        public MetatraderResponse() 
        {
            Action = "NONE";
        }
        //$"BUY,tp={tvAlert.TakeProfit - trade.Offset},sl={tvAlert.StopLoss - trade.Offset},magic='{tvAlert.Magic}'");
        public required string Action { get; set; }
        public double TakeProfit { get; set; }
        public double StopLoss { get; set; }
        public int Magic { get; set; }

    }
}
