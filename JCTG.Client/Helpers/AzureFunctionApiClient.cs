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
            // Prepare the data to send 
            var postData = JsonConvert.SerializeObject(request);
            var content = new StringContent(postData, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _httpClient.PostAsync("https://justcalltheguy.azurewebsites.net/api/Metatrader?code=6CUPL6bDM0q_AQaqZpJnpRQNQko-WFuw-I9nlxu0UvxUAzFuDRTNtw==", content);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var entity = JsonConvert.DeserializeObject<List<MetatraderResponse>>(jsonString);
                if (entity == null)
                    return new List<MetatraderResponse>();
                else
                    return entity;
            }
            else
                return new List<MetatraderResponse>();
        }
    }
}
