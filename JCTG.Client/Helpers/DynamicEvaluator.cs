using JCTG.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Text.RegularExpressions;

namespace JCTG.Client
{
    public class DynamicEvaluator
    {
        private static void LogCalculation(Dictionary<string, string> logMessages, string key, object value) => logMessages[key] = value.ToString();



        public static async Task<decimal?> EvaluateExpressionAsync(string expression, List<BarData> bars)
        {
            // Convert the timestamps in bars to UTC (or any common timezone)
            var barsDictionary = bars.OrderByDescending(f => f.Time)
                                     .ToDictionary(
                                         bar => bar.Epoch,
                                         bar => bar);


            var options = ScriptOptions.Default
                .AddReferences(typeof(BarData).Assembly)
                .AddImports("System", "System.Collections.Generic");

            var script = CSharpScript.Create<decimal>(
                expression,
                options,
                globalsType: typeof(ScriptContext));

            script.Compile(); // Optional, for performance

            try
            {
                var result = await script.RunAsync(new ScriptContext { Bar = barsDictionary });
                return result.ReturnValue;
            }
            catch (CompilationErrorException ex)
            {
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static decimal? EvaluateExpression(string expression, List<BarData> bars, out Dictionary<string, string> logMessages)
        {
            // Init log messages
            logMessages = [];

            // HttpCallOnLogEvent the initial input parameters
            LogCalculation(logMessages, "Expression", expression);
            LogCalculation(logMessages, "Bars.Count", bars.Count);

            // Convert the timestamps in bars to UTC (or any common timezone)
            var barsDictionary = bars.OrderByDescending(f => f.Time).ToDictionary(bar => bar.Epoch, bar => bar);

            var options = ScriptOptions.Default
                .AddReferences(typeof(BarData).Assembly)
                .AddImports("System", "System.Collections.Generic");

            var script = CSharpScript.Create<decimal>(
                expression,
                options,
                globalsType: typeof(ScriptContext));

            script.Compile(); // Optional, for performance

            try
            {
                // Get the code
                LogCalculation(logMessages, "Code", script.Code);

                // Execute task
                var task = Task.Run(async () => await script.RunAsync(new ScriptContext { Bar = barsDictionary }));

                // Retrieve the result
                var result = task.Result;

                // HttpCallOnLogEvent
                LogCalculation(logMessages, "Result", result.ReturnValue);

                // Retun value
                return result.ReturnValue;
            }
            catch (CompilationErrorException ex)
            {
                // HttpCallOnLogEvent
                LogCalculation(logMessages, "CompilationErrorException", ex.Message);
                if(ex.InnerException != null) 
                    LogCalculation(logMessages, "CompilationErrorException", ex.InnerException.Message);
                return null;
            }
            catch (Exception ex)
            {
                // HttpCallOnLogEvent
                LogCalculation(logMessages, "Exception", ex.Message);
                if (ex.InnerException != null)
                    LogCalculation(logMessages, "Exception", ex.InnerException.Message);
                return null;
            }
        }

        public static DateTime? GetDateFromBarString(string input)
        {
            string pattern = @"\[(\d+)\]"; // Pattern to find digits inside brackets

            Match match = Regex.Match(input, pattern);

            if (match.Success)
            {
                string number = match.Groups[1].Value;
                long numberAsLong = long.Parse(number);
                return numberAsLong.FromUnixTime();
            }
            return null;
        }

        public class ScriptContext
        {
            // Dictionary to access BarData by Unix timestamp
            public Dictionary<long, BarData> Bar { get; set; }
        }

        public class ExpressionReturn
        {
            public decimal? Value { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}
