namespace JCTG.WebApp.Frontend.Components.TradingviewOLD;
public class ChartData
{
    /// <summary>
    /// Fill this object with chart entry data such as Candle or PricePoint
    /// </summary>
    public required List<IChartEntry> ChartEntries { get; set; }


    /// <summary>
    /// Optional marker arrow to be displayed in addition to the primary chart data
    /// </summary>
    public List<Marker>? MarkerData { get; set; }


    [Obsolete(
        "Property CandleData has been deprecated. Please use ChartEntries instead. " +
        "You may need to use .Cast<IChartEntry>() to convert a list of List<Candle> into a List<IChartEntry>.",
        true)]
    public List<Candle> CandleData { get; set; }
}