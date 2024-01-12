using Newtonsoft.Json;
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
            const int delayBetweenRetries = 5000;

            // Prepare the data to send 
            var postData = JsonConvert.SerializeObject(request);
            var content = new StringContent(postData, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    //var response = await _httpClient.PostAsync("http://localhost:7259/api/Metatrader", content);
                    var response = await _httpClient.PostAsync("https://justcalltheguy.azurewebsites.net/api/Metatrader?code=5cNSO8LDNjdrupIkouwPIU9tIOrjo2AMQJgsaSQOnXcNAzFu5YSBkg==", content);

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
                    if (retry != maxRetries - 1)
                        await Task.Delay(delayBetweenRetries); // Wait before retrying
                }
            }

            // If all retries failed, return an empty list or handle it as needed
            return new List<MetatraderResponse>();
        }

        public void SendTradeJournals(List<TradeJournalRequest> request)
        {
            // Number of retry attempts
            const int maxRetries = 3;
            // Delay in milliseconds between retries
            const int delayBetweenRetries = 5000;

            // Prepare the data to send 
            var postData = JsonConvert.SerializeObject(request);
            var content = new StringContent(postData, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Start a background task
            Task.Run(async () =>
            {
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        var response = await _httpClient.PostAsync("https://justcalltheguy.azurewebsites.net/api/TradeJournal?code=ZiH5_uE_CNU7Yu1QvBIhAHNe-rTG4nhKaXUiUt9lgIJtAzFuPvuf-A==", content);
                        //var response = await _httpClient.PostAsync("http://localhost:7259/api/TradeJournal", content);

                        // Check the response status
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            // Handle successful response
                            break; // Exit the loop if successful
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
            });
        }

        public void SendTradeJournal(TradeJournalRequest request)
        {
            // Number of retry attempts
            const int maxRetries = 3;
            // Delay in milliseconds between retries
            const int delayBetweenRetries = 5000;

            // Prepare the data to send 
            var obj = new List<TradeJournalRequest>
            {
                request
            };
            var postData = JsonConvert.SerializeObject(obj);
            var content = new StringContent(postData, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Start a background task
            Task.Run(async () =>
            {
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        var response = await _httpClient.PostAsync("https://justcalltheguy.azurewebsites.net/api/TradeJournal?code=ZiH5_uE_CNU7Yu1QvBIhAHNe-rTG4nhKaXUiUt9lgIJtAzFuPvuf-A==", content);
                        //var response = await _httpClient.PostAsync("http://localhost:7259/api/TradeJournal", content);

                        // Check the response status
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            // Handle successful response
                            break; // Exit the loop if successful
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
            });
        }

        public void SendLog(LogRequest request)
        {
            // Number of retry attempts
            const int maxRetries = 3;
            // Delay in milliseconds between retries
            const int delayBetweenRetries = 5000;

            // Prepare the data to send 
            var postData = JsonConvert.SerializeObject(request);
            var content = new StringContent(postData, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Start a background task
            Task.Run(async () =>
            {
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        // Execute the POST request
                        var response = await _httpClient.PostAsync("https://justcalltheguy.azurewebsites.net/api/Log?code=XkM05KfxlYMogR2lf_nN7klzdkHv2qwme8I-wOzUZz4EAzFuNh0wcQ==", content);

                        // Check the response status
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            // Handle successful response
                            break; // Exit the loop if successful
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
            });
        }
    }
}
