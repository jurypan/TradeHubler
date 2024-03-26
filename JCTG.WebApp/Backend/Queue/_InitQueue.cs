namespace JCTG.WebApp.Backend.Queue
{
    public static class InitQueue
    {
        public static IServiceCollection AddAzureQueueServer(this IServiceCollection service)
        {
            // Null reference check
            ArgumentNullException.ThrowIfNull(service);

            // Add as transitent
            service.AddTransient<AzureQueueClient>();

            // Return
            return service;
        }
    }
}
