namespace JCTG.WebApp.Frontend.Components.Tradingview;

public class Marker
{
    public DateTime Time { get; set; }
    public MarkerDirection Direction { get; set; } = MarkerDirection.Buy;
    public MarkerPosition Position { get; set; } = MarkerPosition.AboveBar;
    public MarkerShape Shape { get; set; } = MarkerShape.Circle;
    public string? Text { get; set; }
    public required string Color { get; set; } = "#228B22";
    public string? Id { get; set; }
    public int? Size { get; set; }


    public enum MarkerDirection
    {
        Buy = 1,
        Sell = 2,
    }

    public enum MarkerShape
    {
        ArrowUp = 1,
        ArrowDown = 2,
        Circle = 3,
        Square = 4,
    }

    public enum MarkerPosition
    {
        AboveBar = 1,
        BelowBar = 2,
        InBar = 2,
    }
}