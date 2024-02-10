using JCTG.Models;
using System.Text.Json;
using Websocket.Client;

namespace JCTG.WebApp
{
    public class AzurePubSubClient(WebsocketClient client)
    {
        private readonly WebsocketClient? _client = client;

        public event Action<Log>? SubscribeOnLog;
        public event Action<TradeJournal>? SubscribeOnTradeJournal;

        public void ListeningToServer()
        {
            // Do null reference check
            if (_client != null)
            {
                // Disable the auto disconnect and reconnect because the sample would like the _client to stay online even no data comes in
                _client.ReconnectTimeout = null;

                // Enable the message receive
                _ = _client.MessageReceived.Subscribe(msg =>
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
                                    if (typeNameProperty.GetString() == "Log")
                                    {
                                        var message = data.Deserialize<Log>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                        if (message != null)
                                        {
                                            SubscribeOnLog?.Invoke(message);
                                        }
                                    }
                                    else if (typeNameProperty.GetString() == "TradeJournal")
                                    {
                                        var message = data.Deserialize<TradeJournal>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                        if (message != null)
                                        {
                                            SubscribeOnTradeJournal?.Invoke(message);
                                        }
                                    }
                                }
                            }
                        }
                    }
                });

                // Start the web socket
                Task.Run(_client.Start);
            }
        }

        public void StopListeningToServer() 
        {
            if (_client != null) 
            {
                Task.Run(async () => await _client.StopOrFail(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "shut down"));
            }
        }
    }
}
