using System.Text.Json;
using JCTG.Events;
using Websocket.Client;

namespace JCTG.Client
{
    public class AzurePubSubClient(WebsocketClient client)
    {
        private readonly WebsocketClient? _client = client;

        public event Action<OnReceivingTradingviewSignalEvent> OnReceivingTradingviewSignalEvent;

        public async Task ListeningToServerAsync()
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
                            // Init
                            var type = document.RootElement.GetProperty("Type").GetString();
                            var from = document.RootElement.GetProperty("From").GetString();

                            // If comes from the server
                            if (from == Constants.WebsocketMessageFrom_Server)
                            {
                                var data = document.RootElement.GetProperty("Data");
                                if (data.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("TypeName", out var typeNameProperty))
                                {
                                    if (type == Constants.WebsocketMessageType_OnTradingviewSignalEvent)
                                    {
                                        var @event = data.Deserialize<OnReceivingTradingviewSignalEvent>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                        if (@event != null)
                                            OnReceivingTradingviewSignalEvent?.Invoke(@event);
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

        public async Task<bool> StopListeningToServerAsync() 
        {
            if (_client != null) 
            {
                return await _client.StopOrFail(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "shut down");
            }
            return false;
        }
    }
}
