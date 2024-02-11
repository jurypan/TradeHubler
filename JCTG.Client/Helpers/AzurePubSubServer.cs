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

        public async Task<string> SendOnOrderCreateEventAsync(OnOrderCreateEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnOrderCreateEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnOrderCreateEvent,
                    TypeName = nameof(OnOrderCreateEvent),
                }, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendOnOrderUpdateEventAsync(OnOrderUpdateEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnOrderUpdateEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnOrderUpdateEvent,
                    TypeName = nameof(OnOrderUpdateEvent),
                }, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendOnOrderCloseEventAsync(OnOrderCloseEvent @event)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnOrderCloseEvent>()
                {
                    Data = @event,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_OnOrderCloseEvent,
                    TypeName = nameof(OnOrderCloseEvent),
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
    }
}
