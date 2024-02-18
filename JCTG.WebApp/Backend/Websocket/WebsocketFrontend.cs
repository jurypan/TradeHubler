using JCTG.Events;

namespace JCTG.WebApp.Backend.Websocket;

public class WebsocketFrontend(AzurePubSubServer server) : IAsyncDisposable
{
    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<WebsocketFrontend>();

    public event Action<OnGetHistoricalBarDataEvent>? OnGetHistoricalBarDataEvent;

    public async Task RunAsync()
    {
        // Log
        _logger.Debug($"Init frontend event handlers");

        // Delegate the event from the Azure PubSub class, to the frontend page
        server.OnGetHistoricalBarDataEvent += (onGetHistoricalBarDataEvent) =>
        {
            OnGetHistoricalBarDataEvent?.Invoke(onGetHistoricalBarDataEvent);
        };

       // Listen to the server
       if(!server.IsStarted)
            await server.ListeningToServerAsync();
        
        // Log
        _logger.Information($"Websocket frontend started");
    }


    public void SubscribeToOnGetHistoricalBarDataEvent(Action<OnGetHistoricalBarDataEvent> handler)
    {
        // Unsubscribe all existing handlers to ensure there's only one subscriber
        if (OnGetHistoricalBarDataEvent != null)
        {
            foreach (Delegate existingHandler in OnGetHistoricalBarDataEvent.GetInvocationList())
            {
                OnGetHistoricalBarDataEvent -= (Action<OnGetHistoricalBarDataEvent>)existingHandler;
            }
        }

        // Subscribe the new handler
        OnGetHistoricalBarDataEvent += handler;
    }


    public async ValueTask DisposeAsync()
    {
        if (server.IsStarted)
            await server.StopListeningToServerAsync();
    }
}
