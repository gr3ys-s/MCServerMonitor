using MCServerMonitor.Models;
using MCServerMonitor.Services;

namespace MCServerMonitor.UI;
public class MenuManager
{
    private readonly MonitorService _monitor;
    private Config _config;
    private readonly ConfigManager _configManager;
    private bool _isMonitoringDisplay;

    public MenuManager(MonitorService monitor)
    {
        _monitor = monitor;
        _configManager = new ConfigManager();
        _config = _monitor.GetCurrentConfig();

        _monitor.OnDataCollected += OnDataCollected;
        _monitor.OnMonitoringStopped += OnMonitoringStopped;
    }

    private void OnDataCollected(object sender, MonitoringData data)
    {
        if (_isMonitoringDisplay)
        {
            Console.Write($"\r{data.ToDisplayString()}                                    ");
        }
    }

    private void OnMonitoringStopped(object sender, MonitoringStats stats)
    {
        if (_isMonitoringDisplay)
        {
            Console.WriteLine($"\n\n[Статистика] Всего проверок: {stats.TotalChecks}, Успешно: {stats.SuccessfulChecks}, Ошибок: {stats.FailedChecks}, Успешность: {stats.SuccessRate:F1}%");
            if (!string.IsNullOrEmpty(stats.DataFilePath) && File.Exists(stats.DataFilePath))
            {
                Console.WriteLine($"[Статистика] Данные сохранены в: {stats.DataFilePath}");
            }
            Console.WriteLine("\nНажмите любую клавишу для возврата в главное меню...");
            Console.ReadKey(true);
            _isMonitoringDisplay = false;
        }
    }

    public async Task RunAsync()
    {
        while (true)
        {
            Console.Clear();
            DisplayHeader();
            DisplayMainMenu();

            string choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await StartMonitoringAsync();
                    break;
                case "2":
                    await ShowSettingsMenuAsync();
                    break;
                case "3":
                    await ShowLastRecordsAsync();
                    break;
                case "4":
                    Console.WriteLine("\nВыход из программы...");
                    if (_monitor.IsMonitoring)
                    {
                        await _monitor.StopMonitoringAsync();
                    }
                    return;
                default:
                    ShowMessage("Неверный выбор. Попробуйте снова.", true);
                    break;
            }
        }
    }

    private void DisplayHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         MINECRAFT SERVER MONITOR - Система мониторинга       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private void DisplayMainMenu()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("ГЛАВНОЕ МЕНЮ:");
        Console.ResetColor();
        Console.WriteLine("  1. ▶ Запустить мониторинг");
        Console.WriteLine("  2. ⚙ Настройки");
        Console.WriteLine("  3. 📄 Просмотреть последние записи");
        Console.WriteLine("  4. ✖ Выход");
        Console.WriteLine();
        Console.Write("Выберите действие (1-4): ");
    }

    private async Task StartMonitoringAsync()
    {
        Console.Clear();
        DisplayHeader();

        bool success = await _monitor.StartMonitoringAsync(_config);

        if (!success)
        {
            Console.WriteLine("\n[Ошибка] Не удалось запустить мониторинг. Проверьте настройки.");
            Console.WriteLine("\nНажмите любую клавишу для возврата в главное меню...");
            Console.ReadKey(true);
            return;
        }

        _isMonitoringDisplay = true;

        Console.WriteLine("\nМониторинг запущен. Нажмите 'S' для остановки...");

        while (_monitor.IsMonitoring)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.S)
                {
                    await _monitor.StopMonitoringAsync();
                    break;
                }
            }
            await Task.Delay(100);
        }
    }

    private async Task ShowSettingsMenuAsync()
    {
        bool settingsChanged = false;

        while (true)
        {
            Console.Clear();
            DisplayHeader();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("НАСТРОЙКИ MINECRAFT СЕРВЕРА:");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"  1. Сохранять результаты в файл: {(_config.EnableFileSave ? "Да ✓" : "Нет ✗")}");
            Console.WriteLine($"  2. Папка сохранения: {_config.SaveFolderPath}");
            Console.WriteLine($"  3. Имя файла использовать: {(_config.FileNamingPattern == FileNamingPattern.ServerName ? "Имя сервера" : "IP адрес")}");
            Console.WriteLine($"  4. Интервал проверки: {_config.CheckIntervalSeconds} сек. ({_config.CheckIntervalSeconds / 60} мин)");
            Console.WriteLine($"  5. Адрес сервера: {_config.ServerAddress}:{_config.ServerPort}");
            Console.WriteLine();
            Console.WriteLine("  6. ✓ Сохранить настройки и выйти");
            Console.WriteLine("  7. ✖ Выйти без сохранения");
            Console.WriteLine();
            Console.Write("Выберите действие (1-7): ");

            string choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    _config.EnableFileSave = !_config.EnableFileSave;
                    settingsChanged = true;
                    ShowMessage($"Сохранение файлов: {(_config.EnableFileSave ? "Включено" : "Отключено")}");
                    break;

                case "2":
                    await ChangeFolderPathAsync();
                    settingsChanged = true;
                    break;

                case "3":
                    Console.WriteLine();
                    Console.WriteLine("Выберите, что использовать для имени файла:");
                    Console.WriteLine("  1. Имя сервера (например, mc_hypixel_net)");
                    Console.WriteLine("  2. IP адрес (например, 123_456_78_90)");
                    Console.WriteLine();
                    Console.Write("Ваш выбор (1-2): ");

                    string namingChoice = Console.ReadLine()?.Trim();
                    if (namingChoice == "1")
                    {
                        _config.FileNamingPattern = FileNamingPattern.ServerName;
                        settingsChanged = true;
                        ShowMessage("Имя файла будет содержать имя сервера");
                    }
                    else if (namingChoice == "2")
                    {
                        _config.FileNamingPattern = FileNamingPattern.ServerAddress;
                        settingsChanged = true;
                        ShowMessage("Имя файла будет содержать IP адрес");
                    }
                    else
                    {
                        ShowMessage("Неверный выбор", true);
                    }
                    break;

                case "4":
                    Console.Write("Введите интервал проверки в секундах (минимум 5): ");
                    if (int.TryParse(Console.ReadLine(), out int interval) && interval >= 5)
                    {
                        _config.CheckIntervalSeconds = interval;
                        settingsChanged = true;
                        ShowMessage($"Интервал установлен: {interval} сек. ({interval / 60} мин)");
                    }
                    else
                    {
                        ShowMessage("Ошибка: интервал должен быть числом не менее 5", true);
                    }
                    break;

                case "5":
                    Console.Write("Введите адрес Minecraft сервера (домен или IP): ");
                    string serverAddress = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(serverAddress))
                    {
                        _config.ServerAddress = serverAddress;
                        settingsChanged = true;
                        ShowMessage($"Адрес сервера изменён: {serverAddress}");
                    }

                    Console.Write("Введите порт сервера (по умолчанию 25565): ");
                    string portInput = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(portInput))
                    {
                        _config.ServerPort = 25565;
                    }
                    else if (int.TryParse(portInput, out int port) && port > 0 && port < 65536)
                    {
                        _config.ServerPort = port;
                        ShowMessage($"Порт изменён: {port}");
                    }
                    else
                    {
                        ShowMessage("Неверный порт, оставлен текущий", true);
                    }
                    break;

                case "6":
                    if (settingsChanged)
                    {
                        bool saved = await _monitor.UpdateConfigAsync(_config);
                        if (saved)
                        {
                            ShowMessage("Настройки сохранены", false, 1500);
                        }
                        else
                        {
                            ShowMessage("Не удалось сохранить настройки. Проверьте права доступа.", true, 2000);
                            continue;
                        }
                    }
                    return;

                case "7":
                    if (settingsChanged)
                    {
                        ShowMessage("Изменения не сохранены", true, 1500);
                    }
                    return;

                default:
                    ShowMessage("Неверный выбор", true);
                    break;
            }
        }
    }

    private async Task ChangeFolderPathAsync()
    {
        Console.WriteLine();
        Console.WriteLine("Введите путь к ПАПКЕ для сохранения файлов.");
        Console.WriteLine("Программа сама создаст файл с именем, содержащим:");
        Console.WriteLine("  - адрес сервера");
        Console.WriteLine("  - дату и время запуска мониторинга");
        Console.WriteLine();
        Console.WriteLine("Примеры:");
        Console.WriteLine($"  - {_monitor.GetDefaultFolderPath()} (рекомендуется)");
        Console.WriteLine("  - C:\\Users\\ВашеИмя\\Desktop\\MinecraftStats");
        Console.WriteLine("  - D:\\ServerMonitoring");
        Console.WriteLine();
        Console.Write("Путь к папке: ");

        string newPath = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(newPath))
        {
            ShowMessage("Путь не может быть пустым", true);
            return;
        }

        if (_monitor.IsFolderWritable(newPath))
        {
            _config.SaveFolderPath = newPath;
            ShowMessage($"Папка сохранения изменена: {_config.SaveFolderPath}");
        }
        else
        {
            ShowMessage($"Нет прав на запись в '{newPath}'. Выберите другую папку.", true);

            Console.WriteLine();
            Console.WriteLine("Предлагаю использовать путь по умолчанию:");
            Console.WriteLine(_monitor.GetDefaultFolderPath());
            Console.Write("Использовать этот путь? (y/n): ");

            if (Console.ReadLine()?.Trim().ToLower() == "y")
            {
                _config.SaveFolderPath = _monitor.GetDefaultFolderPath();
                ShowMessage($"Путь изменён на: {_config.SaveFolderPath}");
            }
        }
    }

    private async Task ShowLastRecordsAsync()
    {
        Console.Clear();
        DisplayHeader();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("ПОСЛЕДНИЕ ЗАПИСИ МОНИТОРИНГА");
        Console.ResetColor();
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine();

        var records = await _monitor.GetLastRecordsAsync(20);

        if (records.Count > 0)
        {
            foreach (var record in records)
            {
                Console.WriteLine(record);
            }
            Console.WriteLine();
            Console.WriteLine("=".PadRight(70, '='));
        }
        else
        {
            Console.WriteLine("Нет записей для отображения");
        }

        Console.WriteLine("\nНажмите любую клавишу для возврата в главное меню...");
        Console.ReadKey(true);
    }

    private void ShowMessage(string message, bool isError = false, int delayMs = 1000)
    {
        if (isError)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Ошибка] {message}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[Успех] {message}");
            Console.ResetColor();
        }

        if (delayMs > 0)
        {
            System.Threading.Thread.Sleep(delayMs);
        }
    }
}