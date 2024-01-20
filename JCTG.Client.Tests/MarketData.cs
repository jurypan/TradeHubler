namespace JCTG.Client.Tests
{
    public class MarketdataTests
    {
        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void CountSignificantDigits_WithOneTenth_ReturnsOne()
        {
            // Arrange
            double input = 0.1;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void CountSignificantDigits_WithOneTenThousandth_ReturnsFour()
        {
            // Arrange
            double input = 0.0001;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(4));
        }

        [Test]
        public void CountSignificantDigits_WithZero_ReturnsZero()
        {
            // Arrange
            double input = 0;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void CountSignificantDigits_WithWholeNumber_ReturnsZero()
        {
            // Arrange
            double input = 123;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void CountSignificantDigits_WithDecimalAndZeros_ReturnsCorrectCount()
        {
            // Arrange
            double input = 123.00400;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public void CountSignificantDigits_WithNegativeNumber_ReturnsCorrectCount()
        {
            // Arrange
            double input = -0.0056;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(4));
        }
    }
}