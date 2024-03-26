using Azure.Storage.Queues.Models;
using Azure.Storage.Queues;
using JCTG.Command;
using System.Text.Json;

namespace JCTG.Client
{
    public class AzureQueueClient
    {
        private readonly QueueClient queueClient;
        private readonly int accountId;

        public event Action<OnSendTradingviewSignalCommand>? OnSendTradingviewSignalCommand;


        public AzureQueueClient(string connectionString, int accountId)
        {
            // Set accountId
            this.accountId = accountId;

            // Initialize the QueueClient which will be used to interact with the queue
            queueClient = new QueueClient(connectionString, "account_" + accountId.ToString());

            // Create the queue if it doesn't exist
            queueClient.CreateIfNotExists();
        }

        protected virtual void OnSendTradingviewSignalCommandReceived(OnSendTradingviewSignalCommand cmd)
        {
            // Raise the event
            OnSendTradingviewSignalCommand?.Invoke(cmd);
        }

        public async Task ListeningToServerAsync()
        {
            Console.WriteLine("Listening for messages...");

            // Continuously listen for and process messages
            while (true)
            {
                // Receive messages from the queue
                QueueMessage[] messages = await queueClient.ReceiveMessagesAsync(maxMessages: 10, visibilityTimeout: TimeSpan.FromSeconds(30));

                foreach (var message in messages)
                {
                    // Do null reference check
                    if (message.Body != null)
                    {
                        using (var document = JsonDocument.Parse(message.Body.ToString()))
                        {
                            // Somewhere in your method or constructor
                            var jsonSerializerOptions = new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = null
                            };

                            // Init
                            var type = document.RootElement.GetProperty("Type").GetString();
                            var from = document.RootElement.GetProperty("From").GetString();

                            // If comes from the server
                            if (from == Constants.QueueMessageFrom_Server)
                            {
                                var data = document.RootElement.GetProperty("Data");
                                if (data.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("TypeName", out var typeNameProperty))
                                {
                                    if (type == Constants.QueueMessageType_OnSendTradingviewSignalCommand)
                                    {
                                        var @event = JsonSerializer.Deserialize<OnSendTradingviewSignalCommand>(data.GetRawText(), jsonSerializerOptions);
                                        if (@event != null)
                                        {
                                            // Throw event
                                            OnSendTradingviewSignalCommand?.Invoke(@event);

                                            // Remove the message from the queue
                                            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Wait for a short period before checking for more messages
                await Task.Delay(1000);
            }
        }
    }
}
