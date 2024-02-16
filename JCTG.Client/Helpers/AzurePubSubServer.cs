using Azure.Messaging.WebPubSub;
using JCTG.Events;
using Newtonsoft.Json;

namespace JCTG.Client
{
    public class AzurePubSubServer
    {
        private readonly WebPubSubServiceClient _serviceClient;

        public AzurePubSubServer() 
        {
            // Init Azure Web PubSub
            _serviceClient = new WebPubSubServiceClient("Endpoint=https://justcalltheguy.webpubsub.azure.com;AccessKey=BdxAvvoxX7+nkCq/lQDNe2LAy41lwDfJD8bCPiNuY/k=;Version=1.0;", "server");
        }

        public async Task<string> SendOnOrderCreateEventAsync(OnOrderCreatedEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnOrderCreatedEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnOrderCreatedEvent,
                    TypeName = nameof(OnOrderCreatedEvent),
                }, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendOnOrderUpdateEventAsync(OnOrderUpdatedEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnOrderUpdatedEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnOrderUpdatedEvent,
                    TypeName = nameof(OnOrderUpdatedEvent),
                }, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendOnOrderCloseEventAsync(OnOrderClosedEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnOrderClosedEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnOrderClosedEvent,
                    TypeName = nameof(OnOrderClosedEvent),
                }, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendOnLogEventAsync(OnLogEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnLogEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnLogEvent,
                    TypeName = nameof(OnLogEvent),
                }, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendOnOrderAutoMoveSlToBeEventAsync(OnOrderAutoMoveSlToBeEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnOrderAutoMoveSlToBeEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnOrderAutoMoveSlToBeEvent,
                    TypeName = nameof(OnOrderAutoMoveSlToBeEvent),
                }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendOnItsTimeToCloseTheOrderEventAsync(OnItsTimeToCloseTheOrderEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnItsTimeToCloseTheOrderEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnItsTimeToCloseTheOrderEvent,
                    TypeName = nameof(OnItsTimeToCloseTheOrderEvent),
                }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendOnTradeEventAsync(OnDealCreatedEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnDealCreatedEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnDealCreatedEvent,
                    TypeName = nameof(OnDealCreatedEvent),
                }, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendOnAccountInfoChangedAsync(OnAccountInfoChangedEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnAccountInfoChangedEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnAccountInfoChangedEvent,
                    TypeName = nameof(OnAccountInfoChangedEvent),
                }, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }
    }
}
