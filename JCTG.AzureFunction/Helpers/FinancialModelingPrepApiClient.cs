using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;

namespace JCTG.AzureFunction.Helpers
{
    public class FinancialModelingPrepApiClient
    {
        private readonly HttpClient _httpClient;

        public FinancialModelingPrepApiClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<double> GetPriceAsync(string ticker)
        {
            // Number of retry attempts
            const int maxRetries = 3;
            // Delay in milliseconds between retries
            const int delayBetweenRetries = 5000;

            // Prepare the data to send 
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(string.Format($"https://financialmodelingprep.com/api/v3/stock/real-time-price/{ticker}?apikey=fd0c588f21a15111ff1debcb7a027175"));

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        var entity = JsonConvert.DeserializeObject<CompaniesPriceListContainer>(jsonString);
                        if(entity != null && entity.CompaniesPriceList != null && entity.CompaniesPriceList.Count == 1)
                        {
                            return entity.CompaniesPriceList.First().Price;
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    // Log the exception or handle it as needed
                    if (retry == maxRetries - 1)
                        throw; // Re-throw the exception on the last retry
                    else
                        await Task.Delay(delayBetweenRetries); // Wait before retrying
                }
            }

            // If all retries failed, return an empty list or handle it as needed
            return 0.0;
        }

        private class CompanyPrice
        {
            public required string Symbol { get; set; }
            public double Price { get; set; }
        }

        private class CompaniesPriceListContainer
        {
            public required List<CompanyPrice> CompaniesPriceList { get; set; }
        }
    }
}
