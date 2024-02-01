using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace JCTG.Client
{
    public class DynamicEvaluator
    {
        public static async Task<decimal> EvaluateExpressionAsync(string expression, List<BarData> bars)
        {
            bars = bars.OrderByDescending(f => f.Time).ToList();

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
                var result = await script.RunAsync(new ScriptContext { Bar = bars });
                return result.ReturnValue;
            }
            catch (CompilationErrorException ex)
            {
                // Handle compilation errors
                Console.WriteLine("Compilation error: " + ex.Message);
                return 0M;
            }
            catch (Exception ex)
            {
                // Log or handle other exceptions
                Console.WriteLine("Runtime error: " + ex.Message);
                return 0M;
            }
        }

        public class ScriptContext
        {
            public List<BarData> Bar { get; set; }
        }
    }
}
