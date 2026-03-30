using System.Text.Json.Serialization;

namespace MCServerMonitor.Models
{
    public class Config
    {
        [JsonPropertyName("enable_file_save")]
        public bool EnableFileSave { get; set; } = true;

        [JsonPropertyName("file_path")]
        public string FilePath { get; set; } = "minecraft_server_data.txt";

        [JsonPropertyName("check_interval_seconds")]
        public int CheckIntervalSeconds { get; set; } = 3600; // По умолчанию 1 час

        [JsonPropertyName("server_address")]
        public string ServerAddress { get; set; } = "localhost";

        [JsonPropertyName("server_port")]
        public int ServerPort { get; set; } = 25565;
    }
}