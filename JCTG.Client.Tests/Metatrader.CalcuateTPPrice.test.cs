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
            double mtPrice = 0.3659;
            int mtDigits = 4;
            double tvPrice = 0.36554614;
            double tvSlPrice = 0.36602357;
            double spread = 0.0001;

            // Act
            var result = mt.CalculateSLForShort(mtPrice, spread, mtDigits, tvPrice, tvSlPrice);

            // Assert
            double expectedSLPrice = 7444.1;
            Assert.That(result, Is.EqualTo(expectedSLPrice).Within(0.1));
        }


    }
}