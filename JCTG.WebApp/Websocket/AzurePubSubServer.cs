using Azure.Messaging.WebPubSub;
using JCTG.Events;
using Newtonsoft.Json;

namespace JCTG.WebApp
{
    public class AzurePubSubServer
    {
        public async Task<string> SendOnTradingviewSignalEventAsync(int accountId, OnReceivingTradingviewSignalEvent signal) 
        {
            // Get client from the account
            var _client = new WebPubSubServiceClient("Endpoint=https://justcalltheguy.webpubsub.azure.com;AccessKey=BdxAvvoxX7+nkCq/lQDNe2LAy41lwDfJD8bCPiNuY/k=;Version=1.0;", "account" + accountId);

            if (_client != null) 
            {
                var resp = await _client.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<OnReceivingTradingviewSignalEvent>()
                {
                    Data = signal,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Server,
                    Type = Constants.WebsocketMessageType_OnTradingviewSignalEvent,
                    TypeName = nameof(OnReceivingTradingviewSignalEvent),
                }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }
    }
}
