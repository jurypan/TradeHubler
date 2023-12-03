﻿using System.ComponentModel;

namespace JCTG.Client
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

    public class StringValueAttribute : Attribute
    {
        public string StringValue { get; private set; }

        public StringValueAttribute(string stringValue)
        {
            StringValue = stringValue;
        }
    }
}
