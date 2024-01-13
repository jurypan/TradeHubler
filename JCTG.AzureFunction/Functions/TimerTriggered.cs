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

            try
            {
                // Tradingview
                HttpResponseMessage response = await new HttpClient().GetAsync("https://justcalltheguy.azurewebsites.net/api/Tradingview?code=Ocebxtg1excWosFez5rWMtNp3ZsmIzSFQ0XhqtrfHlMuAzFuQ0OGhA==&source=timer");
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogDebug(responseBody);

                // Log
                response = await new HttpClient().GetAsync("https://justcalltheguy.azurewebsites.net/api/Log?code=XkM05KfxlYMogR2lf_nN7klzdkHv2qwme8I-wOzUZz4EAzFuNh0wcQ==&source=timer");
                response.EnsureSuccessStatusCode();
                responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogDebug(responseBody);

                // Tradingjournal
                response = await new HttpClient().GetAsync("https://justcalltheguy.azurewebsites.net/api/TradeJournal?code=ZiH5_uE_CNU7Yu1QvBIhAHNe-rTG4nhKaXUiUt9lgIJtAzFuPvuf-A==&source=timer");
                response.EnsureSuccessStatusCode();
                responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogDebug(responseBody);
            }
            catch (HttpRequestException e)
            {
                _logger.LogError($"Error calling Tradingview function: {e.Message}");
            }
        }
    }
}
