namespace JCTG.WebApp.Frontend.Components.Tradingview;
public class PricePoint : IChartEntry
{
    public DateTime Time { get; set; }
    public decimal Price { get; set; }
    public decimal Volume { get; set; }

    public decimal DisplayPrice { get => Price; }
}