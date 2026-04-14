using System.Text.Json.Serialization;

namespace MCServerMonitor.Models
{
    public class Config
    {
        [JsonPropertyName("enable_file_save")]
        public bool EnableFileSave { get; set; } = true;

        [JsonPropertyName("save_folder_path")]
        public string SaveFolderPath { get; set; } = ""; 

        [JsonPropertyName("check_interval_seconds")]
        public int CheckIntervalSeconds { get; set; } = 3600; 

        [JsonPropertyName("server_address")]
        public string ServerAddress { get; set; } = "localhost";

        [JsonPropertyName("server_port")]
        public int ServerPort { get; set; } = 25565;

        [JsonPropertyName("file_naming_pattern")]
        public FileNamingPattern FileNamingPattern { get; set; } = FileNamingPattern.ServerName; 
    }

    public enum FileNamingPattern
    {
        ServerName = 0,   
        ServerAddress = 1 
    }
}