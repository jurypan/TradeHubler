namespace JCTG.Client
{
    public class BarData
    {
        public required string Timeframe { get; set; }
        public DateTime Time { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public int TickVolume { get; set; }
    }
}
