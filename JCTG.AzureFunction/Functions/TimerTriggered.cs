using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace JCTG.AzureFunction.Functions
{
    public class TimerTriggered
    {
        private readonly ILogger _logger;

        public TimerTriggered(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TimerTriggered>();
        }

        [Function("TimerTriggered")]
        public async Task RunAsync([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            _logger.LogDebug($"C# Timer trigger function executed at: {DateTime.Now}");

            var url = "https://justcalltheguy.azurewebsites.net/api/Tradingview?code=YOUR_FUNCTION_KEY&source=timer";

            try
            {
                HttpResponseMessage response = await new HttpClient().GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogDebug(responseBody);
            }
            catch (HttpRequestException e)
            {
                _logger.LogError($"Error calling Tradingview function: {e.Message}");
            }
        }
    }
}
