using Newtonsoft.Json;

namespace JCTG.Client
{
    public class Log
    {
        public string? Type { get; set; }
        public DateTime Time { get; set; }
        public string? Message { get; set; }
        [JsonProperty("error_type")]
        public string? ErrorType { get; set; }
        public string? Description { get; set; }

    }
}