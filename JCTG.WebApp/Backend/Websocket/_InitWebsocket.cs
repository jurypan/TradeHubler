namespace JCTG.WebApp.Backend.Websocket;

public static class _InitWebsocket
{

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
