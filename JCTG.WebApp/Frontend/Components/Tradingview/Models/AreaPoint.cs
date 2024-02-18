namespace JCTG.WebApp.Frontend.Components.Tradingview;
public class AreaPoint
{
    public DateTime Time { get; set; }
    public decimal Price { get; set; }

    public decimal DisplayPrice { get => Price; }
}