namespace JCTG
{
    public class Constants
    {
        public static string WebsocketMessageDatatype_JSON = "json";

        // From Metatrader To Server
        public static string WebsocketMessageFrom_Metatrader = "Metatrader";
        public static string WebsocketMessageType_OnOrderCreatedEvent = "OnOrderCreatedEvent";
        public static string WebsocketMessageType_OnOrderUpdatedEvent = "OnOrderUpdatedEvent";
        public static string WebsocketMessageType_OnOrderClosedEvent = "OnOrderClosedEvent";
        public static string WebsocketMessageType_OnDealCreatedEvent = "OnDealCreatedEvent";
        public static string WebsocketMessageType_OnLogEvent = "OnLogEvent";
        public static string WebsocketMessageType_OnOrderAutoMoveSlToBeEvent = "_OnOrderAutoMoveSlToBeEvent";
        public static string WebsocketMessageType_OnItsTimeToCloseTheOrderEvent = "OnItsTimeToCloseTheOrderEvent";
        public static string WebsocketMessageType_OnAccountInfoChangedEvent = "OnAccountInfoChangedEvent";
        public static string WebsocketMessageType_OnGetHistoricalBarDataEvent = "OnGetHistoricalBarDataEvent";
        public static string WebsocketMessageType_OnMarketAbstentionEvent = "OnMarketAbstentionEvent";


        // From Server To Metatrader
        public static string WebsocketMessageFrom_Server = "Server";
        public static string WebsocketMessageType_OnSendTradingviewSignalCommand = "OnSendTradingviewSignalCommand";
        public static string WebsocketMessageType_OnSendGetHistoricalBarDataCommand = "OnSendGetHistoricalBarDataCommand";
        public static string WebsocketMessageType_OnSendStartListeningToTicksCommand = "OnSendStartListeningToTicksCommand";
        public static string WebsocketMessageType_OnSendStopListeningToTicksCommand = "OnSendStopListeningToTicksCommand";
    }
}
