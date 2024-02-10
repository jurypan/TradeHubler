using System.ComponentModel;

namespace JCTG.Models
{
    public enum OrderType
    {
        [Description("buy")]
        Buy,

        [Description("sell")]
        Sell,

        [Description("buylimit")]
        BuyLimit,

        [Description("selllimit")]
        SellLimit,

        [Description("buystop")]
        BuyStop,

        [Description("sellstop")]
        SellStop
    }

    public class StringValueAttribute(string stringValue) : Attribute
    {
        public string StringValue { get; private set; } = stringValue;
    }
}
