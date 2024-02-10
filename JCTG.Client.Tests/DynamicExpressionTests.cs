using JCTG.Models;

namespace JCTG.Client.Tests
{
    public class DynamicExpressionTests
    {
        private List<BarData> bars;

        [SetUp]
        public void Setup()
        {
            bars = new List<BarData>
            {
                new() { High = 10m, Low = 5m, Timeframe = "5M" }, // First bar
                new() { High = 15m, Low = 8m, Timeframe = "5M" }  // Second bar
            };
        }

        [Test]
        public async Task EvaluateExpression_BasicExpression_ReturnsCorrectResult()
        {
            string expression = "Bar[1].High + (Bar[1].High - Bar[0].Low)";

            decimal expected = 20m + (20m - 5m);
            decimal result = await DynamicEvaluator.EvaluateExpressionAsync(expression, bars);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public async Task EvaluateExpression_BasicExpression_ReturnsCorrectResult2()
        {
            string expression = "Bar[1].High";
            decimal expected = 15m;
            decimal result = await DynamicEvaluator.EvaluateExpressionAsync(expression, bars);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void EvaluateExpression_EmptyBarDataList_ThrowsException()
        {
            List<BarData> emptyBars = new List<BarData>();
            Assert.ThrowsAsync<System.ArgumentException>(() => DynamicEvaluator.EvaluateExpressionAsync("", emptyBars));
        }

        [Test]
        public void EvaluateExpression_InvalidExpression_ThrowsException()
        {
            string expression = "Invalid Expression";
            Assert.ThrowsAsync<System.Exception>(() => DynamicEvaluator.EvaluateExpressionAsync(expression, bars));
        }
    }
}
