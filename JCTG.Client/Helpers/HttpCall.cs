using JCTG.Models;
using Newtonsoft.Json;

namespace JCTG.Client
{
    public class HttpCall
    {
        // HttpClient is intended to be instantiated once and re-used throughout the life of an application.
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<string?> GetJsonAsync(string url)
        {
            try
            {
                // Asynchronously call the URI specified, and await the response.
                // Make sure to call .EnsureSuccessStatusCode() to throw an exception if the response is an error.
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                // Asynchronously read the response as a string.
                string json = await response.Content.ReadAsStringAsync();

                return json;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public static async Task<TerminalConfig?> GetTerminalConfigAsync()
        {
            var json = await GetJsonAsync("http://justcalltheguy.westeurope.cloudapp.azure.com/api/terminalconfig?code=Ocebxtg1excWosFez5rWMtNp3ZsmIzSFQ0XhqtrfHlMuAzFuQ0OGhA==&accountid=692803787");
            if (json != null)
                return JsonConvert.DeserializeObject<TerminalConfig>(json);
            else
                return null;
        }
    }
}
