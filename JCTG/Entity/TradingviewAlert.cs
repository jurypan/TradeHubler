namespace JCTG.Entity
{
    public class TradingviewAlert
    {
        public long ID { get; set; }
        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public long TvMagic { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public TradingviewAlertType Type { get; set; } = TradingviewAlertType.Entry;
        public required string RawMessage { get; set; } = string.Empty;
        public TradingviewMethod Method { get; set; }


        public static TradingviewAlertType ParseTradingviewAlertTypeOrDefault(string value)
        {
            if (Enum.TryParse(value, true, out TradingviewAlertType result))
            {
                return result;
            }

            // Return Unknown if parsing fails
            return TradingviewAlertType.Unknown;
        }
    }

    public enum TradingviewAlertType
    {
        Unknown = -1,

        Entry = 0,
        MoveSlToBe = 1,
        Cancel = 2,

        TpHit = 10,
        SlHit = 11,
        BeHit = 12,


        Buy = 20,
        Sell = 21,
        BuyLimit = 22,
        SellLimit = 23,
        BuyStop = 24,
        SellStop = 25,

        CloseAll = 40,
        Close = 41,
    }

    public enum TradingviewMethod
    {
        Webhook = 1,
        Email = 2,
    }
}
