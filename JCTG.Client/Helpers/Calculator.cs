namespace JCTG.Client
{
    public class RiskCalculator
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="accountBalance"></param>
        /// <param name="riskPercent"></param>
        /// <param name="openPrice"></param>
        /// <param name="stopLossPrice"></param>
        /// <param name="tickValue"></param>
        /// <param name="tickStep"></param>
        /// <param name="lotStep"></param>
        /// <param name="minLotSizeAllowed"></param>
        /// <param name="maxLotSizeAllowed"></param>
        /// <param name="spread"></param>
        /// <returns></returns>
        public static decimal LotSize(double startBalance, double accountBalance, decimal riskPercent, decimal openPrice, decimal stopLossPrice, decimal tickValue, decimal tickStep, double lotStep, double minLotSizeAllowed, double maxLotSizeAllowed, List<Risk>? riskData = null)
        {
            // Throw exception when negative balance
            if (accountBalance <= 0)
                return 0.0M;


            // Calculate risk percentage
            var dynamicRisk = ChooseClosestMultiplier(startBalance, accountBalance, riskData);

            // Calculate the initial lot size
            var riskAmount = Convert.ToDecimal(accountBalance) * ((riskPercent * dynamicRisk) / 100.0M);
            var stopLossDistance = Math.Abs(openPrice - stopLossPrice);
            var stopLossDistanceInTicks = stopLossDistance / tickStep;
            var lotSize = Convert.ToDecimal(riskAmount) / (stopLossDistanceInTicks * tickValue);



            // Adjusting for lot step
            var remainder = lotSize % Convert.ToDecimal(lotStep);
            var adjustedLotSize = remainder == 0 ? lotSize : lotSize - remainder + (remainder >= Convert.ToDecimal(lotStep) / 2 ? Convert.ToDecimal(lotStep) : 0);
            adjustedLotSize = Math.Round(adjustedLotSize, 2);

            // Bounds checking
            return Math.Clamp(adjustedLotSize, Convert.ToDecimal(minLotSizeAllowed), Convert.ToDecimal(maxLotSizeAllowed));
        }

        /// <summary>
        /// Calculate the stop loss signalEntryPrice for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK signalEntryPrice</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="mtSpread">Spread value</param>
        /// <param name="mtDigits">Tick size</param>
        /// <param name="signalPrice">Signal ENTRY signalEntryPrice</param>
        /// <param name="signalSL">Signal SL signalEntryPrice</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Stop loss signalEntryPrice</returns>
        public static decimal SLForLong(decimal mtPrice, decimal mtSpread, int mtDigits, decimal signalPrice, decimal signalSL, double pairSlMultiplier = 1.0)
        {
            // Calculate SL signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var slPrice = mtPrice - ((signalPrice - signalSL) * Convert.ToDecimal(pairSlMultiplier));

            // Round
            slPrice = Math.Round(slPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price minus spread
            return slPrice - mtSpread;
        }

        /// <summary>
        /// Calculate the stop loss signalEntryPrice for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK signalEntryPrice</param>
        /// <param name="mtATR">MetaTrader ATR5M</param>
        /// <param name="mtSpread">The spread value</param>
        /// <param name="signalPrice">Signal ENTRY signalEntryPrice</param>
        /// <param name="signalSL">Signal SL signalEntryPrice</param>
        /// <param name="signalATR">Signal ATR5M</param>
        /// <returns>Stop loss signalEntryPrice</returns>
        public static decimal SLForShort(decimal mtPrice, decimal mtSpread, int mtDigits, decimal signalPrice, decimal signalSL, double pairSlMultiplier = 1.0)
        {
            // Calculate SL signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var slPrice = mtPrice + ((signalSL - signalPrice) * Convert.ToDecimal(pairSlMultiplier));

            // Round
            slPrice = Math.Round(slPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price plus spread
            return slPrice + mtSpread;
        }

        /// <summary>
        /// Calculate the take profit signalEntryPrice for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK signalEntryPrice</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="mtTickSize">Tick size</param>
        /// <param name="signalPrice">Signal ENTRY signalEntryPrice</param>
        /// <param name="signalTP">Signal TP signalEntryPrice</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Take profit signalEntryPrice</returns>
        public static decimal TPForLong(decimal mtPrice, int mtDigits, decimal signalPrice, decimal signalTP)
        {
            var multiplier = 1.0M;

            // Calculate TP signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var tpPrice = mtPrice + ((signalTP - signalPrice) * multiplier);

            // Round
            tpPrice = Math.Round(tpPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price minus spread
            return tpPrice;
        }

        /// <summary>
        /// Calculate the take profit signalEntryPrice for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK signalEntryPrice</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="signalPrice">Signal ENTRY signalEntryPrice</param>
        /// <param name="signalTP">Signal TP signalEntryPrice</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Take profit signalEntryPrice</returns>
        public static decimal TPForShort(decimal mtPrice, int mtDigits, decimal signalPrice, decimal signalTP)
        {
            var multiplier = 1.0M;

            // Calculate TP signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var tpPrice = mtPrice - ((signalPrice - signalTP) * multiplier);

            // Round
            tpPrice = Math.Round(tpPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price plus spread
            return tpPrice;
        }

        public static decimal ChooseClosestMultiplier(double startBalance, double accountBalance, List<Risk>? riskData = null)
        {
            // Do null reference check
            if (startBalance <= 0 || accountBalance <= 0 || riskData == null)
                return 1M;

            // Calculate the percentage change
            double percentageChange = ((accountBalance - startBalance) / startBalance) * 100;

            // Find the closest risk percentage
            var closestRisk = riskData.OrderBy(risk => Math.Abs(risk.Procent - percentageChange)).First();

            return Convert.ToDecimal(closestRisk.Multiplier);
        }
    }
}
