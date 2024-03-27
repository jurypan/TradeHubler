using Azure.Storage.Queues;
using JCTG.Command;
using Newtonsoft.Json;
using System.Net;

namespace JCTG.WebApp.Backend.Queue
{
    public class AzureQueueClient
    {
        private readonly IConfiguration _configuration;
        private readonly string? _connectionString;

        public AzureQueueClient(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("AZURE_QUEUE_CONNECTIONSTRING");
            if (_connectionString == null)
                throw new ArgumentNullException(_connectionString);
        }

        private QueueClient GetClient(int accountId)
        {
            return new QueueClient(_connectionString, "account" + accountId);
        }

        private async Task<string?> SendMessageAsync<T>(int accountId, T cmd, string messageType, string typeName)
        {
            try
            {
                var client = GetClient(accountId);
                var message = new WebsocketMessage<T>
                {
                    Data = cmd,
                    DataType = Constants.QueueMessageDatatype_JSON,
                    From = Constants.QueueMessageFrom_Server,
                    Type = messageType,
                    TypeName = typeName,
                };
                var serializedMessage = JsonConvert.SerializeObject(message, new JsonSerializerSettings { ContractResolver = new IgnoreJsonPropertyContractResolver() });
                var response = await client.SendMessageAsync(serializedMessage);
                return response.Value.MessageId;
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
            return await SendMessageAsync(accountId, cmd, Constants.QueueMessageType_OnSendTradingviewSignalCommand, nameof(OnSendTradingviewSignalCommand));
        }
    }
}
