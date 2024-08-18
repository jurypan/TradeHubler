using JCTG.Models;

namespace JCTG.Client
{
    public class Calculator
    {
        private static void LogCalculation(Dictionary<string, string> logMessages, string key, object? value)
        {
            if (value == null)
            {
                logMessages[key] = string.Empty;
            }
            else
            {
                logMessages[key] = value.ToString();
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="accountBalance"></param>
        /// <param name="riskPercent"></param>
        /// <param name="entryBidPrice"></param>
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
            decimal entryBidPrice,
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

            // HttpCallOnLogEvent the initial input parameters
            LogCalculation(logMessages, "startBalance", startBalance);
            LogCalculation(logMessages, "accountBalance", accountBalance);
            LogCalculation(logMessages, "riskPercent", riskPercent);
            LogCalculation(logMessages, "entryBidPrice", entryBidPrice);
            LogCalculation(logMessages, "stopLossPrice", stopLossPrice);
            LogCalculation(logMessages, "tickValue", tickValue);
            LogCalculation(logMessages, "tickSize", tickSize);
            LogCalculation(logMessages, "lotStep", lotStep);
            LogCalculation(logMessages, "minLotSizeAllowed", minLotSizeAllowed);
            LogCalculation(logMessages, "maxLotSizeAllowed", maxLotSizeAllowed);

            // Throw exception when negative balance
            if (accountBalance <= 0)
                return 0.0M;

            // Calculate risk percentage
            var dynamicRisk = ChooseClosestMultiplier(startBalance, accountBalance, riskData);
            LogCalculation(logMessages, "dynamicRisk", dynamicRisk);

            // Calculate the RiskLong Amount
            var riskAmount = Convert.ToDecimal(accountBalance) * ((riskPercent * dynamicRisk) / 100.0M);
            LogCalculation(logMessages, "riskAmount", riskAmount);

            // Calculate the Stop Loss in Points
            var stopLossDistance = Math.Abs(entryBidPrice - stopLossPrice);
            LogCalculation(logMessages, "stopLossDistance", stopLossDistance);

            // Convert Stop Loss to Ticks
            var stopLossDistanceInTicks = stopLossDistance / tickSize;
            LogCalculation(logMessages, "stopLossDistanceInTicks", stopLossDistanceInTicks);

            // Calculate the Loss per Lot
            var lossPerLot = stopLossDistanceInTicks * tickValue;
            LogCalculation(logMessages, "lossPerLot", lossPerLot);

            // Calculate the Lot Size
            var lotSize = riskAmount / lossPerLot;
            LogCalculation(logMessages, "initialLotSize", lotSize);

            // Adjusting for lot tickSize
            lotSize = RoundToNearestLotSize(lotSize, Convert.ToDecimal(lotStep));
            LogCalculation(logMessages, "roundedLotSize", lotSize);

            // Bounds checking
            var finalLotSize = Math.Clamp(lotSize, Convert.ToDecimal(minLotSizeAllowed), Convert.ToDecimal(maxLotSizeAllowed));
            LogCalculation(logMessages, "finalLotSize", finalLotSize);

            return finalLotSize;
        }

        public static decimal? EntryBidPriceForShort(string entryExpression, List<BarData> bars, decimal spread, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // HttpCallOnLogEvent the initial input parameters
            LogCalculation(logMessages, "entryExpression", entryExpression);
            LogCalculation(logMessages, "spread", spread);


            // Get the entry entryBidPrice
            var bidPrice = DynamicEvaluator.EvaluateExpression(entryExpression, bars, out Dictionary<string, string> logMessages1);
            foreach (var log in logMessages1)
                LogCalculation(logMessages, log.Key, log.Value);
            LogCalculation(logMessages, "entryBidPrice", bidPrice);

            // do 0.0 check
            if (bidPrice.HasValue)
            {
                // A sell order is triggered when the bid price (the price at which you buy) reaches or exceeds the set (stop) price.
                // No extra code needed
            }

            return bidPrice;
        }

        public static decimal? EntryBidPriceForLong(string entryExpression, List<BarData> bars, decimal spread, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // HttpCallOnLogEvent the initial input parameters
            LogCalculation(logMessages, "entryExpression", entryExpression);
            LogCalculation(logMessages, "spread", spread);


            // Get the entry entryBidPrice
            var entryBidPrice = DynamicEvaluator.EvaluateExpression(entryExpression, bars, out Dictionary<string, string> logMessages1);
            foreach (var log in logMessages1)
                LogCalculation(logMessages, log.Key, log.Value);
            LogCalculation(logMessages, "initialEntryBidPrice", entryBidPrice);

            // do 0.0 check
            if (entryBidPrice.HasValue)
            {
                // A buy order is triggered when the ask price (the price at which you buy) reaches or exceeds the set (stop) price.
                entryBidPrice = CalculateSpreadExecForLong(entryBidPrice.Value, spread, SpreadExecType.Add);
                LogCalculation(logMessages, "entryBidPrice", entryBidPrice);
            }

            return entryBidPrice;
        }

        public static decimal StoplossToBreakEvenForShort(decimal entryBidPrice, decimal currentBidPrice, decimal spread, decimal tickSize, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            LogCalculation(logMessages, "currentBidPrice", currentBidPrice);

            var stoploss = StoplossForShort(
                        entryBidPrice: entryBidPrice,
                        risk: 0.0M,
                        slMultiplier: 1,
                        stopLossExpression: null,
                        bars: [],
                        spread: spread,
                        tickSize: tickSize,
                        out Dictionary<string, string> logMessages2);
            foreach (var log in logMessages2)
                LogCalculation(logMessages, log.Key, log.Value);

            // Extra check
            if (stoploss <= currentBidPrice)
            {
                LogCalculation(logMessages, "stoploss <= currentBidPrice", true);
               
                // Set SL to 1 tick above the current entryBidPrice
                stoploss = currentBidPrice + (2 * tickSize);
                stoploss = CalculateSpreadExecForShort(stoploss, spread, SpreadExecType.Subtract);
                LogCalculation(logMessages, "stoploss", stoploss);
            }

            return stoploss;
        }

        public static decimal StoplossToBreakEvenForLong(decimal entryBidPrice, decimal currentBidPrice, decimal spread, decimal tickSize, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            LogCalculation(logMessages, "currentBidPrice", currentBidPrice);

            // Get the Stop Loss entryBidPrice
            var stoploss = StoplossForLong(
                        entryBidPrice: entryBidPrice,
                        risk: 0.0M,
                        slMultiplier: 1,
                        stopLossExpression: null,
                        bars: [],
                        spread: spread,
                        tickSize: tickSize,
                         out Dictionary<string, string> logMessages2);
            foreach (var log in logMessages2)
                LogCalculation(logMessages, log.Key, log.Value);

            // Extra check
            if (stoploss >= currentBidPrice)
            {
                LogCalculation(logMessages, "stoploss >= currentBidPrice", true);
               
                // Set SL to 1 tick above the current entryBidPrice
                stoploss = currentBidPrice - (2 * tickSize);
                LogCalculation(logMessages, "stoploss", stoploss);
            }

            return stoploss;
        }

        public static decimal StoplossForShort(decimal entryBidPrice, decimal risk, double slMultiplier, string? stopLossExpression, List<BarData> bars, decimal spread, decimal tickSize, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            LogCalculation(logMessages, "entryBidPrice", entryBidPrice);
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "slMultiplier", slMultiplier);
            LogCalculation(logMessages, "stopLossExpression", stopLossExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "tickSize", tickSize);

            // Get the Stop Loss entryBidPrice
            var stoploss = entryBidPrice + (risk * Convert.ToDecimal(slMultiplier));
            LogCalculation(logMessages, "stoploss", stoploss);

            // If SL expression is enabled
            if (!string.IsNullOrEmpty(stopLossExpression))
            {
                LogCalculation(logMessages, "stopLossExpression.HasValue", true);

                // Get the SL entryBidPrice
                var slExpr = DynamicEvaluator.EvaluateExpression(stopLossExpression, bars, out Dictionary<string, string> logMessages2);
                foreach (var log in logMessages2)
                    LogCalculation(logMessages, log.Key, log.Value);

                // Do null reference check
                if (slExpr.HasValue)
                {
                    LogCalculation(logMessages, "bar found", true);

                    // Overwrite the stop loss entryBidPrice
                    stoploss = slExpr.Value;
                    LogCalculation(logMessages, "stoploss", stoploss);

                    // Override thr isk
                    risk = Math.Abs(stoploss - entryBidPrice);
                    LogCalculation(logMessages, "risk", risk);

                    // Add SL Multiplier (Price + (risk * SlMultiplier)
                    stoploss = entryBidPrice + (risk * Convert.ToDecimal(slMultiplier));
                    LogCalculation(logMessages, "stoploss", stoploss);
                }
            }

            // In a short position, the stop loss order is triggered when the ask price (the price at which you need to buy to close your position) reaches or exceeds the stop loss level. 
            stoploss = CalculateSpreadExecForShort(stoploss, spread, SpreadExecType.Subtract);
            LogCalculation(logMessages, "stoploss", stoploss);

            // Extra check
            if (stoploss <= entryBidPrice)
            {
                LogCalculation(logMessages, "stoploss <= entryBidPrice", true);

                // Set SL to 1 tick above the current entryBidPrice
                stoploss = entryBidPrice + (2 * tickSize);
                LogCalculation(logMessages, "stoploss", stoploss);
            }

            return stoploss;
        }

        public static decimal StoplossForLong(decimal entryBidPrice, decimal risk, double slMultiplier, string? stopLossExpression, List<BarData> bars, decimal spread, decimal tickSize, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // HttpCallOnLogEvent the initial input parameters
            LogCalculation(logMessages, "entryBidPrice", entryBidPrice);
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "slMultiplier", slMultiplier);
            LogCalculation(logMessages, "stopLossExpression", stopLossExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "tickSize", tickSize);

            // Get the Stop Loss entryBidPrice
            var stoploss = entryBidPrice - (risk * Convert.ToDecimal(slMultiplier));
            LogCalculation(logMessages, "stoploss", stoploss);

            // If SL expression is enabled
            if (!string.IsNullOrEmpty(stopLossExpression))
            {
                LogCalculation(logMessages, "stopLossExpression.HasValue", true);

                // Get the SL entryBidPrice
                var slBidPrice = DynamicEvaluator.EvaluateExpression(stopLossExpression, bars, out Dictionary<string, string> logMessages2);
                foreach (var log in logMessages2)
                    LogCalculation(logMessages, log.Key, log.Value);

                // Do null reference check
                if (slBidPrice.HasValue)
                {
                    LogCalculation(logMessages, "bar found", true);

                    // Overwrite the stop loss entryBidPrice
                    stoploss = slBidPrice.Value;
                    LogCalculation(logMessages, "stoploss", stoploss);

                    // Override thr isk
                    risk = Math.Abs(entryBidPrice - stoploss);
                    LogCalculation(logMessages, "risk", risk);

                    // Add SL Multiplier (Price - (risk * SlMultiplier)
                    stoploss = entryBidPrice - (risk * Convert.ToDecimal(slMultiplier));
                    LogCalculation(logMessages, "stoploss", stoploss);
                }
            }

            // In a long position, the stop loss order is triggered when the bid price (the price at which you need to sell to close your position) reaches or exceeds the stop loss level. 
            // No extra code needed
            LogCalculation(logMessages, "stoploss", stoploss);

            // Extra check
            if (stoploss >= entryBidPrice)
            {
                LogCalculation(logMessages, "stoploss >= entryBidPrice", true);

                // Set SL to 1 tick above the current entryBidPrice
                stoploss = entryBidPrice - (2 * tickSize);
                LogCalculation(logMessages, "stoploss", stoploss);
            }

            return stoploss;
        }

        public static decimal TakeProfitForShort(decimal entryBidPrice, decimal risk, double slMultiplier, string? stopLossExpression, List<BarData> bars, decimal spread, decimal riskRewardRatio, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // HttpCallOnLogEvent the initial input parameters
            LogCalculation(logMessages, "entryBidPrice", entryBidPrice);
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "slMultiplier", slMultiplier);
            LogCalculation(logMessages, "stopLossExpression", stopLossExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "riskRewardRatio", riskRewardRatio);

            // Get the Stop Loss entryBidPrice
            var stoploss = entryBidPrice + (risk * Convert.ToDecimal(slMultiplier));

            // If SL expression is enabled
            if (!string.IsNullOrEmpty(stopLossExpression))
            {
                // Get the SL entryBidPrice
                var slExpr = DynamicEvaluator.EvaluateExpression(stopLossExpression, bars, out Dictionary<string, string> logMessages2);

                // Do null reference check
                if (slExpr.HasValue)
                {
                    // Overwrite the stop loss entryBidPrice
                    stoploss = slExpr.Value;

                    // Override the risk
                    risk = Math.Abs(stoploss - entryBidPrice);
                    LogCalculation(logMessages, "risk", risk);
                }
            }

            // Get the Take Profit Price
            var takeprofit = entryBidPrice - (risk * riskRewardRatio);
            LogCalculation(logMessages, "takeprofit", takeprofit);

            // In a short position, the take profit order is triggered when the ask price (the price at which you need to buy to close your position) reaches or exceeds the take profit level. 
            takeprofit = CalculateSpreadExecForShort(takeprofit, spread, SpreadExecType.Subtract);
            LogCalculation(logMessages, "takeprofit", takeprofit);

            return takeprofit;
        }

        public static decimal TakeProfitForLong(decimal entryBidPrice, decimal risk, double slMultiplier, string? stopLossExpression, List<BarData> bars, decimal spread, decimal riskRewardRatio, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // HttpCallOnLogEvent the initial input parameters
            LogCalculation(logMessages, "entryBidPrice", entryBidPrice);
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "slMultiplier", slMultiplier);
            LogCalculation(logMessages, "stopLossExpression", stopLossExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "riskRewardRatio", riskRewardRatio);

            // Get the Stop Loss entryBidPrice
            var stoploss = entryBidPrice - (risk * Convert.ToDecimal(slMultiplier));

            // If SL expression is enabled
            if (!string.IsNullOrEmpty(stopLossExpression))
            {
                // Get the SL entryBidPrice
                var slExpr = DynamicEvaluator.EvaluateExpression(stopLossExpression, bars, out Dictionary<string, string> logMessages2);

                // Do null reference check
                if (slExpr.HasValue)
                {
                    // Overwrite the stop loss entryBidPrice
                    stoploss = slExpr.Value;

                    // Override the risk
                    risk = Math.Abs(entryBidPrice - stoploss);
                    LogCalculation(logMessages, "risk", risk);
                }
            }

            // Get the Take Profit Price
            var takeprofit = entryBidPrice + (risk * riskRewardRatio);
            LogCalculation(logMessages, "takeprofit", takeprofit);

            // In a long position, the take profit order is triggered when the bid price (the price at which you need to sell to close your position) reaches or exceeds the take profit level. 
            // No extra code needed

            return takeprofit;
        }



        public static string GenerateComment(decimal signalId, decimal price, decimal stopLoss, long strategyId, decimal spread)
        {
            return string.Format($"{signalId}/{price}/{stopLoss}/{Convert.ToInt32(strategyId)}/{spread}");
        }

        public static long? GetSignalIdFromComment(string? comment)
        {
            string[] components = comment != null ? comment.Split('/') : [];
            long signalID = 0;
            if (components != null && components.Length == 5)
            {
                _ = long.TryParse(components[0], out signalID);
            }
            return signalID == 0 ? null : signalID;
        }

        public static long? GetStrategyIdFromComment(string? comment)
        {
            string[] components = comment != null ? comment.Split('/') : [];
            long strategyID = 0;
            if (components != null && components.Length == 5)
            {
                _ = long.TryParse(components[3].Replace("[stopLossBidPrice]", string.Empty).Replace("[tp]", string.Empty), out strategyID);
            }
            return strategyID == 0 ? null : strategyID;
        }

        public static decimal? GetEntryPriceFromComment(string? comment)
        {
            string[] components = comment != null ? comment.Split('/') : [];
            decimal retour = 0M;
            if (components != null && components.Length == 5)
            {
                _ = decimal.TryParse(components[1], out retour);
            }
            return retour == 0 ? null : retour;
        }

        public static decimal? GetStoplossFromComment(string? comment)
        {
            string[] components = comment != null ? comment.Split('/') : [];
            decimal retour = 0M;
            if (components != null && components.Length == 5)
            {
                _ = decimal.TryParse(components[2], out retour);
            }
            return retour == 0 ? null : retour;
        }

        public static decimal? GetSpreadFromComment(string? comment)
        {
            string[] components = comment != null ? comment.Split('/') : [];
            decimal retour = 0M;
            if (components != null && components.Length == 5)
            {
                _ = decimal.TryParse(components[4], out retour);
            }
            return retour == 0 ? null : retour;
        }




        public static decimal RoundToNearestLotSize(decimal value, decimal tickSize)
        {
            int digits = BitConverter.GetBytes(decimal.GetBits(tickSize)[3])[2];
            var roundedValue = Math.Round(value / tickSize) * tickSize;
            return Math.Round(roundedValue, digits);
        }

        public static decimal ChooseClosestMultiplier(double startBalance, double accountBalance, List<Risk>? riskData = null)
        {
            // Do null reference check
            if (startBalance <= 0 || accountBalance <= 0 || riskData == null || riskData.Count == 0)
                return 1M;

            // Calculate the percentage change
            double percentageChange = ((accountBalance - startBalance) / startBalance) * 100;

            // Find the closest risk percentage
            var closestRisk = riskData.OrderBy(risk => Math.Abs(risk.Procent - percentageChange)).First();

            return Convert.ToDecimal(closestRisk.Multiplier);
        }

        public static decimal RoundToNearestTickSize(decimal value, decimal tickSize, int digits)
        {
            if (value == 0) return 0.0M;
            var roundedValue = Math.Round(Math.Abs(value) / tickSize) * tickSize;
            return Math.Round(roundedValue, digits);
        }

        public static decimal CalculateSpread(decimal ask, decimal bid, decimal tickSize, int digits)
        {
            decimal spread = Math.Abs(ask - bid);
            return RoundToNearestTickSize(spread, tickSize, digits);
        }

        public static decimal CalculateCostSpread(decimal spread, double lotSize, decimal tickSize, int digits, decimal tickValue)
        {
            return (spread / tickSize) * tickValue * Convert.ToDecimal(lotSize);
        }

        private static decimal CalculateSpreadExecForShort(decimal price, decimal spread, SpreadExecType spreadExecType)
        {
            // Ensure spread is non-negative to avoid unexpected behavior
            if (spread < 0)
                throw new ArgumentException("Spread cannot be negative");

            switch (spreadExecType)
            {
                case SpreadExecType.Add:
                    price -= spread;
                    break;
                case SpreadExecType.Subtract:
                    price += spread;
                    break;
                case SpreadExecType.TwiceAdd:
                    price -= (2 * spread);
                    break;
                case SpreadExecType.TwiceSubtract:
                    price += (2 * spread);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(spreadExecType), spreadExecType, null);
            }

            return price;
        }

        private static decimal CalculateSpreadExecForLong(decimal price, decimal spread, SpreadExecType spreadExecType)
        {
            // Ensure spread is non-negative to avoid unexpected behavior
            if (spread < 0)
                throw new ArgumentException("Spread cannot be negative");

            switch (spreadExecType)
            {
                case SpreadExecType.Add:
                    price += spread;
                    break;
                case SpreadExecType.Subtract:
                    price -= spread;
                    break;
                case SpreadExecType.TwiceAdd:
                    price += (2 * spread);
                    break;
                case SpreadExecType.TwiceSubtract:
                    price -= (2 * spread);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(spreadExecType), spreadExecType, null);
            }

            return price;
        }





    }
}
