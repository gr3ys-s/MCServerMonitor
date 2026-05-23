using MCServerMonitor.Models;
using MCServerMonitor.Services;
using MCServerMonitor.UI;

namespace MCServerMonitor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "Minecraft Server Monitor - Мониторинг Minecraft сервера";

            if (args.Length > 0)
            {
                await RunWithArgumentsAsync(args);
            }
            else
            {
                var monitor = new MonitorService();
                var menu = new MenuManager(monitor);

                await menu.RunAsync();
            }
        }
        /// <summary>
        /// Запуск с аргументами
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task RunWithArgumentsAsync(string[] args)
        {
            var configManager = new ConfigManager();
            var config = await configManager.LoadConfigAsync();
            var autoStartManager = new AutoStartManager();
            var monitor = new MonitorService();

            Console.WriteLine("Minecraft Server Monitor - Автоматический режим");
            Console.WriteLine("=".PadRight(50, '='));

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--server":
                    case "-s": 
                        if (i + 1 < args.Length)
                        {
                            var serverParts = args[++i].Split(':');
                            config.ServerAddress = serverParts[0];
                            if (serverParts.Length > 1 && int.TryParse(serverParts[1], out var port))
                            {
                                config.ServerPort = port;
                            }
                            Console.WriteLine($"✓ Адрес сервера: {config.ServerAddress}:{config.ServerPort}");
                        }
                        break;
                    case "--interval":
                    case "-i":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int interval))
                        {
                            config.CheckIntervalSeconds = interval;
                            Console.WriteLine($"✓ Интервал проверки: {interval} сек.");
                        }
                        break;
                    case "--save-folder":
                    case "-f":
                        if (i + 1 < args.Length)
                        {
                            config.SaveFolderPath = args[++i];
                            config.EnableFileSave = true;
                            Console.WriteLine($"✓ Папка сохранения: {config.SaveFolderPath}");
                        }
                        break;
                    case "--no-save":
                    case "-ns":
                        config.EnableFileSave = false;
                        Console.WriteLine("✓ Сохранение данных отключено");
                        break;
                    case "--naming":
                    case "-n":
                        if (i + 1 < args.Length)
                        {
                            string naming = args[++i].ToLower();
                            if (naming == "name" || naming == "server")
                            {
                                config.FileNamingPattern = FileNamingPattern.ServerName;
                                Console.WriteLine("✓ Имя файла: по имени сервера");
                            }
                            else if (naming == "ip" || naming == "address")
                            {
                                config.FileNamingPattern = FileNamingPattern.ServerAddress;
                                Console.WriteLine("✓ Имя файла: по IP адресу");
                            }
                        }
                        break;
                    case "--auto-start":
                    case "-a":
                        Console.WriteLine("✓ Режим автозапуска: мониторинг стартует без меню");
                        // В этом режиме сразу запускаем мониторинг
                        await autoStartManager.AddToStartupAsync();
                        Console.WriteLine("✓ Программа добавлена в автозагрузку");
                        break;
                    case "--help":
                    case "-h":
                    case "-?":
                        ShowHelp();
                        return;
                }
            }
            if (!autoStartManager.IsAutoStartEnabled())
            {
                Console.WriteLine();
                Console.Write("Добавить программу в автозагрузку Windows? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                {
                    if (await autoStartManager.AddToStartupAsync())
                    {
                        Console.WriteLine("✓ Программа добавлена в автозагрузку");
                    }
                }
            }
            else
            {
                Console.WriteLine("ℹ Программа уже добавлена в автозагрузку");
            }

            await configManager.SaveConfigAsync(config);

            Console.WriteLine();
            Console.WriteLine("Запуск мониторинга...");
            Console.WriteLine("Нажмите Ctrl+C для остановки");
            Console.WriteLine();

            var cts = new System.Threading.CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n\nПолучен сигнал остановки...");
                cts.Cancel();
            };

            bool success = await monitor.StartMonitoringAsync(config);

            if (!success)
            {
                Console.WriteLine("Ошибка запуска мониторинга. Проверьте настройки.");
                return;
            }

            try
            {
                while (monitor.IsMonitoring && !cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                }
            }
            catch (OperationCanceledException)
            {
                // Ожидаемое исключение
            }

            if (monitor.IsMonitoring)
            {
                await monitor.StopMonitoringAsync();
            }

            Console.WriteLine("\nМониторинг завершён. Нажмите любую клавишу для выхода...");
            Console.ReadKey(true);
        }
        static void ShowHelp()
        {
            Console.WriteLine(@"
Minecraft Server Monitor - Параметры командной строки
================================================================================

Использование: MinecraftMonitor.exe [параметры]

Параметры:
  --server, -s <адрес[:порт]>    Адрес Minecraft сервера (порт по умолчанию: 25565)
                                 Пример: --server mc.hypixel.net
                                 Пример: --server localhost:25565

  --interval, -i <секунды>       Интервал проверки в секундах (минимум 5)
                                 Пример: --interval 3600

  --save-folder, -f <путь>       Путь к папке для сохранения файлов
                                 Пример: --save-folder ""C:\Users\Имя\Documents\Stats""

  --no-save, -ns                 Отключить сохранение данных в файл

  --naming, -n <name|ip>         Способ именования файла (name или ip)
                                 Пример: --naming name

  --auto-start, -a               Добавить программу в автозагрузку Windows

  --help, -h, -?                 Показать эту справку

Примеры:
  # Обычный запуск с параметрами
  MinecraftMonitor.exe --server mc.hypixel.net --interval 300 --save-folder ""D:\Stats""

  # Запуск без сохранения файлов
  MinecraftMonitor.exe --server localhost --no-save

  # Добавление в автозагрузку
  MinecraftMonitor.exe --auto-start --server mc.hypixel.net --interval 3600

================================================================================
");
            Console.ReadKey();
        }
    }
}