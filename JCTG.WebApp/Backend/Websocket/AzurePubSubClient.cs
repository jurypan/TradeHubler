using Azure.Messaging.WebPubSub;
using JCTG.Events;
using Newtonsoft.Json;

namespace JCTG.WebApp.Backend.Websocket;

public class AzurePubSubClient(IConfiguration configuration)
{
    public async Task<string> SendOnTradingviewSignalEventAsync(int accountId, OnReceivingTradingviewSignalEvent signal) 
    {
        // Get client from the account
        var _client = new WebPubSubServiceClient(configuration.GetConnectionString("AZURE_PUBSUB_CONNECTIONSTRING"), "account" + accountId);

        if (_client != null) 
        {
            var resp = await _client.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnReceivingTradingviewSignalEvent>()
            {
                Data = signal,
                DataType = Constants.WebsocketMessageDatatype_JSON,
                From = Constants.WebsocketMessageFrom_Server,
                Type = Constants.WebsocketMessageType_OnTradingviewSignalEvent,
                TypeName = nameof(OnReceivingTradingviewSignalEvent),
            }, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() }), Azure.Core.ContentType.ApplicationJson);

            return resp.ClientRequestId;
        }
        return "0";
    }
}
