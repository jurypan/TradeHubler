namespace JCTG
{
    public class Constants
    {
        public static string WebsocketMessageDatatype_JSON = "json";

        // From Metatrader To Server
        public static string WebsocketMessageFrom_Metatrader = "metatrader";
        public static string WebsocketMessageType_OnOrderCreateEvent = "onordercreateevent";
        public static string WebsocketMessageType_OnOrderUpdateEvent = "onorderupdateevent";
        public static string WebsocketMessageType_OnOrderCloseEvent = "onordercloseevent";
        public static string WebsocketMessageType_OnLogEvent = "onlogevent";
        public static string WebsocketMessageType_OnOrderAutoMoveSlToBeEvent = "onorderautomovesltobeevent";
        public static string WebsocketMessageType_OnItsTimeToCloseTheOrderEvent = "onitstimetoclosetheorderevent";


        // From Server To Metatrader
        public static string WebsocketMessageFrom_Server = "server";
        public static string WebsocketMessageType_OnTradingviewSignalEvent = "ontradingviewsignalevent";
    }
}
