using Azure.Messaging.WebPubSub;
using Websocket.Client;

namespace JCTG.WebApp.Backend.Websocket;

public static class _InitWebsocket
{
    public static IServiceCollection AddAzurePubSubClient(this IServiceCollection service, string? connectionString)
    {
        // Null reference check
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(connectionString);

        // Init
        var serviceClient = new WebPubSubServiceClient(connectionString, "server");

        // Add as singleton
        service.AddSingleton(new AzurePubSubServer(new WebsocketClient(serviceClient.GetClientAccessUri())));

        // Add as transitent
        service.AddSingleton<WebsocketBackend>();
        service.AddSingleton<WebsocketFrontend>();

        // Return
        return service;
    }

    public static IServiceCollection AddAzurePubSubServer(this IServiceCollection service)
    {
        // Null reference check
        ArgumentNullException.ThrowIfNull(service);

        // Add as transitent
        service.AddTransient<AzurePubSubClient>();

        // Return
        return service;
    }
}
