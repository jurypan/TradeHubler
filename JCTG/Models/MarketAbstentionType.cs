using System.ComponentModel;

namespace JCTG.Models
{
    public enum MarketAbstentionType
    {
        Unknown = -1,

        [Description("No account info available")]
        NoAccountInfoAvailable = 10,

        [Description("No market data available")]
        NoMarketDataAvailable = 11,

        [Description("No subscription for this pair and strategy")]
        NoSubscriptionForThisPairAndStrategy = 12,

        [Description("Spread is too high")]
        SpreadIsTooHigh = 20,

        [Description("Correlated pair found")]
        CorrelatedPairFound = 21,

        [Description("Market will be closed within X minutes")]
        MarketWillBeClosedWithinXMinutes = 22,

        [Description("Max lot size exceeded")]
        AmountOfLotSizeShouldBeSmallerThenMaxLotsize = 23,

        [Description("RiskLong should be at least X times the spread")]
        RiskShouldBeAtLeastXTimesTheSpread = 24,

        [Description("Exception calculating lot size")]
        ExceptionCalculatingLotSize = 30,

        [Description("Exception calculating stop loss price")]
        ExceptionCalculatingStopLossPrice = 31,

        [Description("Exception calculating take profit price")]
        ExceptionCalculatingTakeProfitPrice = 32,

        [Description("Exception calculating entry price")]
        ExceptionCalculatingEntryPrice = 33,

        [Description("Metatrader order error")]
        MetatraderOpenOrderError = 40,
    }
}
