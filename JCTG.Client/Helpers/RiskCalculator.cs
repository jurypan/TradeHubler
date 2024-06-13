using JCTG.Models;

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
            if(stopLossDistanceInTicks > 0) 
            {
                var lotSize = Convert.ToDecimal(riskAmount) / (stopLossDistanceInTicks * tickValue);

                // Adjusting for lot step
                var remainder = lotSize % Convert.ToDecimal(lotStep);
                var adjustedLotSize = remainder == 0 ? lotSize : lotSize - remainder + (remainder >= Convert.ToDecimal(lotStep) / 2 ? Convert.ToDecimal(lotStep) : 0);
                adjustedLotSize = Math.Round(adjustedLotSize, 2);

                // Bounds checking
                return Math.Clamp(adjustedLotSize, Convert.ToDecimal(minLotSizeAllowed), Convert.ToDecimal(maxLotSizeAllowed));
            }
            return 0.0M;
        }


        public static decimal LotSize2(double startBalance, double accountBalance, decimal riskPercent, decimal openPrice, decimal stopLossPrice, decimal tickValue, decimal tickStep, decimal pointSize, double lotStep, double minLotSizeAllowed, double maxLotSizeAllowed, List<Risk>? riskData = null)
        {
            // Throw exception when negative balance
            if (accountBalance <= 0)
                return 0.0M;

            // Mocking ChooseClosestMultiplier for the unit test
            var dynamicRisk = ChooseClosestMultiplier(startBalance, accountBalance, riskData);

            // Calculate the initial lot size
            var riskAmount = Convert.ToDecimal(accountBalance) * ((riskPercent * dynamicRisk) / 100.0M);
            var stopLossDistance = Math.Abs(openPrice - stopLossPrice);

            if (stopLossDistance > 0)
            {
                // Calculate the point value
                var pointValue = tickValue * pointSize / tickStep;

                // Calculate the lot size
                var lotSize = riskAmount / (stopLossDistance * pointValue);

                // Adjusting for lot step
                lotSize = Math.Floor(lotSize / Convert.ToDecimal(lotStep)) * Convert.ToDecimal(lotStep);
                lotSize = Math.Round(lotSize, 2);

                // Bounds checking
                return Math.Clamp(lotSize, Convert.ToDecimal(minLotSizeAllowed), Convert.ToDecimal(maxLotSizeAllowed));
            }

            return 0.0M;
        }

        public static decimal RoundToNearestTickSize(decimal value, decimal tickSize, int digits)
        {
            var roundedValue = Math.Round(value / tickSize) * tickSize;
            return Math.Round(roundedValue, digits);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spread"></param>
        /// <param name="lotSize"></param>
        /// <param name="pointSize"></param>
        /// <param name="contractSize"></param>
        /// <returns></returns>
        public static double CostSpread(double spread, double lotSize, decimal pointSize, double contractSize)
        {
            //  SymbolInfoInteger(_Symbol, SYMBOL_SPREAD) * _Point * Lots * CONTRACT_SIZE;
            return spread * lotSize * Convert.ToDouble(pointSize) * contractSize;
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
        public static decimal SLForLong(decimal mtPrice, decimal mtSpread, int mtDigits, decimal signalPrice, decimal signalSL, SpreadExecType? spreadExecType = null, double pairSlMultiplier = 1.0)
        {
            // Calculate SL signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var slPrice = mtPrice - ((signalPrice - signalSL) * Convert.ToDecimal(pairSlMultiplier));

            // Round
            slPrice = Math.Round(slPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price
            if (spreadExecType.HasValue)
            {
                if(spreadExecType.Value == SpreadExecType.Add) 
                    return slPrice + mtSpread;
                else if(spreadExecType.Value == SpreadExecType.Subtract) 
                    return slPrice - mtSpread;
            }
            return slPrice;
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
        public static decimal SLForShort(decimal mtPrice, decimal mtSpread, int mtDigits, decimal signalPrice, decimal signalSL, SpreadExecType? spreadExecType = null, double pairSlMultiplier = 1.0)
        {
            // Calculate SL signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var slPrice = mtPrice + ((signalSL - signalPrice) * Convert.ToDecimal(pairSlMultiplier));

            // Round
            slPrice = Math.Round(slPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price
            if (spreadExecType.HasValue)
            {
                if (spreadExecType.Value == SpreadExecType.Add)
                    return slPrice - mtSpread;
                else if (spreadExecType.Value == SpreadExecType.Subtract)
                    return slPrice + mtSpread;
            }
            return slPrice;
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
        public static decimal TPForLong(decimal mtPrice, decimal mtSpread, int mtDigits, decimal signalPrice, decimal signalTP, SpreadExecType? spreadExecType = null)
        {
            var multiplier = 1.0M;

            // Calculate TP signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var tpPrice = mtPrice + ((signalTP - signalPrice) * multiplier);

            // Round
            tpPrice = Math.Round(tpPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price
            if (spreadExecType.HasValue)
            {
                if (spreadExecType.Value == SpreadExecType.Add)
                    return tpPrice + mtSpread;
                else if (spreadExecType.Value == SpreadExecType.Subtract)
                    return tpPrice - mtSpread;
            }
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
        public static decimal TPForShort(decimal mtPrice, decimal mtSpread, int mtDigits, decimal signalPrice, decimal signalTP, SpreadExecType? spreadExecType = null)
        {
            var multiplier = 1.0M;

            // Calculate TP signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var tpPrice = mtPrice - ((signalPrice - signalTP) * multiplier);

            // Round
            tpPrice = Math.Round(tpPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price
            if (spreadExecType.HasValue)
            {
                if (spreadExecType.Value == SpreadExecType.Add)
                    return tpPrice - mtSpread;
                else if (spreadExecType.Value == SpreadExecType.Subtract)
                    return tpPrice + mtSpread;
            }
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
