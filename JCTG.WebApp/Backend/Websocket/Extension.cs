using Azure.Messaging.WebPubSub;
using Websocket.Client;

namespace JCTG.WebApp.Backend.Websocket;

public static class Extension
{
    public static IServiceCollection AddAzurePubSubClient(this IServiceCollection service, string? connectionString)
    {
        // Null reference check
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(connectionString);

        // Init
        var serviceClient = new WebPubSubServiceClient(connectionString, "server");
        var url = serviceClient.GetClientAccessUri();
        var pubSubBackend = new AzurePubSubServerBackend(new WebsocketClient(url));
        var pubSubFrontend = new AzurePubSubServerFrontend(new WebsocketClient(url));

        // Add as singleton
        service.AddSingleton(pubSubBackend);
        service.AddSingleton(pubSubFrontend);

        // Add as transitent
        service.AddTransient<WebsocketServer>();

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
