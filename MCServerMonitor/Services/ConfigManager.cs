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
                        // Проверяем, доступен ли путь для записи
                        if (!string.IsNullOrEmpty(config.FilePath) && !IsPathWritable(config.FilePath))
                        {
                            Console.WriteLine($"[Предупреждение] Путь '{config.FilePath}' недоступен для записи. Будет использован путь по умолчанию.");
                            config.FilePath = GetDefaultFilePath();
                        }
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ошибка] Не удалось загрузить настройки: {ex.Message}");
            }

            // Возвращаем настройки по умолчанию
            var defaultConfig = new Config();
            defaultConfig.FilePath = GetDefaultFilePath();
            return defaultConfig;
        }

        public async Task<bool> SaveConfigAsync(Config config)
        {
            try
            {
                // Проверяем путь перед сохранением
                if (!string.IsNullOrEmpty(config.FilePath) && !IsPathWritable(config.FilePath))
                {
                    Console.WriteLine($"[Ошибка] Путь '{config.FilePath}' недоступен для записи.");
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
        /// Проверяет, доступен ли путь для записи
        /// </summary>
        public bool IsPathWritable(string filePath)
        {
            try
            {
                // Получаем директорию
                string directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory))
                {
                    directory = AppDomain.CurrentDomain.BaseDirectory;
                }

                // Проверяем существование директории
                if (!Directory.Exists(directory))
                {
                    // Пытаемся создать директорию
                    Directory.CreateDirectory(directory);
                }

                // Создаём тестовый файл для проверки прав
                string testFile = Path.Combine(directory, $"test_{Guid.NewGuid()}.tmp");
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
        public string GetDefaultFilePath()
        {
            try
            {
                // Используем папку Documents пользователя
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string appFolder = Path.Combine(documentsPath, "MinecraftServerMonitor");

                // Создаём папку приложения, если её нет
                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }

                return Path.Combine(appFolder, "minecraft_server_data.txt");
            }
            catch
            {
                // Если не удалось получить Documents, используем текущую папку
                return "minecraft_server_data.txt";
            }
        }
    }
}