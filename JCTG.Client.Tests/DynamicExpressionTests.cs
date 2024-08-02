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
                new() { Open = 3316.08000M, High = 3329.17000M, Close = 3315.31000M, Low = 3313.64000M, Timeframe = "5M", Time = Convert.ToDateTime("2024.08.01 12:30:00") }, // First bar
                new() { Open = 3315.28000M, High = 3332.45000M, Close = 3330.50000M, Low = 3314.20000M, Timeframe = "5M", Time = Convert.ToDateTime("2024.08.01 12:45:00") }  // Second bar
            };
        }

        [Test]
        public async Task EvaluateExpression_BasicExpression_ReturnsCorrectResult()
        {
            string expression = "Bar[1722515400000].Low";

            decimal? result = await DynamicEvaluator.EvaluateExpressionAsync(expression, bars);
            Assert.That(result, Is.EqualTo(5m));
        }

        [Test]
        public async Task EvaluateExpression_BasicExpression_ReturnsCorrectResult2()
        {
            string expression = "Bar[1].High";
            decimal expected = 15m;
            decimal? result = await DynamicEvaluator.EvaluateExpressionAsync(expression, bars);
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
