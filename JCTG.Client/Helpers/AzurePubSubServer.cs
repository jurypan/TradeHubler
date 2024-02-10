using Azure.Messaging.WebPubSub;
using JCTG.Models;
using Newtonsoft.Json;

namespace JCTG.Client
{
    public class AzurePubSubServer
    {
        private readonly WebPubSubServiceClient _serviceClient;

        public AzurePubSubServer(long accountId) 
        {
            // Init Azure Web PubSub
            _serviceClient = new WebPubSubServiceClient("Endpoint=https://justcalltheguy.webpubsub.azure.com;AccessKey=BdxAvvoxX7+nkCq/lQDNe2LAy41lwDfJD8bCPiNuY/k=;Version=1.0;", "client" + accountId.ToString());
        }


        public async Task<string> SendLogAsync(Log log) 
        {
            if(_serviceClient != null) 
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<Log>()
                {
                    Data = log,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_Message,
                    TypeName = nameof(Log),
                }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }

        public async Task<string> SendTradeJournalAsync(TradeJournal tradeJournal)
        {
            if (_serviceClient != null)
            {
                var resp = await _serviceClient.SendToAllAsync(JsonConvert.SerializeObject(new WebsocketMessage<TradeJournal>()
                {
                    Data = tradeJournal,
                    DataType = Constants.WebsocketMessageDatatype_JSON,
                    From = Constants.WebsocketMessageFrom_Metatrader,
                    Type = Constants.WebsocketMessageType_Message,
                    TypeName = nameof(TradeJournal),
                }), Azure.Core.ContentType.ApplicationJson);

                return resp.ClientRequestId;
            }
            return "0";
        }
    }
}
