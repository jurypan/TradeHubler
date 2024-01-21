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
            decimal input = 0.1M;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void CountSignificantDigits_WithOneTenThousandth_ReturnsFour()
        {
            // Arrange
            decimal input = 0.0000100000M;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(5));
        }

        [Test]
        public void CountSignificantDigits_WithZero_ReturnsZero()
        {
            // Arrange
            decimal input = 0;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void CountSignificantDigits_WithWholeNumber_ReturnsZero()
        {
            // Arrange
            decimal input = 123;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void CountSignificantDigits_WithDecimalAndZeros_ReturnsCorrectCount()
        {
            // Arrange
            decimal input = 123.00400M;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(3));
        }

        [Test]
        public void CountSignificantDigits_WithNegativeNumber_ReturnsCorrectCount()
        {
            // Arrange
            decimal input = -0.0056M;

            // Act
            int result = MarketData.CountSignificantDigits(input);

            // Assert
            Assert.That(result, Is.EqualTo(4));
        }
    }
}