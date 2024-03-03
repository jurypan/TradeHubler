namespace JCTG.WebApp.Frontend.Components.Tradingview;

public static class _InitApex
{
    public static IServiceCollection InitTradingview(this IServiceCollection service)
    {
        // Null reference check
        ArgumentNullException.ThrowIfNull(service);

        // Add as transitent
        service.AddTransient<JSInteropt>();

        // Return
        return service;
    }
}
