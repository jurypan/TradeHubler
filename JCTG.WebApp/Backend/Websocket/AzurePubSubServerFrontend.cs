using JCTG.Events;
using System.Text.Json;
using Websocket.Client;

namespace JCTG.WebApp.Backend.Websocket;

public class AzurePubSubServerFrontend(WebsocketClient client)
{
    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<AzurePubSubServerFrontend>();

    public event Action<OnGetHistoricalBarDataEvent>? OnGetHistoricalBarDataEvent;


    public async Task ListeningToServerAsync()
    {
        // Log
        _logger.Debug($"Init client");

        // Do null reference check
        if (client != null)
        {
            // Disable the auto disconnect and reconnect because the sample would like the client to stay online even no data comes in
            client.ReconnectTimeout = null;

            // Enable the event receive
            client.MessageReceived.Subscribe(msg =>
            {
                if (msg != null && msg.Text != null)
                {
                    using var document = JsonDocument.Parse(msg.Text);

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
                            if (type == Constants.WebsocketMessageType_OnGetHistoricalBarDataEvent)
                            {
                                var @event = JsonSerializer.Deserialize<OnGetHistoricalBarDataEvent>(data.GetRawText(), jsonSerializerOptions);
                                if (@event != null)
                                    OnGetHistoricalBarDataEvent?.Invoke(@event);
                            }
                        }
                    }
                }
            });

            // Start the web socket
            if (!client.IsStarted)
                await client.Start();
        }
    }

    public async Task StopListeningToServerAsync() 
    {
        if (client != null) 
        {
            if (client.IsStarted)
                await client.StopOrFail(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "shut down");
        }
    }
}
