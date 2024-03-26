namespace JCTG
{
    public class QueueMessage<T>()
    {
        public required int AccountId { get; set; }
        public required string Type { get; set; }
        public required string From { get; init; } = Constants.QueueMessageFrom_Server;
        public required string? TypeName { get; init; } = nameof(T);
        public required T Data { get; set; }
    }
}
