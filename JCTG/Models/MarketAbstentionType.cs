namespace JCTG.Models
{
    public enum MarketAbstentionType
    {
        Unknown = -1,

        NoAccountInfoAvailable = 10,
        NoMarketDataAvailable = 11,
        NoSubscriptionForThisPairAndStrategy = 12,

        SpreadIsTooHigh = 20,
        CorrelatedPairFound = 21,
        MarketWillBeClosedWithinXMinutes = 22,
        AmountOfLotSizeShouldBeSmallerThenMaxLotsize = 23,
        RiskShouldBeAtLeastXTimesTheSpread = 24,

        ExceptionCalculatingLotSize = 30,
        ExceptionCalculatingStopLossPrice = 31,
        ExceptionCalculatingTakeProfitPrice = 32,
        ExceptionCalculatingEntryPrice = 33,
    }
}
