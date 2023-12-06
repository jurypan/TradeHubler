using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;
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

        public async Task<List<MetatraderResponse>> GetMetatraderResponseAsync(List<MetatraderRequest> request)
        {
            // Number of retry attempts
            const int maxRetries = 3;
            // Delay in milliseconds between retries
            const int delayBetweenRetries = 2000;

            // Prepare the data to send 
            var postData = JsonConvert.SerializeObject(request);
            var content = new StringContent(postData, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    var response = await _httpClient.PostAsync("https://justcalltheguy.azurewebsites.net/api/Metatrader?code=6CUPL6bDM0q_AQaqZpJnpRQNQko-WFuw-I9nlxu0UvxUAzFuDRTNtw==", content);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        var entity = JsonConvert.DeserializeObject<List<MetatraderResponse>>(jsonString);
                        return entity ?? new List<MetatraderResponse>();
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
            return new List<MetatraderResponse>();
        }

    }
}
