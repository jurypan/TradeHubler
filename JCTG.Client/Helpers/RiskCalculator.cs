using JCTG.Models;

namespace JCTG.Client
{
    public class RiskCalculator
    {
        private static void LogCalculation(Dictionary<string, string> logMessages, string key, object value) => logMessages[key] = value.ToString();



        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="accountBalance"></param>
        /// <param name="riskPercent"></param>
        /// <param name="openPrice"></param>
        /// <param name="stopLossPrice"></param>
        /// <param name="tickValue"></param>
        /// <param name="tickSize"></param>
        /// <param name="lotStep"></param>
        /// <param name="minLotSizeAllowed"></param>
        /// <param name="maxLotSizeAllowed"></param>
        /// <param name="spread"></param>
        /// <returns></returns>
        public static decimal LotSize(
            double startBalance,
            double accountBalance,
            decimal riskPercent,
            decimal openPrice,
            decimal stopLossPrice,
            decimal tickValue,
            decimal tickSize,
            double lotStep,
            double minLotSizeAllowed,
            double maxLotSizeAllowed,
            out Dictionary<string, string> logMessages,
            List<Risk>? riskData = null)
        {
            logMessages = [];

            // Log the initial input parameters
            LogCalculation(logMessages, "StartBalance", startBalance);
            LogCalculation(logMessages, "AccountBalance", accountBalance);
            LogCalculation(logMessages, "RiskPercent", riskPercent);
            LogCalculation(logMessages, "OpenPrice", openPrice);
            LogCalculation(logMessages, "StopLossPrice", stopLossPrice);
            LogCalculation(logMessages, "TickValue", tickValue);
            LogCalculation(logMessages, "TickSize", tickSize);
            LogCalculation(logMessages, "LotStep", lotStep);
            LogCalculation(logMessages, "MinLotSizeAllowed", minLotSizeAllowed);
            LogCalculation(logMessages, "MaxLotSizeAllowed", maxLotSizeAllowed);

            // Throw exception when negative balance
            if (accountBalance <= 0)
                return 0.0M;

            // Calculate risk percentage
            var dynamicRisk = ChooseClosestMultiplier(startBalance, accountBalance, riskData);
            LogCalculation(logMessages, "DynamicRisk", dynamicRisk);

            // Calculate the Risk Amount
            var riskAmount = Convert.ToDecimal(accountBalance) * ((riskPercent * dynamicRisk) / 100.0M);
            LogCalculation(logMessages, "RiskAmount", riskAmount);

            // Calculate the Stop Loss in Points
            var stopLossDistance = Math.Abs(openPrice - stopLossPrice);
            LogCalculation(logMessages, "StopLossDistance", stopLossDistance);

            // Convert Stop Loss to Ticks
            var stopLossDistanceInTicks = stopLossDistance / tickSize;
            LogCalculation(logMessages, "StopLossDistanceInTicks", stopLossDistanceInTicks);

            // Calculate the Loss per Lot
            var lossPerLot = stopLossDistanceInTicks * tickValue;
            LogCalculation(logMessages, "LossPerLot", lossPerLot);

            // Calculate the Lot Size
            var lotSize = riskAmount / lossPerLot;
            LogCalculation(logMessages, "InitialLotSize", lotSize);

            // Adjusting for lot step
            lotSize = RoundToNearestLotSize(lotSize, Convert.ToDecimal(lotStep));
            LogCalculation(logMessages, "RoundedLotSize", lotSize);

            // Bounds checking
            var finalLotSize = Math.Clamp(lotSize, Convert.ToDecimal(minLotSizeAllowed), Convert.ToDecimal(maxLotSizeAllowed));
            LogCalculation(logMessages, "FinalLotSize", finalLotSize);

            return finalLotSize;
        }



        public static decimal RoundToNearestLotSize(decimal value, decimal step)
        {
            int digits = BitConverter.GetBytes(decimal.GetBits(step)[3])[2];
            var roundedValue = Math.Round(value / step) * step;
            return Math.Round(roundedValue, digits);
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

        public static decimal RoundToNearestTickSize(decimal value, decimal step, int digits)
        {
            var roundedValue = Math.Round(value / step) * step;
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


    }
}
