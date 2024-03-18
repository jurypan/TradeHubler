using System.Text.Json;
using JCTG.Command;
using Websocket.Client;

namespace JCTG.Client
{
    public class AzurePubSubClient(WebsocketClient client)
    {
        private readonly WebsocketClient? _client = client;

        public event Action<OnSendTradingviewSignalCommand> OnSendTradingviewSignalCommand;
        public event Action<OnSendGetHistoricalBarDataCommand> OnSendGetHistoricalBarDataCommand;

        public async Task ListeningToServerAsync()
        {
            // Do null reference check
            if (_client != null)
            {
                // Disable the auto disconnect and reconnect because the sample would like the _client to stay online even no data comes in
                _client.ReconnectTimeout = null;
                _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(10);
                _client.ReconnectionHappened.Subscribe(OnReconnection);
                _client.DisconnectionHappened.Subscribe(OnDisconnect);

                // Enable the message receive
                _client.MessageReceived.Subscribe(msg =>
                {
                    if (msg != null && msg.Text != null)
                    {
                        using (var document = JsonDocument.Parse(msg.Text))
                        {
                            // Somewhere in your method or constructor
                            var jsonSerializerOptions = new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = null
                            };

                            // Init
                            var type = document.RootElement.GetProperty("Type").GetString();
                            var from = document.RootElement.GetProperty("From").GetString();

                            // If comes from the server
                            if (from == Constants.WebsocketMessageFrom_Server)
                            {
                                var data = document.RootElement.GetProperty("Data");
                                if (data.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("TypeName", out var typeNameProperty))
                                {
                                    if (type == Constants.WebsocketMessageType_OnSendTradingviewSignalCommand)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnSendTradingviewSignalCommand>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnSendTradingviewSignalCommand?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnSendGetHistoricalBarDataCommand)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnSendGetHistoricalBarDataCommand>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnSendGetHistoricalBarDataCommand?.Invoke(@event);
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

        private void OnReconnection(ReconnectionInfo info)
        {
            Console.WriteLine($"INFO : { DateTime.UtcNow} / Reconnection happened, type: {info.Type}");
        }

        private void OnDisconnect(DisconnectionInfo info)
        {
            // Log
            Console.WriteLine($"INFO : {DateTime.UtcNow} / Disconnection happened, type: {info.Type}, reason: {info.CloseStatus}");

            // Reconnect
            Task.Run(async () => await client.Reconnect());
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
