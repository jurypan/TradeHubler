namespace JCTG.Client.Tests
{
    public class MetatraderCalculateLotSizeTests
    {
        private Metatrader mt;

        [SetUp]
        public void Setup()
        {
            mt = new Metatrader(new AppConfig());
        }

        [Test]
        public void CalculateLotSize_ValidInputs_CorrectCalculation()
        {
            // Arrange
            var accountBalance = 100000.0;
            var riskPercent = 1.0;
            var openPrice = 1.3000;
            var stopLossPrice = 1.2950;
            var pipValue = 10.0;
            var pipSize = 0.0001;
            var lotStep = 0.01;
            var minLotSizeAllowed = 0.01;
            var maxLotSizeAllowed = 5.0;
            var spread = 0.0;


            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, openPrice, stopLossPrice, pipValue, pipSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(2));
        }

        [Test]
        public void CalculateLotSize_WithValidInputs_ReturnsCorrectLotSize()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 10;
            double pointSize = 0.0001;
            double lotStep = 0.01;
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(0.1)); // Expected value based on the inputs
        }

        [Test]
        public void CalculateLotSize_ValidInputs_CorrectCalculationForNasdaq()
        {
            // Arrange
            var accountBalance = 100000.0;
            var riskPercent = 1.0;
            var openPrice = 12000.0; // NASDAQ index value
            var stopLossPrice = 11800.0; // 200 points stop loss
            var pipValue = 1.0; // $1 per point
            var pipSize = 1.0; // 1 point
            var lotStep = 1.0; // One contract step
            var minLotSizeAllowed = 1.0; // Minimum one contract
            var maxLotSizeAllowed = 10.0; // Maximum ten contracts
            var spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, openPrice, stopLossPrice, pipValue, pipSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(5)); // Expected value based on the inputs
        }

        [Test]
        public void CalculateLotSize_ValidInputs_CorrectCalculationForSP500()
        {
            // Arrange
            var accountBalance = 100000.0;
            var riskPercent = 1.0;
            var openPrice = 4300.0; // S&P 500 index value
            var stopLossPrice = 4290.0; // 10 points stop loss for shorter time frames
            var pipValue = 1.0; // $1 per point
            var pipSize = 1.0; // 1 point
            var lotStep = 0.01; // One contract step
            var minLotSizeAllowed = 1.0; // Minimum one contract
            var maxLotSizeAllowed = 10.0; // Maximum ten contracts
            var spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, openPrice, stopLossPrice, pipValue, pipSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(10)); // Expected value based on the inputs
        }

        [Test]
        public void CalculateLotSize_WithValidInputs_ReturnsCorrectLotSize2()
        {
            // Arrange
            double accountBalance = 163400;
            double riskPercent = 0.15;
            double askPrice = 4726.85000;
            double stopLossPrice = 4632.31300;
            double tickValue = 0.0091117003;
            double pointSize = 0.01;
            double lotStep = 1;
            double minLotSizeAllowed = 1;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(3)); // Expected value based on the inputs
        }

        [Test]
        public void CalculateLotSize_WithNegativeBalance_ThrowsArgumentException()
        {
            // Arrange
            double accountBalance = -1000; // Invalid input

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => mt.CalculateLotSize(0, accountBalance, 1, 1.3000, 1.2900, 10, 0.0001, 0.01, 0.01, 100, 0.0));
            Assert.That(ex.ParamName, Is.EqualTo("accountBalance"));
        }

        [Test]
        public void CalculateLotSize_WithZeroBalance_ThrowsArgumentException()
        {
            // Arrange
            double accountBalance = 0; // Invalid input

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => mt.CalculateLotSize(0, accountBalance, 1, 1.3000, 1.2900, 10, 0.0001, 0.01, 0.01, 100, 0.0));
            Assert.That(ex.ParamName, Is.EqualTo("accountBalance"));
        }

        [Test]
        public void CalculateLotSize_WithLotSizeBelowMinimum_ReturnsMinimumLotSize()
        {
            // Arrange
            double accountBalance = 1000;
            double riskPercent = 0.1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 10;
            double pointSize = 0.0001;
            double lotStep = 0.01;
            double minLotSizeAllowed = 0.05;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(minLotSizeAllowed));
        }

        [Test]
        public void CalculateLotSize_WithHigherRiskPercent_ReturnsLargerLotSize()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 2; // Increased risk percent
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 10;
            double pointSize = 0.0001;
            double lotStep = 0.01;
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(0.2));
        }

        [Test]
        public void CalculateLotSize_WithSmallerStopLossDifference_ReturnsSmallerLotSize()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2950; // Smaller difference to stop loss
            double tickValue = 10;
            double pointSize = 0.0001;
            double lotStep = 0.01;
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(0.2));
        }

        [Test]
        public void CalculateLotSize_WithLargerLotStep_ReturnsAdjustedLotSize()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 10;
            double pointSize = 0.0001;
            double lotStep = 0.05; // Larger lot step
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            // The expected value should be adjusted according to the new lot step
            Assert.That(result % 0.05 < double.Epsilon || Math.Abs(result % 0.05 - 0.05) < double.Epsilon);
        }

        [Test]
        public void CalculateLotSize_WithHigherTickValue_ReturnsLowerLotSize()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 20; // Increased tick value
            double pointSize = 0.0001;
            double lotStep = 0.01;
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result < 0.1); // Expecting a lower lot size due to higher tick value
        }

        [Test]
        public void CalculateLotSize_WithIncreasedTickValue_ReturnsExpectedLotSize()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 20; // Increased tick value
            double pointSize = 0.0001;
            double lotStep = 0.01;
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(0.05)); // Expected lot size based on the calculation
        }

        [Test]
        public void CalculateLotSize_WithVerySmallRiskPercent_ReturnsMinimumLotSize()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 0.01; // Very small risk percent
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 20;
            double pointSize = 0.0001;
            double lotStep = 0.01;
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(minLotSizeAllowed)); // Expecting the minimum lot size due to very small risk percent
        }

        [Test]
        public void CalculateLotSize_WithLargeStopLossDifference_ReturnsSmallerLotSize()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2500; // Large difference to stop loss
            double tickValue = 20;
            double pointSize = 0.0001;
            double lotStep = 0.01;
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.LessThan(0.1)); // Expecting a smaller lot size due to larger stop loss difference
        }

        [Test]
        public void CalculateLotSize_WithLargerLotStep_AdjustsLotSizeAccordingly()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 20;
            double pointSize = 0.0001;
            double lotStep = 0.05; // Larger lot step
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result % lotStep < double.Epsilon || Math.Abs(result % lotStep - lotStep) < double.Epsilon); // Result should be a multiple of lotStep
        }




        [Test]
        public void CalculateLotSize_WithLotStepEqualToMinLotSize_ReturnsAdjustedLotSize()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 20;
            double pointSize = 0.0001;
            double lotStep = 0.01; // Lot step equal to minLotSizeAllowed
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(0.05));
        }

        [Test]
        public void CalculateLotSize_WithLotStepEqualToMinLotSize_ReturnsAdjustedLotSize2()
        {
            // Arrange
            double accountBalance = 10000;
            double riskPercent = 1;
            double askPrice = 1.3000;
            double stopLossPrice = 1.2900;
            double tickValue = 20;
            double pointSize = 0.0001;
            double lotStep = 0.01; // Lot step equal to minLotSizeAllowed
            double minLotSizeAllowed = 0.01;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Assert
            Assert.That(result, Is.EqualTo(0.05));
        }

        [Test]
        public void CalculateLotSize_WithSpecificParameters_ReturnsExpectedLotSize()
        {
            // Arrange
            double accountBalance = 100000;
            double riskPercent = 0.2;
            double askPrice = 146.55;
            double stopLossPrice = 143.17;
            double tickValue = 1;
            double pointSize = 1;
            double lotStep = 1;
            double minLotSizeAllowed = 1;
            double maxLotSizeAllowed = 500;
            double spread = 0.0;

            // Act
            var result = mt.CalculateLotSize(0, accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed, spread);

            // Calculate expected result
            double riskAmount = accountBalance * (riskPercent / 100.0); // 200
            double stopLossPriceInPips = Math.Abs(askPrice - stopLossPrice) / pointSize; // 3.38
            double initialLotSize = riskAmount / (stopLossPriceInPips * tickValue); // 59.17
            double remainder = initialLotSize % lotStep; // 0.17
            double adjustedLotSize = remainder == 0 ? initialLotSize : initialLotSize - remainder; // 59
            double expectedLotSize = Math.Max(adjustedLotSize, minLotSizeAllowed); // 59

            // Assert
            Assert.That(result, Is.EqualTo(expectedLotSize)); // Expecting the calculated lot size
        }
    }
}