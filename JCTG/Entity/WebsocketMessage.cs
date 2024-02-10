namespace JCTG
{
    public class WebsocketMessage<T>()
    {
        public required string Type { get; set; }
        public required string From { get; init; } = Constants.WebsocketMessageFrom_Server;
        public required string DataType { get; init; } = Constants.WebsocketMessageDatatype_JSON;
        public required string? TypeName { get; init; } = nameof(T);
        public required T Data { get; set; }
    }
}
