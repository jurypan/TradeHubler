using Newtonsoft.Json;

namespace JCTG
{
    public class MetatraderResponse
    {
        public MetatraderResponse() 
        {
            Action = "NONE";
            Comment = string.Empty;
        }
        //$"BUY,tp={tvAlert.TakeProfit - trade.Offset},sl={tvAlert.StopLoss - trade.Offset},comment='{tvAlert.Comment}'");
        public required string Action { get; set; }
        public double TakeProfit { get; set; }
        public double StopLoss { get; set; }
        public required string Comment { get; set; }

    }
}
