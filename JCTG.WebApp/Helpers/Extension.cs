using Azure.Messaging.WebPubSub;
using JCTG.WebApp.Helpers;
using Websocket.Client;

namespace JCTG.WebApp
{
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
            var pubSub = new AzurePubSubClient(new WebsocketClient(url));

            // Add as singleton
            service.AddSingleton(pubSub);

            // Add as transitent
            service.AddTransient<WebsocketService>();

            // Return
            return service;
        }

        public static IServiceCollection AddAzurePubSubServer(this IServiceCollection service)
        {
            // Null reference check
            ArgumentNullException.ThrowIfNull(service);

            // Add as transitent
            service.AddTransient<AzurePubSubServer>();

            // Return
            return service;
        }
    }
}
