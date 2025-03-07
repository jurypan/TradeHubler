﻿using JCTG.Events;
using JCTG.Models;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

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

        public static async Task<string?> PostJsonAsync(string url, string jsonData)
        {
            try
            {
                // Serialize the data to JSON and prepare it for sending
                HttpContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                // Asynchronously call the URI specified, posting the data, and await the response.
                // Make sure to call .EnsureSuccessStatusCode() to throw an exception if the response is an error.
                HttpResponseMessage response = await _httpClient.PostAsync(url, content);
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

        public static async Task PushDataAsync(string url, string jsonData)
        {
            // Create the content to send in the request
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            // StartCheckTimeAndExecuteOnceDaily the task of sending the request without awaiting it
            _ = _httpClient.PostAsync(url, content);

            //await Task.Yield();
        }

        public static async Task<TerminalConfig?> GetTerminalConfigAsync()
        {
            var json = await GetJsonAsync("https://app.tradehubler.com:444/api/terminalconfig?code=Ocebxtg1excWosFez5rWMtNp3ZsmIzSFQ0XhqtrfHlMuAzFuQ0OGhA==&accountid=692803787");
            if (json != null)
                return JsonConvert.DeserializeObject<TerminalConfig>(json);
            else
                return null;
        }

        public static async Task OnOrderCreatedEvent(OnOrderCreatedEvent model)
        {
            await PostJsonAsync("https://app.tradehubler.com:444/api/OnOrderCreatedEvent", JsonConvert.SerializeObject(model));
        }

        public static async Task OnOrderUpdatedEvent(OnOrderUpdatedEvent model)
        {
            await PostJsonAsync("https://app.tradehubler.com:444/api/OnOrderUpdatedEvent", JsonConvert.SerializeObject(model));
        }

        public static async Task OnOrderClosedEvent(OnOrderClosedEvent model)
        {
            await PostJsonAsync("https://app.tradehubler.com:444/api/OnOrderClosedEvent", JsonConvert.SerializeObject(model));
        }

        public static async Task OnOrderAutoMoveSlToBeEvent(OnOrderAutoMoveSlToBeEvent model)
        {
            await PostJsonAsync("https://app.tradehubler.com:444/api/ModifyOrderByMoveSLtoBE", JsonConvert.SerializeObject(model));
        }

        public static async Task OnItsTimeToCloseTheOrderEvent(OnItsTimeToCloseTheOrderEvent model)
        {
            await PostJsonAsync("https://app.tradehubler.com:444/api/OnItsTimeToCloseTheOrderEvent", JsonConvert.SerializeObject(model));
        }

        public static async Task OnLogEvent(OnLogEvent model)
        {
            // await PushDataAsync("https://app.tradehubler.com:444/api/OnLogEvent", JsonConvert.SerializeObject(model));
            await PostJsonAsync("https://app.tradehubler.com:444/api/OnLogEvent", JsonConvert.SerializeObject(model));
        }

        public static async Task OnDealCreatedEvent(OnDealCreatedEvent model)
        {
            await PostJsonAsync("https://app.tradehubler.com:444/api/OnDealCreatedEvent", JsonConvert.SerializeObject(model));
        }

        public static async Task OnAccountInfoChangedEvent(OnAccountInfoChangedEvent model)
        {
            await PostJsonAsync("https://app.tradehubler.com:444/api/OnAccountInfoChangedEvent", JsonConvert.SerializeObject(model));
        }

        public static async Task OnMarketAbstentionEvent(OnMarketAbstentionEvent model)
        {
            await PostJsonAsync("https://app.tradehubler.com:444/api/OnMarketAbstentionEvent", JsonConvert.SerializeObject(model));
        }

        public static async Task OnMarketAbstentionEvent(OnMetatraderMarketAbstentionEvent model)
        {
            await PostJsonAsync("https://app.tradehubler.com:444/api/OnMetatraderMarketAbstentionEvent", JsonConvert.SerializeObject(model));
        }
    }
}
