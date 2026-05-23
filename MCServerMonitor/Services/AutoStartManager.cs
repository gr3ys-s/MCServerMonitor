using MCServerMonitor.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCServerMonitor.Services
{
    public class AutoStartManager
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "MCServerMonitor";
        /// <summary>
        /// Проверяет, добавлена ли программа в автозагрузку
        /// </summary>
        public bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                if (key != null)
                {
                    string value = key.GetValue(AppName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ошибка] Не удалось проверить автозагрузку: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Добавляет программу в автозагрузку
        /// </summary>
        public async Task<bool> AddToStartupAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    string exePath = GetExecutablePath();

                    // Создаём аргументы для автозагрузки с текущими настройками
                    var configManager = new ConfigManager();
                    var config = configManager.LoadConfigAsync().GetAwaiter().GetResult();

                    string arguments = BuildAutoStartArguments(config);

                    using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                    if (key != null)
                    {
                        key.SetValue(AppName, $"\"{exePath}\" {arguments}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Ошибка] Не удалось добавить в автозагрузку: {ex.Message}");
                }
                return false;
            });
        }

        /// <summary>
        /// Удаляет программу из автозагрузки
        /// </summary>
        public async Task<bool> RemoveFromStartupAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                    if (key != null)
                    {
                        key.DeleteValue(AppName, false);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Ошибка] Не удалось удалить из автозагрузки: {ex.Message}");
                }
                return false;
            });
        }

        /// <summary>
        /// Получает путь к исполняемому файлу программы
        /// </summary>
        private string GetExecutablePath()
        {
            string exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            }
            return exePath;
        }

        /// <summary>
        /// Формирует аргументы командной строки для автозагрузки
        /// </summary>
        private string BuildAutoStartArguments(Config config)
        {
            var args = new System.Text.StringBuilder();

            args.Append($"--server \"{config.ServerAddress}:{config.ServerPort}\" ");
            args.Append($"--interval {config.CheckIntervalSeconds} ");

            if (config.EnableFileSave && !string.IsNullOrEmpty(config.SaveFolderPath))
            {
                args.Append($"--save-folder \"{config.SaveFolderPath}\" ");
            }
            else
            {
                args.Append($"--no-save ");
            }

            args.Append($"--naming {(config.FileNamingPattern == FileNamingPattern.ServerName ? "name" : "ip")}");

            return args.ToString();
        }
    }
}
