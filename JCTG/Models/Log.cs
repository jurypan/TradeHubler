using Newtonsoft.Json;

namespace JCTG.Models
{
    public class Log
    {
        public DateTime Time { get; set; }
        public string? Type { get; set; }
        public string? Message { get; set; }
        [JsonProperty("Error_type")]
        public string? ErrorType { get; set; }
        public string? Description { get; set; }

    }
}