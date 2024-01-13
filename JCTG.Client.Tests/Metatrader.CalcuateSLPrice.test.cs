using NUnit.Framework;

namespace JCTG.Client.Tests
{
    public class MetatraderCalcuateSLPriceTest
    {

        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void CalculateSLForLong_AtrMultiplierLessThan1_ReturnsCorrectValue()
        {
            // Arrange
            double mtPrice = 7464.1;
            double mtAtr = 0.8;
            double mtTickSize = 0.01;
            double tvPrice = 7462.1;
            double tvSlPrice = 7442.1;
            double tvAtr = 1.0;
            double spread = 0.0;

            // Act
            var result = Metatrader.CalculateSLForLong(mtPrice, mtAtr, spread, mtTickSize, tvPrice, tvSlPrice, tvAtr);

            // Assert
            double expectedSLPrice = 7444.1;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.0001));
        }

        [Test]
        public void CalculateSLForLong_AtrMultiplierGreaterThan1_ReturnsCorrectValue()
        {
            // Arrange
            double mtPrice = 7464.1;
            double mtAtr = 2.0;
            double mtTickSize = 0.01;
            double tvPrice = 7462.1;
            double tvSlPrice = 7442.1;
            double tvAtr = 1.0;
            double spread = 0.0;

            // Act
            var result = Metatrader.CalculateSLForLong(mtPrice, mtAtr, spread, mtTickSize, tvPrice, tvSlPrice, tvAtr);

            // Assert
            double expectedSLPrice = 7424.1;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.0001));
        }

        [Test]
        public void CalculateSLForLong_AtrMultiplierEquals1_ReturnsCorrectValue()
        {
            // Arrange
            double mtPrice = 7464.1;
            double mtAtr = 1.5;
            double mtTickSize = 0.01;
            double tvPrice = 7462.1;
            double tvSlPrice = 7442.1;
            double tvAtr = 1.0;
            double spread = 0.0;

            // Act
            var result = Metatrader.CalculateSLForLong(mtPrice, mtAtr, spread, mtTickSize, tvPrice, tvSlPrice, tvAtr);

            // Assert
            double expectedSLPrice = 7434.1;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.0001));
        }

        [Test]
        public void CalculateSLForLong_WhenMtATRIsGreaterThanSignalATR_ShouldUseCorrectAtrMultiplier()
        {
            // Arrange
            double mtPrice = 100.0, mtATR = 10.0, mtSpread = 0.5, mtTickSize = 0.1;
            double signalEntryPrice = 95.0, signalSlPrice = 90.0, signalATR = 5.0;

            // Expected ATR multiplier = mtATR / signalATR = 10 / 5 = 2
            double expectedSlPrice = 89.5; // Calculate expected SL price based on the formula

            // Act
            double actualSlPrice = Metatrader.CalculateSLForLong(mtPrice, mtATR, mtSpread, mtTickSize, signalEntryPrice, signalSlPrice, signalATR);

            // Assert
            Assert.That(expectedSlPrice, Is.EqualTo(actualSlPrice), "The calculated SL price is incorrect.");
        }
    }
}