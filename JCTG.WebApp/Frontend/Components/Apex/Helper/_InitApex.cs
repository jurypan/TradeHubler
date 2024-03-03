namespace JCTG.WebApp.Frontend.Components.Apex;

public static class _InitApex
{
    public static IServiceCollection InitApex(this IServiceCollection service)
    {
        // Null reference check
        ArgumentNullException.ThrowIfNull(service);

        // Add as transitent
        service.AddTransient<JSInteropt>();

        // Return
        return service;
    }
}
