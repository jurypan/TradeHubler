namespace JCTG.Client.Tests
{
    public class MetatraderRiskCalculatorTests
    {
        private Dictionary<string, double> riskData;

        [SetUp]
        public void Setup()
        {
            riskData = new Dictionary<string, double>
            {
                { "1", 12 },
                { "2", 25 },
                { "4", 50 },
                { "-1", -12 },
                { "-2", -25 },
                { "-4", -50 },
                { "-6", -75 }
            };
        }


        [Test]
        public void GetClosestRiskPercentage_WithPositiveBalance_ReturnsCorrectAdjustedRisk()
        {
            double accountBalance = 1; // 1% up
            var result = Metatrader.GetClosestRiskPercentage(accountBalance, riskData);
            Assert.That(result, Is.EqualTo(1.12));
        }

        [Test]
        public void GetClosestRiskPercentage_WithNegativeBalance_ReturnsCorrectAdjustedRisk()
        {
            double accountBalance = -4; // -4% down
            var result = Metatrader.GetClosestRiskPercentage(accountBalance, riskData);
            Assert.That(result, Is.EqualTo(0.5));
        }
    }
}