using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace JCTG.Client
{
    public class AzureFunctionApiClient
    {
        private readonly HttpClient _httpClient;

        public AzureFunctionApiClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<MetatraderResponse> GetMetatraderResponseAsync(int accountId, int clientId, string metatraderTicker, double currentPrice, string tradingviewTicker, StrategyType strategyType)
        {
            // Prepare the data to send 
            var postData = string.Format($"{accountId},{clientId},{metatraderTicker},{currentPrice},{tradingviewTicker},{(int)strategyType}");
            var content = new StringContent(postData, Encoding.UTF8, "application/text");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/text"));
            var response = await _httpClient.PostAsync("https://justcalltheguy.azurewebsites.net/api/Metatrader?code=6CUPL6bDM0q_AQaqZpJnpRQNQko-WFuw-I9nlxu0UvxUAzFuDRTNtw==", content);
            //var response = await _httpClient.PostAsync("http://localhost:7259/api/Metatrader", content);
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var entity = JsonConvert.DeserializeObject<MetatraderResponse>(jsonString);
            if (entity == null)
                throw new Exception("Invalid operation");
            else
                return entity;
        }
    }
}
