using System.Text.Json;
using MCServerMonitor.Models;

namespace MCServerMonitor.Services
{
    public class ConfigManager
    {
        private const string ConfigFileName = "minecraft_monitor_config.json";
        private string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        public async Task<Config> LoadConfigAsync()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = await File.ReadAllTextAsync(ConfigPath, System.Text.Encoding.UTF8);
                    var config = JsonSerializer.Deserialize<Config>(json);
                    if (config != null)
                    {
                        if (!string.IsNullOrEmpty(config.SaveFolderPath) && !IsFolderWritable(config.SaveFolderPath))
                        {
                            Console.WriteLine($"[Предупреждение] Папка '{config.SaveFolderPath}' недоступна для записи. Будет использована папка по умолчанию.");
                            config.SaveFolderPath = GetDefaultFolderPath();
                        }
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ошибка] Не удалось загрузить настройки: {ex.Message}");
            }

            var defaultConfig = new Config();
            defaultConfig.SaveFolderPath = GetDefaultFolderPath();
            return defaultConfig;
        }

        public async Task<bool> SaveConfigAsync(Config config)
        {
            try
            {
                if (!string.IsNullOrEmpty(config.SaveFolderPath) && !IsFolderWritable(config.SaveFolderPath))
                {
                    Console.WriteLine($"[Ошибка] Папка '{config.SaveFolderPath}' недоступна для записи.");
                    return false;
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(config, options);
                await File.WriteAllTextAsync(ConfigPath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ошибка] Не удалось сохранить настройки: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Проверяет, доступна ли папка для записи
        /// </summary>
        public bool IsFolderWritable(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string testFile = Path.Combine(folderPath, $"test_{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Возвращает путь по умолчанию в папке Documents пользователя
        /// </summary>
        public string GetDefaultFolderPath()
        {
            try
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string appFolder = Path.Combine(documentsPath, "MinecraftServerMonitor");

                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }

                return appFolder;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        /// <summary>
        /// Генерирует имя файла на основе адреса сервера и времени старта
        /// </summary>
        public string GenerateFileName(string serverAddress, int serverPort, FileNamingPattern pattern, DateTime startTime, int duration)
        {
            string serverName;
            if (pattern == FileNamingPattern.ServerName)
            {
                serverName = serverAddress.Replace('.', '_').Replace(':', '_');
            }
            else
            {
                try
                {
                    var ips = System.Net.Dns.GetHostAddresses(serverAddress);
                    var ipv4 = System.Array.Find(ips, ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    serverName = (ipv4?.ToString() ?? serverAddress).Replace('.', '_');
                }
                catch
                {
                    serverName = serverAddress.Replace('.', '_');
                }
            }

            string dateTimeStr = startTime.ToString("yyyy-MM-dd_HH-mm-ss");

            return $"monitor_{serverName}_{dateTimeStr}_{duration}sec.txt";
        }

        /// <summary>
        /// Получает полный путь к файлу для сохранения
        /// </summary>
        public string GetFullFilePath(Config config, DateTime startTime, int duration)
        {
            string fileName = GenerateFileName(config.ServerAddress, config.ServerPort, config.FileNamingPattern, startTime, duration);
            return Path.Combine(config.SaveFolderPath, fileName);
        }
    }
}