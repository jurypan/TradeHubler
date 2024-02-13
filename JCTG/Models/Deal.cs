namespace JCTG.Models
{
    public class Deal
    {
        public int Magic { get; set; }
        public string Symbol { get; set; }
        public double Lots { get; set; }
        public string Type { get; set; }
        public string Entry { get; set; }
        public string DealTime { get; set; }
        public double DealPrice { get; set; }
        public double Pnl { get; set; }
        public double Commission { get; set; }
        public double Swap { get; set; }
        public string Comment { get; set; }
    }
}
