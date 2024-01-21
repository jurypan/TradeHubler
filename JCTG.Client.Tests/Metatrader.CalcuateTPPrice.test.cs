using NUnit.Framework;

namespace JCTG.Client.Tests
{
    public class MetatraderCalculateTPPriceTest
    {

        private Metatrader mt;

        [SetUp]
        public void Setup()
        {
            mt = new Metatrader(new AppConfig());
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
            var result = mt.CalculateSLForShort(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

            // Assert
            var expectedSLPrice = 7444.1M;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.1M));
        }


    }
}