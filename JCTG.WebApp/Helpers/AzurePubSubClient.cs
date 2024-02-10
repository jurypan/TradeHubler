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

        public void ListeningToServer()
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
                            // INit
                            var type = document.RootElement.GetProperty("type").GetString();
                            var from = document.RootElement.GetProperty("from").GetString();

                            // If comes from metatrader
                            if (from == Constants.WebsocketMessageFrom_Metatrader)
                            {
                                var data = document.RootElement.GetProperty("data");
                                if (data.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("TypeName", out var typeNameProperty))
                                {
                                    if (type == Constants.WebsocketMessageType_OnOrderCreateEvent)
                                    {
                                        var @event = data.Deserialize<OnOrderCreateEvent>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                        if (@event != null)
                                            OnOrderCreateEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnOrderUpdateEvent)
                                    {
                                        var @event = data.Deserialize<OnOrderUpdateEvent>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                        if (@event != null)
                                            OnOrderUpdateEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnOrderCloseEvent)
                                    {
                                        var @event = data.Deserialize<OnOrderCloseEvent>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                        if (@event != null)
                                            OnOrderCloseEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnLogEvent)
                                    {
                                        var @event = data.Deserialize<OnLogEvent>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                        if (@event != null)
                                            OnLogEvent?.Invoke(@event);
                                    }
                                    else if (type == Constants.WebsocketMessageType_OnOrderAutoMoveSlToBeEvent)
                                    {
                                        var @event = data.Deserialize<OnOrderAutoMoveSlToBeEvent>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                        if (@event != null)
                                            OnOrderAutoMoveSlToBeEvent?.Invoke(@event);
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
