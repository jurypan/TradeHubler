using System.Text.Json;
using JCTG.Models;
using Websocket.Client;

namespace JCTG.Client
{
    public class AzurePubSubClient(WebsocketClient client)
    {
        private readonly WebsocketClient? _client = client;

        public event Action<TradingviewSignal> SubscribeOnTradingviewSignal;

        public async Task StartAsync()
        {
            // Do null reference check
            if (_client != null)
            {
                // Disable the auto disconnect and reconnect because the sample would like the _client to stay online even no data comes in
                _client.ReconnectTimeout = null;

                // Enable the message receive
                _client.MessageReceived.Subscribe(msg =>
                {
                    if (msg != null && msg.Text != null)
                    {
                        using (var document = JsonDocument.Parse(msg.Text))
                        {
                            var type = document.RootElement.GetProperty("type").GetString();
                            var from = document.RootElement.GetProperty("from").GetString();
                            if (type == Constants.WebsocketMessageType_Message && from == Constants.WebsocketMessageFrom_Server)
                            {
                                var data = document.RootElement.GetProperty("data");
                                if (data.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("typeName", out var typeNameProperty))
                                {
                                    if (typeNameProperty.GetString() == "MetatraderMessage")
                                    {
                                        var message = data.Deserialize<TradingviewSignal>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                        if (message != null)
                                        {
                                            SubscribeOnTradingviewSignal?.Invoke(message);
                                        }  
                                    }
                                }
                            }
                        }
                    }
                });

                // Start the web socket
                await _client.Start();
            }
        }

        public async Task<bool> StopAsync() 
        {
            if (_client != null) 
            {
                return await _client.StopOrFail(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "shut down");
            }
            return false;
        }
    }
}
