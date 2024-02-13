namespace JCTG.Models
{
    public class Deal
    {
        public bool IsMT4 { get; set; }
        public int Magic { get; set; }
        public string Symbol { get; set; }
        public double Lots { get; set; }
        public string Type { get; set; }
        public string Entry { get; set; } // In MT4 is always "trade"
        public double Pnl { get; set; }
        public double Commission { get; set; }
        public double Swap { get; set; }
        public string Comment { get; set; }
    }
}
