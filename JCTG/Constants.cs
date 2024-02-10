namespace JCTG
{
    public class Constants
    {
        public static string WebsocketMessageType_Message = "message";
        public static string WebsocketMessageFrom_Server = "server";
        public static string WebsocketMessageFrom_Metatrader = "metatrader";
        public static string WebsocketMessageDatatype_JSON = "json";


        // Client
        public static string WebsocketMessageType_OnOrderCreateEvent = "onordercreateevent";
        public static string WebsocketMessageType_OnOrderUpdateEvent = "onorderupdateevent";
        public static string WebsocketMessageType_OnOrderCloseEvent = "onordercloseevent";
        public static string WebsocketMessageType_OnLogEvent = "onlogevent";
    }
}
