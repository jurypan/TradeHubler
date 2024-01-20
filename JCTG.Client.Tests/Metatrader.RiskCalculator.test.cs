namespace JCTG.Client.Tests
{
    public class MetatraderRiskCalculatorTests
    {
    //          "Risk": [
    //    {
    //      "Procent": 2.0,
    //      "Multiplier": 25.0
    //    },
    //    {
    //      "Procent": 4.0,
    //      "Multiplier": 50.0
    //    },
    //    {
    //"Procent": -2.0,
    //      "Multiplier": -25.0
    //    },
    //    {
    //"Procent": -4.0,
    //      "Multiplier": -50.0
    //    },
    //    {
    //"Procent": -6.0,
    //      "Multiplier": -75.0
    //    }
    //  ],

        private List<Risk> riskData;

        [SetUp]
        public void Setup()
        {
            riskData = new List<Risk>()
            {
                new() { Procent = 0, Multiplier = 1 },
                new() { Procent = 2, Multiplier = 1.25 },
                new() { Procent = 4, Multiplier = 1.5 },
                new() { Procent = -2, Multiplier = 0.75 },
                new() { Procent = -4, Multiplier = 0.5 },
                new() { Procent = -6, Multiplier = 0.25 }
            };
        }


        [Test]
        public void GetClosestRiskPercentage_WithPositiveBalance_ReturnsCorrectAdjustedRisk()
        {
            double startBalance = 100000;
            double accountBalance = 101000; // 1% up
            var result = Metatrader.ChooseClosestMultiplier(startBalance, accountBalance, riskData);
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void GetClosestRiskPercentage_WithNegativeBalance_ReturnsCorrectAdjustedRisk()
        {
            double startBalance = 100000;
            double accountBalance = 102000; // 2% up
            var result = Metatrader.ChooseClosestMultiplier(startBalance, accountBalance, riskData);
            Assert.That(result, Is.EqualTo(1.25));
        }
    }
}