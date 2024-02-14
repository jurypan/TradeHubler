using JCTG.Events;
using System.Text.Json;
using Websocket.Client;

namespace JCTG.WebApp
{
    public class AzurePubSubClient(WebsocketClient client)
    {
        private readonly WebsocketClient? _client = client;

        public event Action<OnLogEvent>? OnLogEvent;
        public event Action<OnOrderCreatedEvent>? OnOrderCreatedEvent;
        public event Action<OnOrderUpdatedEvent>? OnOrderUpdateEvent;
        public event Action<OnOrderClosedEvent>? OnOrderCloseEvent;
        public event Action<OnOrderAutoMoveSlToBeEvent>? OnOrderAutoMoveSlToBeEvent;
        public event Action<OnItsTimeToCloseTheOrderEvent>? OnItsTimeToCloseTheOrderEvent;
        public event Action<OnDealCreatedEvent>? OnDealCreatedEvent;

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
                                    if (type == Constants.WebsocketMessageType_OnOrderCreatedEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnOrderCreatedEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnOrderCreatedEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnOrderUpdatedEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnOrderUpdatedEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnOrderUpdateEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnOrderClosedEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnOrderClosedEvent>(data.GetRawText(), jsonSerializerOptions);
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
                                    else if (type == Constants.WebsocketMessageType_OnDealCreatedEvent)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnDealCreatedEvent>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                            OnDealCreatedEvent?.Invoke(@event);
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
