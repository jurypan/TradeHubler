using Azure.Messaging.WebPubSub;
using JCTG.Models;
using Newtonsoft.Json;

namespace JCTG.WebApp
{
    public class AzurePubSubServer
    {
        private readonly WebPubSubServiceClient _serviceClient;

        public AzurePubSubServer() 
        {
            // Init Azure Web PubSub
            _serviceClient = new WebPubSubServiceClient("Endpoint=https://justcalltheguy.webpubsub.azure.com;AccessKey=BdxAvvoxX7+nkCq/lQDNe2LAy41lwDfJD8bCPiNuY/k=;Version=1.0;", "server");
        }


        public async Task<string> SendTradingviewSignalAsync(TradingviewSignal signal) 
        {
            if(_serviceClient != null) 
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<TradingviewSignal>()
                {
                    Data = signal,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Server,
                    Type = Constants.WebsocketMessageType_Message,
                    TypeName = nameof(TradingviewSignal),
                }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }
    }
}
