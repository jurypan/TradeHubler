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
        /// <param name="tickSize"></param>
        /// <param name="lotStep"></param>
        /// <param name="minLotSizeAllowed"></param>
        /// <param name="maxLotSizeAllowed"></param>
        /// <param name="spread"></param>
        /// <returns></returns>
        public static decimal LotSize(double startBalance, double accountBalance, decimal riskPercent, decimal openPrice, decimal stopLossPrice, decimal tickValue, decimal tickSize, double lotStep, double minLotSizeAllowed, double maxLotSizeAllowed, List<Risk>? riskData = null)
        {
            // Throw exception when negative balance
            if (accountBalance <= 0)
                return 0.0M;

            // Calculate risk percentage
            var dynamicRisk = ChooseClosestMultiplier(startBalance, accountBalance, riskData);

            // Calculate the Risk Amount
            var riskAmount = Convert.ToDecimal(accountBalance) * ((riskPercent * dynamicRisk) / 100.0M);

            // Calculate the Stop Loss in Points
            var stopLossDistance = Math.Abs(openPrice - stopLossPrice);

            // Convert Stop Loss to Ticks
            var stopLossDistanceInTicks = stopLossDistance / tickSize;

            // Calculate the Loss per Lot
            var lossPerLot = stopLossDistanceInTicks * tickValue;

            // Calculate the Lot Size
            var lotSize = riskAmount / lossPerLot;

            // Adjusting for lot step
            lotSize = RoundToNearestTickSize(lotSize, Convert.ToDecimal(lotStep));

            // Bounds checking
            return Math.Clamp(lotSize, Convert.ToDecimal(minLotSizeAllowed), Convert.ToDecimal(maxLotSizeAllowed));
        }

        public static decimal RoundToNearestTickSize(decimal value, decimal step)
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
