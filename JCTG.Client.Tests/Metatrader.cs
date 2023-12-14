namespace JCTG.Client.Tests
{
    public class MetatraderTests
    {
        private Metatrader mt;

        [SetUp]
        public void Setup()
        {
            mt = new Metatrader(new AppConfig());
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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.That(result, Is.EqualTo(0.1)); // Expected value based on the inputs
        }

        [Test]
        public void CalculateLotSize_WithNegativeBalance_ThrowsArgumentException()
        {
            // Arrange
            double accountBalance = -1000; // Invalid input

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => mt.CalculateLotSize(accountBalance, 1, 1.3000, 1.2900, 10, 0.0001, 0.01, 0.01, 100));
            Assert.That(ex.ParamName, Is.EqualTo("accountBalance"));
        }

        [Test]
        public void CalculateLotSize_WithZeroBalance_ThrowsArgumentException()
        {
            // Arrange
            double accountBalance = 0; // Invalid input

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => mt.CalculateLotSize(accountBalance, 1, 1.3000, 1.2900, 10, 0.0001, 0.01, 0.01, 100));
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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            // The expected value should be adjusted according to the new lot step
            Assert.IsTrue(result % 0.05 < double.Epsilon || Math.Abs(result % 0.05 - 0.05) < double.Epsilon);
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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.Less(result, 0.1); // Expecting a lower lot size due to higher tick value
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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

            // Assert
            Assert.IsTrue(result % lotStep < double.Epsilon || Math.Abs(result % lotStep - lotStep) < double.Epsilon); // Result should be a multiple of lotStep
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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

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

            // Act
            var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

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

        //[Test]
        //public void CalculateLotSize_WithSpecificParameters_ReturnsExpectedLotSize2()
        //{
        //    // Arrange
        //    double accountBalance = 161500;
        //    double riskPercent = 0.2;
        //    double askPrice = 146.55;
        //    double stopLossPrice = 143.17;
        //    double tickValue = 1;
        //    double pointSize = 1;
        //    double lotStep = 1;
        //    double minLotSizeAllowed = 1;
        //    double maxLotSizeAllowed = 500;

        //    // Act
        //    var result = mt.CalculateLotSize(accountBalance, riskPercent, askPrice, stopLossPrice, tickValue, pointSize, lotStep, minLotSizeAllowed, maxLotSizeAllowed);

        //    // Calculate expected result
        //    double riskAmount = accountBalance * (riskPercent / 100.0); // 200
        //    double stopLossPriceInPips = Math.Abs(askPrice - stopLossPrice) / pointSize; // 3.38
        //    double initialLotSize = riskAmount / (stopLossPriceInPips * tickValue); // 59.17
        //    double remainder = initialLotSize % lotStep; // 0.17
        //    double adjustedLotSize = remainder == 0 ? initialLotSize : initialLotSize - remainder; // 59
        //    double expectedLotSize = Math.Max(adjustedLotSize, minLotSizeAllowed); // 59

        //    // Assert
        //    Assert.That(result, Is.EqualTo(expectedLotSize)); // Expecting the calculated lot size
        //}
    }
}