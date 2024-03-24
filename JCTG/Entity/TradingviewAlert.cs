using System.ComponentModel.DataAnnotations;

namespace JCTG.Entity
{
    public class TradingviewAlert
    {
        public TradingviewAlert()
        {
            DateCreated = DateTime.UtcNow;
            Type = TradingviewAlertType.Entry;
            RawMessage = string.Empty;
        }

        [Key]
        public long ID { get; set; }
        public Signal Signal { get; set; }
        public long SignalID { get; set; }
        public DateTime DateCreated { get; set; }
        public TradingviewAlertType Type { get; set; }
        public required string RawMessage { get; set; }


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
        CancelOrder = 2,

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
    }
}
