namespace JCTG
{
    public class Constants
    {
        public static string WebsocketMessageDatatype_JSON = "json";

        // From Metatrader To Server
        public static string WebsocketMessageFrom_Metatrader = "Metatrader";
        public static string WebsocketMessageType_OnOrderCreateEvent = "OnOrderCreateEvent";
        public static string WebsocketMessageType_OnOrderUpdateEvent = "OnOrderUpdateEvent";
        public static string WebsocketMessageType_OnOrderCloseEvent = "OnOrderCloseEvent";
        public static string WebsocketMessageType_OnTradeEvent = "OnTradeEvent";
        public static string WebsocketMessageType_OnLogEvent = "OnLogEvent";
        public static string WebsocketMessageType_OnOrderAutoMoveSlToBeEvent = "_OnOrderAutoMoveSlToBeEvent";
        public static string WebsocketMessageType_OnItsTimeToCloseTheOrderEvent = "OnItsTimeToCloseTheOrderEvent";


        // From Server To Metatrader
        public static string WebsocketMessageFrom_Server = "Server";
        public static string WebsocketMessageType_OnTradingviewSignalEvent = "OnReceivingTradingviewSignalEvent";
    }
}
