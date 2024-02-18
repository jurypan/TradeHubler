using Azure.Messaging.WebPubSub;
using JCTG.Command;
using Newtonsoft.Json;

namespace JCTG.WebApp.Backend.Websocket;

public class AzurePubSubClient
{
    private readonly IConfiguration _configuration;
    private readonly string? _connectionString;

    public AzurePubSubClient(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("AZURE_PUBSUB_CONNECTIONSTRING");
        if(_connectionString == null)
            throw new ArgumentNullException(_connectionString);
    }

    private WebPubSubServiceClient GetClient(int accountId)
    {
        return new WebPubSubServiceClient(_connectionString, "account" + accountId);
    }

    private async Task<string?> SendMessageAsync<T>(int accountId, T cmd, string messageType, string typeName)
    {
        try
        {
            var client = GetClient(accountId);
            var message = new WebsocketMessage<T>
            {
                Data = cmd,
                DataType = Constants.WebsocketMessageDatatype_JSON,
                From = Constants.WebsocketMessageFrom_Server,
                Type = messageType,
                TypeName = typeName,
            };
            var serializedMessage = JsonConvert.SerializeObject(message, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() });
            var response = await client.SendToAllAsync(serializedMessage, Azure.Core.ContentType.ApplicationJson);
            return response.ClientRequestId;
        }
        catch (Exception ex)
        {
            // Consider logging the exception
            // Decide how you want to handle errors: return null, a specific error code, or throw
            return null; // or "Error" or throw new Exception("Message sending failed", ex);
        }
    }

    public async Task<string?> SendOnTradingviewSignalCommandAsync(int accountId, OnSendTradingviewSignalCommand cmd)
    {
        return await SendMessageAsync(accountId, cmd, Constants.WebsocketMessageType_OnSendTradingviewSignalCommand, nameof(OnSendTradingviewSignalCommand));
    }

    public async Task<string?> SendGetHistoricalBarDataAsync(int accountId, OnSendGetHistoricalBarDataCommand cmd)
    {
        return await SendMessageAsync(accountId, cmd, Constants.WebsocketMessageType_OnSendGetHistoricalBarDataCommand, nameof(OnSendGetHistoricalBarDataCommand));
    }

    public async Task<string?> SendStartListeningToTicksCommand(int accountId, OnSendStartListeningToTicksCommand cmd)
    {
        return await SendMessageAsync(accountId, cmd, Constants.WebsocketMessageType_OnSendStartListeningToTicksCommand, nameof(OnSendStartListeningToTicksCommand));
    }

    public async Task<string?> SendStopListeningToTicksCommand(int accountId, OnSendStopListeningToTicksCommand cmd)
    {
        return await SendMessageAsync(accountId, cmd, Constants.WebsocketMessageType_OnSendStopListeningToTicksCommand, nameof(OnSendStopListeningToTicksCommand));
    }
}