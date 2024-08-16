using JCTG.Models;
using System.Web;

namespace JCTG.Command
{
    public class OnSendManualOrderCommand
    {

        // Required
        public int AccountID { get; set; } = 0;
        public long StrategyID { get; set; } = 0;
        public List<OnReceivingPairInstrument> ClientInstruments { get; set; } = [];
        public required string OrderType { get; set; } = "BUY";
        public int Magic { get; set; } = int.MinValue;
        public string MagicAsString
        {
            get => Magic.ToString();
            set
            {
                if (int.TryParse(value, out int newValue))
                {
                    Magic = newValue;
                }
            }
        }
        public double ProcentRiskOfBalance { get; set; } = 0.5;
        public string ProcentRiskOfBalanceAsString
        {
            get => ProcentRiskOfBalance.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    ProcentRiskOfBalance = newValue;
                }
            }
        }

        public OnReceivingManualMarketOrder MarketOrder { get; set; } = new OnReceivingManualMarketOrder();

        public string ToQueryString()
        {
            var queryParameters = new List<string>
            {
                $"AccountID={AccountID}",
                $"StrategyID={StrategyID}",
                $"OrderType={HttpUtility.UrlEncode(OrderType)}",
                $"Magic={Magic}",
                $"ProcentRiskOfBalance={ProcentRiskOfBalance}"
            };

            if (ClientInstruments != null && ClientInstruments.Any())
            {
                foreach (var instrument in ClientInstruments)
                {
                    queryParameters.Add($"ClientInstruments[]={HttpUtility.UrlEncode(instrument.ToQueryString())}");
                }
            }

            if (MarketOrder != null)
            {
                queryParameters.Add($"MarketOrder.EntryPrice={MarketOrder.EntryPrice}");
                queryParameters.Add($"MarketOrder.StopLossPrice={MarketOrder.StopLossPrice}");
                queryParameters.Add($"MarketOrder.RiskRewardRatio={MarketOrder.RiskRewardRatio}");
            }

            return string.Join(",", queryParameters);
        }

    }

    public class OnReceivingManualMarketOrder // BUY or SELL
    {
        public double EntryPrice { get; set; }
        public string EntryPriceAsString
        {
            get => EntryPrice.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    EntryPrice = newValue;
                }
            }
        }
        public double StopLossPrice { get; set; }
        public string StopLossPriceAsString
        {
            get => StopLossPrice.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    StopLossPrice = newValue;
                }
            }
        }
        public double RiskRewardRatio { get; set; }
        public string RiskRewardRatioAsString
        {
            get => RiskRewardRatio.ToString();
            set
            {
                if (double.TryParse(value, out double newValue))
                {
                    RiskRewardRatio = newValue;
                }
            }
        }
    }
    public class OnReceivingPairInstrument // BUY or SELL
    {
        public long ClientID { get; set; }
        public string ClientIDAsString
        {
            get => ClientID.ToString();
            set
            {
                if (long.TryParse(value, out long newValue))
                {
                    ClientID = newValue;
                }
            }
        }
        public string Instrument { get; set; }

        public string ToQueryString()
        {
            return $"ClientID={ClientID}&Instrument={HttpUtility.UrlEncode(Instrument)}";
        }
    }
}
