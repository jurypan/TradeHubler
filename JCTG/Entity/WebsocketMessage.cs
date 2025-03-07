﻿namespace JCTG
{
    public class WebsocketMessage<T>()
    {
        public required string Type { get; set; }
        public required string From { get; init; } = Constants.QueueMessageFrom_Server;
        public required string DataType { get; init; } = Constants.QueueMessageDatatype_JSON;
        public required string? TypeName { get; init; } = nameof(T);
        public required T Data { get; set; }
    }
}
