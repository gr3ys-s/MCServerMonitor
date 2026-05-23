using System.Text.Json.Serialization;

namespace MCServerMonitor.Models
{
    public class Config
    {
        /// <summary>
        /// Проверка на сохранение
        /// </summary>
        [JsonPropertyName("enable_file_save")]
        public bool EnableFileSave { get; set; } = true;

        /// <summary>
        /// Путь сохранения
        /// </summary>
        [JsonPropertyName("save_folder_path")]
        public string SaveFolderPath { get; set; } = ""; 

        /// <summary>
        /// Интервал
        /// </summary>
        [JsonPropertyName("check_interval_seconds")]
        public int CheckIntervalSeconds { get; set; } = 3600; 

        /// <summary>
        /// Адрес сервера
        /// </summary>
        [JsonPropertyName("server_address")]
        public string ServerAddress { get; set; } = "localhost";

        /// <summary>
        /// Порт сервера
        /// </summary>
        [JsonPropertyName("server_port")]
        public int ServerPort { get; set; } = 25565;

        /// <summary>
        /// Паттерн названия файла
        /// </summary>
        [JsonPropertyName("file_naming_pattern")]
        public FileNamingPattern FileNamingPattern { get; set; } = FileNamingPattern.ServerName; 
    }

    public enum FileNamingPattern
    {
        ServerName = 0,   
        ServerAddress = 1 
    }
}