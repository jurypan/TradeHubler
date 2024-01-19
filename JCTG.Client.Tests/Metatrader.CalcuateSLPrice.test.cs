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
            double mtPrice = 7464.1;
            int mtDigits = 1;
            double tvPrice = 7462.1;
            double tvSlPrice = 7442.1;
            double spread = 0.0;

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
            double mtPrice = 7464.1;
            int mtDigits = 1;
            double tvPrice = 7462.1;
            double tvSlPrice = 7442.1;
            double spread = 0.0;

            // Act
            var result = mt.CalculateSLForLong(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

            // Assert
            double expectedSLPrice = 7424.1;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.0001));
        }

        [Test]
        public void CalculateSLForLong_AtrMultiplierEquals1_ReturnsCorrectValue()
        {
            // Arrange
            double mtPrice = 7464.1;
            int mtDigits = 1;
            double tvPrice = 7462.1;
            double tvSlPrice = 7442.1;
            double spread = 0.0;

            // Act
            var result = mt.CalculateSLForLong(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

            // Assert
            double expectedSLPrice = 7434.1;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.0001));
        }

        [Test]
        public void CalculateSLForLong_WhenMtATRIsGreaterThanSignalATR_ShouldUseCorrectAtrMultiplier()
        {
            // Arrange
            int mtDigits = 1;
            double mtPrice = 100.0,mtSpread = 0.5;
            double signalEntryPrice = 95.0, signalSlPrice = 90.0;

            // Expected ATR multiplier = mtATR / signalATR = 10 / 5 = 2
            double expectedSlPrice = 89.5; // Calculate expected SL price based on the formula

            // Act
            double actualSlPrice = mt.CalculateSLForLong(mtPrice,  mtSpread, mtDigits, signalEntryPrice, signalSlPrice);

            // Assert
            Assert.That(expectedSlPrice, Is.EqualTo(actualSlPrice), "The calculated SL price is incorrect.");
        }
    }
}