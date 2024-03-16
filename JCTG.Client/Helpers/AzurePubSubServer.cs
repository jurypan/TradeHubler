using Azure.Messaging.WebPubSub;
using JCTG.Entity;
using JCTG.Events;
using Newtonsoft.Json;

namespace JCTG.Client;

public class AzurePubSubServer
{
    private readonly WebPubSubServiceClient _serviceClient;

    public AzurePubSubServer()
    {
        // Initialize Azure Web PubSub with the hardcoded connection string
        _serviceClient = new WebPubSubServiceClient("Endpoint=https://justcalltheguy.webpubsub.azure.com;AccessKey=BdxAvvoxX7+nkCq/lQDNe2LAy41lwDfJD8bCPiNuY/k=;Version=1.0;", "server");
    }

    private async Task<string?> SendMessageAsync<T>(T @event, string eventType, string typeName)
    {
        try
        {
            var message = new WebsocketMessage<T>
            {
                Data = @event,
                DataType = Constants.WebsocketMessageDatatype_JSON,
                From = Constants.WebsocketMessageFrom_Metatrader,
                Type = eventType,
                TypeName = typeName,
            };
            var serializedMessage = JsonConvert.SerializeObject(message, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() });
            var response = await _serviceClient.SendToAllAsync(serializedMessage, Azure.Core.ContentType.ApplicationJson);
            return response.ClientRequestId;
        }
        catch (Exception ex)
        {
            // Consider logging the exception
            // Decide how you want to handle errors: return null, a specific error code, or throw
            return null; // or "Error" or throw new Exception("Message sending failed", ex);
        }
    }

    public Task<string?> SendOnOrderCreateEventAsync(OnOrderCreatedEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnOrderCreatedEvent, nameof(OnOrderCreatedEvent));
    }

    public Task<string?> SendOnOrderUpdateEventAsync(OnOrderUpdatedEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnOrderUpdatedEvent, nameof(OnOrderUpdatedEvent));
    }

    public Task<string?> SendOnOrderCloseEventAsync(OnOrderClosedEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnOrderClosedEvent, nameof(OnOrderClosedEvent));
    }

    public Task<string?> SendOnLogEventAsync(OnLogEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnLogEvent, nameof(OnLogEvent));
    }

    public Task<string?> SendOnMarketAbstentionEventAsync(OnMarketAbstentionEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnMarketAbstentionEvent, nameof(OnMarketAbstentionEvent));
    }

    public Task<string?> SendOnOrderAutoMoveSlToBeEventAsync(OnOrderAutoMoveSlToBeEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnOrderAutoMoveSlToBeEvent, nameof(OnOrderAutoMoveSlToBeEvent));
    }

    public Task<string?> SendOnItsTimeToCloseTheOrderEventAsync(OnItsTimeToCloseTheOrderEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnItsTimeToCloseTheOrderEvent, nameof(OnItsTimeToCloseTheOrderEvent));
    }

    public Task<string?> SendOnTradeEventAsync(OnDealCreatedEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnDealCreatedEvent, nameof(OnDealCreatedEvent));
    }

    public Task<string?> SendOnAccountInfoChangedAsync(OnAccountInfoChangedEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnAccountInfoChangedEvent, nameof(OnAccountInfoChangedEvent));
    }

    public Task<string?> SendOnGetHistoricalBarDataEventAsync(OnGetHistoricalBarDataEvent @event)
    {
        return SendMessageAsync(@event, Constants.WebsocketMessageType_OnGetHistoricalBarDataEvent, nameof(OnGetHistoricalBarDataEvent));
    }
}

