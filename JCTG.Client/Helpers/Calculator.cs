using JCTG.Command;
using JCTG.Models;
using System.Diagnostics;

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
        /// <param name="entryPrice"></param>
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
            decimal entryPrice,
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
            LogCalculation(logMessages, "OpenPrice", entryPrice);
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

            // Calculate the RiskLong Amount
            var riskAmount = Convert.ToDecimal(accountBalance) * ((riskPercent * dynamicRisk) / 100.0M);
            LogCalculation(logMessages, "RiskAmount", riskAmount);

            // Calculate the Stop Loss in Points
            var stopLossDistance = Math.Abs(entryPrice - stopLossPrice);
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

        public static decimal? EntryPriceForShort(decimal risk, string entryExpression, List<BarData> bars, decimal spread, SpreadExecType? spreadExecType, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // Log the initial input parameters
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "entryExpression", entryExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "spreadExecType", spreadExecType);


            // Get the entry entryPrice
            var price = DynamicEvaluator.EvaluateExpression(entryExpression, bars, out Dictionary<string, string> logMessages1);
            foreach (var log in logMessages1)
                LogCalculation(logMessages, log.Key, log.Value);
            LogCalculation(logMessages, "entryPrice", price);

            // do 0.0 check
            if (price.HasValue)
            {
                LogCalculation(logMessages, "entryPrice.HasValue", true);
                LogCalculation(logMessages, "entryPrice.Value", price);

                // Add the spread options
                if (spreadExecType.HasValue)
                {
                    LogCalculation(logMessages, "spreadExecType.HasValue", true);
                    LogCalculation(logMessages, "spreadExecType.Value", spreadExecType);

                    // Calculate EP
                    price = CalculateSpreadExecForShort(price.Value, spread, spreadExecType.Value);
                    LogCalculation(logMessages, "entryPrice", price);
                }
            }

            return price;
        }

        public static decimal? EntryPriceForLong(decimal risk, string entryExpression, List<BarData> bars, decimal spread, SpreadExecType? spreadExecType, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // Log the initial input parameters
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "entryExpression", entryExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "spreadExecType", spreadExecType);


            // Get the entry entryPrice
            var price = DynamicEvaluator.EvaluateExpression(entryExpression, bars, out Dictionary<string, string> logMessages1);
            foreach (var log in logMessages1)
                LogCalculation(logMessages, log.Key, log.Value);
            LogCalculation(logMessages, "entryPrice", price);

            // do 0.0 check
            if (price.HasValue)
            {
                LogCalculation(logMessages, "entryPrice.HasValue", true);
                LogCalculation(logMessages, "entryPrice.Value", price);

                // Add the spread options
                if (spreadExecType.HasValue)
                {
                    LogCalculation(logMessages, "spreadExecType.HasValue", true);
                    LogCalculation(logMessages, "spreadExecType.Value", spreadExecType);

                    // Calculate EP
                    price = CalculateSpreadExecForLong(price.Value, spread, spreadExecType.Value);
                    LogCalculation(logMessages, "entryPrice", price);
                }
            }

            return price;
        }

        public static decimal StoplossToBreakEvenForShort(decimal entryPrice, decimal bidPrice, decimal spread, SpreadExecType? spreadExecType, decimal tickSize, out Dictionary<string, string> logMessages)
        {
            return StoplossForShort(
                        entryPrice: entryPrice,
                        bidPrice: bidPrice,
                        risk: 0.0M,
                        slMultiplier: 1,
                        stopLossExpression: null,
                        bars: [],
                        spread: spread,
                        spreadExecType: spreadExecType,
                        tickSize: tickSize,
                        out logMessages);
        }

        public static decimal StoplossToBreakEvenForLong(decimal entryPrice, decimal askPrice, decimal spread, SpreadExecType? spreadExecType, decimal tickSize, out Dictionary<string, string> logMessages)
        {
            return StoplossForLong(
                        entryPrice: entryPrice,
                        askPrice: askPrice,
                        risk: 0.0M,
                        slMultiplier: 1,
                        stopLossExpression: null,
                        bars: [],
                        spread: spread,
                        spreadExecType: spreadExecType,
                        tickSize: tickSize,
                        out logMessages);
        }

        public static decimal StoplossForShort(decimal entryPrice, decimal bidPrice, decimal risk, double slMultiplier, string? stopLossExpression, List<BarData> bars, decimal spread, SpreadExecType? spreadExecType, decimal tickSize, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // Log the initial input parameters
            LogCalculation(logMessages, "entryPrice", entryPrice);
            LogCalculation(logMessages, "askPrice", bidPrice);
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "slMultiplier", slMultiplier);
            LogCalculation(logMessages, "stopLossExpression", stopLossExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "spreadExecType", spreadExecType);
            LogCalculation(logMessages, "tickSize", tickSize);

            // Get the Stop Loss entryPrice
            var stoploss = entryPrice + (risk * Convert.ToDecimal(slMultiplier));
            LogCalculation(logMessages, "stoploss", stoploss);

            // If SL expression is enabled
            if (!string.IsNullOrEmpty(stopLossExpression))
            {
                LogCalculation(logMessages, "stopLossExpression.HasValue", true);

                // Get the SL entryPrice
                var slExpr = DynamicEvaluator.EvaluateExpression(stopLossExpression, bars, out Dictionary<string, string> logMessages2);
                foreach (var log in logMessages2)
                    LogCalculation(logMessages, log.Key, log.Value);

                // Do null reference check
                if (slExpr.HasValue)
                {
                    LogCalculation(logMessages, "bar found", true);

                    // Overwrite the stop loss entryPrice
                    stoploss = slExpr.Value;
                    LogCalculation(logMessages, "stoploss", stoploss);

                    // Override thr isk
                    risk = Math.Abs(stoploss - entryPrice);
                    LogCalculation(logMessages, "risk", risk);

                    // Add SL Multiplier (Price + (risk * SlMultiplier)
                    stoploss = entryPrice + (risk * Convert.ToDecimal(slMultiplier));
                    LogCalculation(logMessages, "stoploss", stoploss);
                }
            }

            // Add the spread options
            if (spreadExecType.HasValue)
            {
                LogCalculation(logMessages, "spreadExecType.HasValue", true);

                // Calculate SL
                stoploss = CalculateSpreadExecForShort(stoploss, spread, spreadExecType.Value);
                LogCalculation(logMessages, "stoploss", stoploss);
            }

            // Extra check
            if (stoploss <= bidPrice)
            {
                LogCalculation(logMessages, "stoploss <= askPrice", true);

                // Set SL to 1 tick above the current entryPrice
                stoploss = bidPrice + (2 * tickSize);
                LogCalculation(logMessages, "stoploss", stoploss);
            }

            return stoploss;
        }

        public static decimal StoplossForLong(decimal entryPrice, decimal askPrice, decimal risk, double slMultiplier, string? stopLossExpression, List<BarData> bars, decimal spread, SpreadExecType? spreadExecType, decimal tickSize, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // Log the initial input parameters
            LogCalculation(logMessages, "entryPrice", entryPrice);
            LogCalculation(logMessages, "askPrice", askPrice);
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "slMultiplier", slMultiplier);
            LogCalculation(logMessages, "stopLossExpression", stopLossExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "spreadExecType", spreadExecType);
            LogCalculation(logMessages, "tickSize", tickSize);

            // Get the Stop Loss entryPrice
            var stoploss = entryPrice - (risk * Convert.ToDecimal(slMultiplier));
            LogCalculation(logMessages, "stoploss", stoploss);

            // If SL expression is enabled
            if (!string.IsNullOrEmpty(stopLossExpression))
            {
                LogCalculation(logMessages, "stopLossExpression.HasValue", true);

                // Get the SL entryPrice
                var slExpr = DynamicEvaluator.EvaluateExpression(stopLossExpression, bars, out Dictionary<string, string> logMessages2);
                foreach (var log in logMessages2)
                    LogCalculation(logMessages, log.Key, log.Value);

                // Do null reference check
                if (slExpr.HasValue)
                {
                    LogCalculation(logMessages, "bar found", true);

                    // Overwrite the stop loss entryPrice
                    stoploss = slExpr.Value;
                    LogCalculation(logMessages, "stoploss", stoploss);

                    // Override thr isk
                    risk = Math.Abs(entryPrice - stoploss);
                    LogCalculation(logMessages, "risk", risk);

                    // Add SL Multiplier (Price - (risk * SlMultiplier)
                    stoploss = entryPrice - (risk * Convert.ToDecimal(slMultiplier));
                    LogCalculation(logMessages, "stoploss", stoploss);
                }
            }

            // Add the spread options
            if (spreadExecType.HasValue)
            {
                LogCalculation(logMessages, "spreadExecType.HasValue", true);

                // Calculate SL
                stoploss = CalculateSpreadExecForLong(stoploss, spread, spreadExecType.Value);
                LogCalculation(logMessages, "stoploss", stoploss);
            }

            // Extra check
            if (stoploss >= askPrice)
            {
                LogCalculation(logMessages, "stoploss >= askPrice", true);

                // Set SL to 1 tick above the current entryPrice
                stoploss = askPrice - (2 * tickSize);
                LogCalculation(logMessages, "stoploss", stoploss);
            }

            return stoploss;
        }

        public static decimal TakeProfitForShort(decimal entryPrice, decimal risk, double slMultiplier, string? stopLossExpression, List<BarData> bars, decimal spread, SpreadExecType? spreadExecType, decimal riskRewardRatio, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // Log the initial input parameters
            LogCalculation(logMessages, "entryPrice", entryPrice);
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "slMultiplier", slMultiplier);
            LogCalculation(logMessages, "stopLossExpression", stopLossExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "spreadExecType", spreadExecType);
            LogCalculation(logMessages, "riskRewardRatio", riskRewardRatio);

            // Get the Stop Loss entryPrice
            var stoploss = entryPrice + (risk * Convert.ToDecimal(slMultiplier));

            // If SL expression is enabled
            if (!string.IsNullOrEmpty(stopLossExpression))
            {
                // Get the SL entryPrice
                var slExpr = DynamicEvaluator.EvaluateExpression(stopLossExpression, bars, out Dictionary<string, string> logMessages2);

                // Do null reference check
                if (slExpr.HasValue)
                {
                    // Overwrite the stop loss entryPrice
                    stoploss = slExpr.Value;

                    // Override the risk
                    risk = Math.Abs(stoploss - entryPrice);
                    LogCalculation(logMessages, "risk", risk);
                }
            }

            // Get the Take Profit Price
            var takeprofit = entryPrice - (risk * riskRewardRatio);
            LogCalculation(logMessages, "takeprofit", takeprofit);

            // Spread exec type
            if (spreadExecType.HasValue)
            {
                LogCalculation(logMessages, "spreadExecType.HasValue", true);

                // Calculate TP
                takeprofit = CalculateSpreadExecForShort(takeprofit, spread, spreadExecType.Value);
                LogCalculation(logMessages, "takeprofit", takeprofit);
            }

            return takeprofit;
        }

        public static decimal TakeProfitForLong(decimal entryPrice, decimal risk, double slMultiplier, string? stopLossExpression, List<BarData> bars, decimal spread, SpreadExecType? spreadExecType, decimal riskRewardRatio, out Dictionary<string, string> logMessages)
        {
            logMessages = [];

            // Log the initial input parameters
            LogCalculation(logMessages, "entryPrice", entryPrice);
            LogCalculation(logMessages, "risk", risk);
            LogCalculation(logMessages, "slMultiplier", slMultiplier);
            LogCalculation(logMessages, "stopLossExpression", stopLossExpression);
            LogCalculation(logMessages, "spread", spread);
            LogCalculation(logMessages, "spreadExecType", spreadExecType);
            LogCalculation(logMessages, "riskRewardRatio", riskRewardRatio);

            // Get the Stop Loss entryPrice
            var stoploss = entryPrice - (risk * Convert.ToDecimal(slMultiplier));

            // If SL expression is enabled
            if (!string.IsNullOrEmpty(stopLossExpression))
            {
                // Get the SL entryPrice
                var slExpr = DynamicEvaluator.EvaluateExpression(stopLossExpression, bars, out Dictionary<string, string> logMessages2);

                // Do null reference check
                if (slExpr.HasValue)
                {
                    // Overwrite the stop loss entryPrice
                    stoploss = slExpr.Value;

                    // Override the risk
                    risk = Math.Abs(entryPrice - stoploss);
                    LogCalculation(logMessages, "risk", risk);
                }
            }

            // Get the Take Profit Price
            var takeprofit = entryPrice + (risk * riskRewardRatio);
            LogCalculation(logMessages, "takeprofit", takeprofit);

            // Spread exec type
            if (spreadExecType.HasValue)
            {
                LogCalculation(logMessages, "spreadExecType.HasValue", true);

                // Calculate TP
                takeprofit = CalculateSpreadExecForLong(takeprofit, spread, spreadExecType.Value);
                LogCalculation(logMessages, "takeprofit", takeprofit);
            }

            return takeprofit;
        }



        public static string GenerateComment(decimal signalId, decimal price, decimal stopLoss, long strategyId, decimal spread)
        {
            return string.Format($"{signalId}/{price}/{stopLoss}/{Convert.ToInt32(strategyId)}/{spread}");
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
            if (startBalance <= 0 || accountBalance <= 0 || riskData == null || riskData.Count == 0)
                return 1M;

            // Calculate the percentage change
            double percentageChange = ((accountBalance - startBalance) / startBalance) * 100;

            // Find the closest risk percentage
            var closestRisk = riskData.OrderBy(risk => Math.Abs(risk.Procent - percentageChange)).First();

            return Convert.ToDecimal(closestRisk.Multiplier);
        }

        public static decimal RoundToNearestTickSize(decimal value, decimal step, int digits)
        {
            if (value == 0) return 0.0M;
            var roundedValue = Math.Round(Math.Abs(value) / step) * step;
            return Math.Round(roundedValue, digits);
        }

        public static decimal CalculateSpread(decimal ask, decimal bid, decimal step, int digits)
        {
            decimal spread = ask >= bid ? ask - bid : 0;
            return RoundToNearestTickSize(spread, step, digits);
        }

        public static double CalculateCostSpread(double spread, double lotSize, decimal pointSize, double contractSize)
        {
            //  SymbolInfoInteger(_Symbol, SYMBOL_SPREAD) * _Point * Lots * CONTRACT_SIZE;
            return spread * lotSize * Convert.ToDouble(pointSize) * contractSize;
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





        public static bool IsBUYcommand(OnSendTradingviewSignalCommand command, decimal spread, decimal maxSpread)
        {
            if ((maxSpread == 0 || (maxSpread > 0 && spread < maxSpread))
                                                            && command.OrderType.Equals("BUY", StringComparison.CurrentCultureIgnoreCase)
                                                            && command.MarketOrder != null
                                                            && command.MarketOrder.Risk.HasValue
                                                            && command.MarketOrder.RiskRewardRatio.HasValue)
            {
                return true;
            }
            return false;
        }

    }
}
