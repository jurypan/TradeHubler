namespace JCTG.WebApp.Backend.Helpers
{
    public static class Helper
    {
        public static decimal GetRiskAmount(this string logMessage) 
        {
            // Split the input string by new lines
            string[] lines = logMessage.Replace("LotSize || ", string.Empty).Replace(" ", string.Empty).Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

            // Dictionary to store the extracted values
            var values = new Dictionary<string, string>();

            // Extract the values
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    values[parts[0]] = parts[1];
                }
            }

            // Parse object
            decimal riskAmount = 0.0M;
            if (values.TryGetValue("RiskAmount", out string? prop))
            {
                riskAmount = decimal.Parse(prop);
            }
            return Math.Round(riskAmount, 2);
        }

        public static int GenerateRandomNumber(Random random, int digits)
        {
            int result = 0;
            for (int i = 0; i < digits; i++)
            {
                result = result * 10 + random.Next(0, 10);
            }
            return Math.Abs(result);
        }
    }
}
