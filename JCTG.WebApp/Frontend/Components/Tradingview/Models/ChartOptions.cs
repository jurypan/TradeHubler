namespace JCTG.WebApp.Frontend.Components.Tradingview;

public class ChartOptions
{
    public int Width { get; set; } = -1;
    public int Height { get; set; } = 300;

    // -- Layout
    public string LayoutBackgroundColor { get; set; } = "#fff";
    public string LayoutTextColor { get; set; } = "rgba(0, 0, 0, 0.9)";

    // -- Grid
    public string VertLinesColor { get; set; } = "rgba(197, 203, 206, 0.5)";
    public string HorzLinesColor { get; set; } = "rgba(197, 203, 206, 0.5)";

    // -- RightPriceScale
    public string RightPriceScaleBorderColor { get; set; } = "rgba(197, 203, 206, 0.8)";
    public int RightPriceScaleDecimalPrecision { get; set; } = 2;

    // -- Timescale
    public string TimeScaleBorderColor { get; set; } = "rgba(197, 203, 206, 0.8)";
    public bool TimeScaleTimeVisible { get; set; } = true;
    public bool TimeScaleSecondsVisible { get; set; } = false;
    // -- Volume
    public string VolumeColorUp { get; set; } = "rgba(0, 150, 136, 0.8)";
    public string VolumeColorDown { get; set; } = "rgba(255,82,82, 0.8)";
}