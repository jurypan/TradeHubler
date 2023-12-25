using NUnit.Framework;

namespace JCTG.AzureFunction.Tests
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
            double tvPrice = 7462.1;
            double tvSlPrice = 7442.1;
            double tvAtr = 1.0;
            double offset = -2.0;

            // Act
            var result = Metatrader.CalculateSLForLong(mtPrice, mtAtr, tvPrice, tvSlPrice, tvAtr, offset);

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
            double tvPrice = 7462.1;
            double tvSlPrice = 7442.1;
            double tvAtr = 1.0;
            double offset = -2.0;

            // Act
            var result = Metatrader.CalculateSLForLong(mtPrice, mtAtr, tvPrice, tvSlPrice, tvAtr, offset);

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
            double tvPrice = 7462.1;
            double tvSlPrice = 7442.1;
            double tvAtr = 1.0;
            double offset = -2.0;

            // Act
            var result = Metatrader.CalculateSLForLong(mtPrice, mtAtr, tvPrice, tvSlPrice, tvAtr, offset);

            // Assert
            double expectedSLPrice = 7434.1;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.0001));
        }


    }
}