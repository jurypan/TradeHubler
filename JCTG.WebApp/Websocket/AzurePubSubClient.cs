using JCTG.Events;
using System.Text.Json;
using Websocket.Client;

namespace JCTG.WebApp
{
    public class AzurePubSubClient(WebsocketClient client)
    {
        private readonly WebsocketClient? _client = client;

        public event Action<OnLogEvent>? OnLogEvent;
        public event Action<OnOrderCreateEvent>? OnOrderCreateEvent;
        public event Action<OnOrderUpdateEvent>? OnOrderUpdateEvent;
        public event Action<OnOrderCloseEvent>? OnOrderCloseEvent;
        public event Action<OnOrderAutoMoveSlToBeEvent>? OnOrderAutoMoveSlToBeEvent;
        public event Action<OnItsTimeToCloseTheOrderEvent>? OnItsTimeToCloseTheOrderEvent;
        public event Action<OnDealEvent>? OnDealEvent;

        public async Task ListeningToServerAsync()
        {
            // Do null reference check
            if (_client != null)
            {
                // Disable the auto disconnect and reconnect because the sample would like the _client to stay online even no data comes in
                _client.ReconnectTimeout = null;

                // Enable the event receive
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

                            // INit
                            var type = document.RootElement.GetProperty("Type").GetString();
                            var from = document.RootElement.GetProperty("From").GetString();

                            // If comes from metatrader
                            if (from == Constants.WebsocketMessageFrom_Metatrader)
                            {
                                var data = document.RootElement.GetProperty("Data");
                                if (data.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("TypeName", out var typeNameProperty))
                                {
                                    if (type == Constants.WebsocketMessageType_OnOrderCreateEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnOrderCreateEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnOrderCreateEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnOrderUpdateEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnOrderUpdateEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnOrderUpdateEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnOrderCloseEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnOrderCloseEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnOrderCloseEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnLogEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnLogEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnLogEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnOrderAutoMoveSlToBeEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnOrderAutoMoveSlToBeEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnOrderAutoMoveSlToBeEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnItsTimeToCloseTheOrderEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnItsTimeToCloseTheOrderEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnItsTimeToCloseTheOrderEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnDealEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnDealEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnDealEvent?.Invoke(@event);
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

        public async Task StopListeningToServerAsync() 
        {
            if (_client != null) 
            {
                await _client.StopOrFail(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "shut down");
            }
        }
    }
}
