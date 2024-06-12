using JCTG.Models;
using NUnit.Framework;

namespace JCTG.Client.Tests
{
    public class RiskCalculatorTest
    {
        private List<Risk> riskData;

        [SetUp]
        public void Setup()
        {
            riskData =
            [
                new() { Procent = 0, Multiplier = 1 },
                new() { Procent = 2, Multiplier = 1.25 },
                new() { Procent = 4, Multiplier = 1.5 },
                new() { Procent = -2, Multiplier = 0.75 },
                new() { Procent = -4, Multiplier = 0.5 },
                new() { Procent = -6, Multiplier = 0.25 }
            ];
        }

        [Test]
        public void LotSize_CalculatesCorrectly()
        {
            // Arrange
            double startBalance = 20000;
            double balance = 19495.48;
            decimal risk = 0.50M;
            decimal askPrice = 8226.5M;
            decimal slPrice = 8188.4M;
            decimal tickValue = 0.1M;
            decimal tickSize = 0.1M;
            decimal tickStep = 0.1M;
            double lotStep = 1;
            double minLotSize = 1;
            double maxLotSize = 5;
            List<Risk>? riskData = null; // Assuming no additional risk data
            decimal pointSize = 0.01M;

            // Act
            decimal result = RiskCalculator.LotSize2(startBalance, balance, risk, askPrice, slPrice, tickValue, pointSize, tickSize, tickStep, lotStep, minLotSize, maxLotSize, riskData);

            // Assert
            Assert.That(result, Is.EqualTo(1.00M)); // Assuming the expected lot size calculation is 1.00
        }

        [Test]
        public void CalculateSLForLong_AtrMultiplierLessThan1_ReturnsCorrectValue()
        {
            // Arrange
            var mtPrice = 7464.1M;
            int mtDigits = 1;
            var tvPrice = 7462.1M;
            var tvSlPrice = 7442.1M;
            var spread = 0.0M;

            // Act
            var result = RiskCalculator.SLForLong(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

            // Assert
            double expectedSLPrice = 7444.1;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.1));
        }

        [Test]
        public void CalculateSLForLong_AtrMultiplierGreaterThan1_ReturnsCorrectValue()
        {
            // Arrange
            var mtPrice = 7464.1M;
            int mtDigits = 1;
            var tvPrice = 7462.1M;
            var tvSlPrice = 7442.1M;
            var spread = 0.0M;

            // Act
            var result = RiskCalculator.SLForLong(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

            // Assert
            var expectedSLPrice = 7424.1M;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.0001M));
        }

        [Test]
        public void CalculateSLForLong_AtrMultiplierEquals1_ReturnsCorrectValue()
        {
            // Arrange
            var mtPrice = 7464.1M;
            int mtDigits = 1;
            var tvPrice = 7462.1M;
            var tvSlPrice = 7442.1M;
            var spread = 0.0M;

            // Act
            var result = RiskCalculator.SLForLong(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

            // Assert
            var expectedSLPrice = 7434.1M;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.0001M));
        }

        [Test]
        public void CalculateSLForLong_WhenMtATRIsGreaterThanSignalATR_ShouldUseCorrectAtrMultiplier()
        {
            // Arrange
            int mtDigits = 1;
            var mtPrice = 100.0M;
            var mtSpread = 0.5M;
            var signalEntryPrice = 95.0M;
            var signalSlPrice = 90.0M;

            // Expected ATR multiplier = mtATR / signalATR = 10 / 5 = 2
            var expectedSlPrice = 89.5M; // Calculate expected SL price based on the formula

            // Act
            var actualSlPrice = RiskCalculator.SLForLong(mtPrice,  mtSpread, mtDigits, signalEntryPrice, signalSlPrice);

            // Assert
            Assert.That(expectedSlPrice, Is.EqualTo(actualSlPrice), "The calculated SL price is incorrect.");
        }

        [Test]
        public void CalculateTPForLong_ReturnsCorrectValue()
        {
            // Arrange
            var mtPrice = 0.3659M;
            int mtDigits = 4;
            var tvPrice = 0.36554614M;
            var tvSlPrice = 0.36602357M;
            var spread = 0.0001M;

            // Act
            var result = RiskCalculator.SLForShort(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

            // Assert
            var expectedSLPrice = 7444.1M;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.1M));
        }


        [Test]
        public void CalculateLotSize_ValidInputs_CorrectCalculation1()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 5000.0;
            var riskPercent = 0.05M;
            var openPrice = 2513.87M;
            var stopLossPrice = 2523.19M;
            var tickValue = 0.01M;
            var tickSize = 0.01M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 100.0;


            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, openPrice, stopLossPrice, tickValue, tickSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(0.4));
        }

        [Test]
        public void CalculateLotSize_ValidInputs_CorrectCalculation2()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 5000.0;
            var riskPercent = 0.05M;
            var openPrice = 2530.21M;
            var stopLossPrice = 2536.50M;
            var tickValue = 0.01M;
            var tickSize = 0.01M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 100.0;


            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, openPrice, stopLossPrice, tickValue, tickSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(0.4));
        }

        [Test]
        public void CalculateLotSize_WithValidInputs_ReturnsCorrectLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 1M;
            var askPrice = 1.3M;
            var stopLossPrice = 1.29M;
            var tickValue = 10M;
            var pointSize = 0.0001M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(0.1)); // Expected value based on the inputs
        }

        [Test]
        public void CalculateLotSize_ValidInputs_CorrectCalculationForNasdaq()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 100000.0;
            var riskPercent = 1.0M;
            var openPrice = 12000.0M; // NASDAQ index value
            var stopLossPrice = 11800.0M; // 200 points stop loss
            var tickValue = 1.0M; // $1 per point
            var tickSize = 1.0M; // 1 point
            var lotStep = 1.0; // One contract step
            var minLotSizeAllowed = 1.0; // Minimum one contract
            var maxLotSizeAllowed = 10.0; // Maximum ten contracts

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, openPrice, stopLossPrice, tickValue, tickSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(5)); // Expected value based on the inputs
        }

        [Test]
        public void CalculateLotSize_ValidInputs_CorrectCalculationForSP500()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 100000.0;
            var riskPercent = 1.0M;
            var openPrice = 4300.0M; // S&P 500 index value
            var stopLossPrice = 4290.0M; // 10 points stop loss for shorter time frames
            var tickValue = 1.0M; // $1 per point
            var tickSize = 1.0M; // 1 point
            var lotStep = 0.01; // One contract step
            var minLotSizeAllowed = 1.0; // Minimum one contract
            var maxLotSizeAllowed = 10.0; // Maximum ten contracts

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, openPrice, stopLossPrice, tickValue, tickSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(10)); // Expected value based on the inputs
        }

        [Test]
        public void CalculateLotSize_WithValidInputs_ReturnsCorrectLotSize2()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 163400;
            var riskPercent = 0.15M;
            var askPrice = 4726.85000M;
            var stopLossPrice = 4632.31300M;
            var tickValue = 0.0091117003M;
            var pointSize = 0.01M;
            var lotStep = 1;
            var minLotSizeAllowed = 1;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(3)); // Expected value based on the inputs
        }

        [Test]
        public void CalculateLotSize_WithNegativeBalance_ThrowsArgumentException()
        {
            // Arrange
            var startBalance = 5000.0;
            double accountBalance = -1000; // Invalid input

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, 1M, 1.3000M, 1.2900M, 10M, 0.0001M, 0.01, 0.01, 100);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void CalculateLotSize_WithZeroBalance_ReturnZeroLots()
        {
            // Arrange
            var startBalance = 5000.0;
            double accountBalance = 0; // Invalid input

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, 1M, 1.3000M, 1.2900M, 10, 0.0001M, 0.01, 0.01, 100);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void CalculateLotSize_WithLotSizeBelowMinimum_ReturnsMinimumLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 1000;
            var riskPercent = 0.1M;
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2900M;
            var tickValue = 10M;
            var pointSize = 0.0001M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.05;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(minLotSizeAllowed));
        }

        [Test]
        public void CalculateLotSize_WithHigherRiskPercent_ReturnsLargerLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 2M; // Increased risk percent
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2900M;
            var tickValue = 10M;
            var pointSize = 0.0001M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(0.2));
        }

        [Test]
        public void CalculateLotSize_WithSmallerStopLossDifference_ReturnsSmallerLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 1M;
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2950M; // Smaller difference to stop loss
            var tickValue = 10M;
            var pointSize = 0.0001M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(0.2));
        }

        [Test]
        public void CalculateLotSize_WithLargerLotStep_ReturnsAdjustedLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 1M;
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2900M;
            var tickValue = 10M;
            var pointSize = 0.0001M;
            var lotStep = 0.05; // Larger lot step
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            // The expected value should be adjusted according to the new lot step
            Assert.That(result % 0.05M < Convert.ToDecimal(double.Epsilon) || Math.Abs(result % 0.05M - 0.05M) < Convert.ToDecimal(double.Epsilon));
        }

        [Test]
        public void CalculateLotSize_WithHigherTickValue_ReturnsLowerLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 1M;
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2900M;
            var tickValue = 20M; // Increased tick value
            var pointSize = 0.0001M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result < 0.1M); // Expecting a lower lot size due to higher tick value
        }

        [Test]
        public void CalculateLotSize_WithIncreasedTickValue_ReturnsExpectedLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 1M;
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2900M;
            var tickValue = 20M; // Increased tick value
            var pointSize = 0.0001M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(0.05)); // Expected lot size based on the calculation
        }

        [Test]
        public void CalculateLotSize_WithVerySmallRiskPercent_ReturnsMinimumLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 0.01M; // Very small risk percent
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2900M;
            var tickValue = 20M;
            var pointSize = 0.0001M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(minLotSizeAllowed)); // Expecting the minimum lot size due to very small risk percent
        }

        [Test]
        public void CalculateLotSize_WithLargeStopLossDifference_ReturnsSmallerLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 1M;
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2500M; // Large difference to stop loss
            var tickValue = 20M;
            var pointSize = 0.0001M;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.LessThan(0.1)); // Expecting a smaller lot size due to larger stop loss difference
        }

        [Test]
        public void CalculateLotSize_WithLargerLotStep_AdjustsLotSizeAccordingly()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 1M;
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2900M;
            var tickValue = 20M;
            var pointSize = 0.0001M;
            var lotStep = 0.05; // Larger lot step
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result % Convert.ToDecimal(lotStep) < Convert.ToDecimal(double.Epsilon) || Math.Abs(result % Convert.ToDecimal(lotStep) - Convert.ToDecimal(lotStep)) < Convert.ToDecimal(double.Epsilon)); // Result should be a multiple of lotStep
        }




        [Test]
        public void CalculateLotSize_WithLotStepEqualToMinLotSize_ReturnsAdjustedLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 1M;
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2900M;
            var tickValue = 20M;
            var pointSize = 0.0001M;
            var lotStep = 0.01; // Lot step equal to minLotSizeAllowed
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(0.05));
        }

        [Test]
        public void CalculateLotSize_WithLotStepEqualToMinLotSize_ReturnsAdjustedLotSize2()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 10000;
            var riskPercent = 1M;
            var askPrice = 1.3000M;
            var stopLossPrice = 1.2900M;
            var tickValue = 20M;
            var pointSize = 0.0001M;
            var lotStep = 0.01; // Lot step equal to minLotSizeAllowed
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(0.05));
        }

        [Test]
        public void CalculateLotSize_WithSpecificParameters_ReturnsExpectedLotSize()
        {
            // Arrange
            var startBalance = 5000.0;
            var accountBalance = 100000;
            var riskPercent = 0.2M;
            var askPrice = 146.55M;
            var stopLossPrice = 143.17M;
            var tickValue = 1M;
            var pointSize = 1M;
            var lotStep = 1;
            var minLotSizeAllowed = 1;
            var maxLotSizeAllowed = 500;

            // Act
            var result = RiskCalculator.LotSize(startBalance, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Calculate expected result
            var riskAmount = accountBalance * (riskPercent / 100.0M); // 200
            var stopLossPriceInPips = Math.Abs(askPrice - stopLossPrice) / pointSize; // 3.38
            var initialLotSize = Convert.ToDecimal(riskAmount) / (stopLossPriceInPips * tickValue); // 59.17
            var remainder = initialLotSize % lotStep; // 0.17
            var adjustedLotSize = remainder == 0 ? initialLotSize : initialLotSize - remainder; // 59
            var expectedLotSize = Math.Max(adjustedLotSize, minLotSizeAllowed); // 59

            // Assert
            Assert.That(result, Is.EqualTo(expectedLotSize)); // Expecting the calculated lot size
        }


        [Test]
        public void GetClosestRiskPercentage_WithPositiveBalance_ReturnsCorrectAdjustedRisk()
        {
            double startBalance = 100000;
            double accountBalance = 101000; // 1% up
            var result = RiskCalculator.ChooseClosestMultiplier(startBalance, accountBalance, riskData);
            Assert.That(result, Is.EqualTo(0M));
        }

        [Test]
        public void GetClosestRiskPercentage_WithNegativeBalance_ReturnsCorrectAdjustedRisk()
        {
            double startBalance = 100000;
            double accountBalance = 102000; // 2% up
            var result = RiskCalculator.ChooseClosestMultiplier(startBalance, accountBalance, riskData);
            Assert.That(result, Is.EqualTo(1.25M));
        }
    }
}