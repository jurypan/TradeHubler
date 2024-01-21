using NUnit.Framework;

namespace JCTG.Client.Tests
{
    public class MetatraderCalcuateSLPriceTest
    {

        private Metatrader mt;

        [SetUp]
        public void Setup()
        {
            mt = new Metatrader(new AppConfig());
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
            var result = mt.CalculateSLForLong(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

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
            var result = mt.CalculateSLForLong(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

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
            var result = mt.CalculateSLForLong(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

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
            var actualSlPrice = mt.CalculateSLForLong(mtPrice,  mtSpread, mtDigits, signalEntryPrice, signalSlPrice);

            // Assert
            Assert.That(expectedSlPrice, Is.EqualTo(actualSlPrice), "The calculated SL price is incorrect.");
        }
    }
}