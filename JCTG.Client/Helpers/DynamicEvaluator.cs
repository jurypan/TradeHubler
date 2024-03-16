using JCTG.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Text.RegularExpressions;

namespace JCTG.Client
{
    public class DynamicEvaluator
    {
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
            catch (CompilationErrorException)
            {
                return null;
            }
            catch (Exception)
            {
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
    }
}
