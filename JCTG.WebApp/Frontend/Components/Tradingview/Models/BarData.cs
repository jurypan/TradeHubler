namespace JCTG.WebApp.Frontend.Components.Tradingview;

/// <summary>
/// Used to display bar chart
/// </summary>
public class BarData
{
    public DateTime Time { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public string? Color { get; set; }
}
