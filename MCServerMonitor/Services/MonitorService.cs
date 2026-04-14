using System.Text;
using MCServerMonitor.Models;

namespace MCServerMonitor.Services
{
    public class MonitorService
    {
        private CancellationTokenSource _cts;
        private Task _monitoringTask;
        private bool _isMonitoring;
        private Config _currentConfig;
        private int _successfulChecks;
        private int _failedChecks;
        private readonly ConfigManager _configManager;
        private readonly MinecraftServerPinger _pinger;
        private string _currentDataFilePath;
        private DateTime _monitoringStartTime;
        private int _monitoringDuration;

        public event EventHandler<MonitoringData> OnDataCollected;
        public event EventHandler<MonitoringStats> OnMonitoringStopped;

        public bool IsMonitoring => _isMonitoring;

        public MonitorService()
        {
            _configManager = new ConfigManager();
            _pinger = new MinecraftServerPinger();
            _currentConfig = _configManager.LoadConfigAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> StartMonitoringAsync(Config config)
        {
            if (_isMonitoring)
            {
                Console.WriteLine("[Предупреждение] Мониторинг уже запущен");
                return false;
            }

            _currentConfig = config;
            _monitoringStartTime = DateTime.Now;
            _monitoringDuration = config.CheckIntervalSeconds;

            // Проверяем папку перед запуском, если включено сохранение
            if (_currentConfig.EnableFileSave)
            {
                if (string.IsNullOrEmpty(_currentConfig.SaveFolderPath))
                {
                    _currentConfig.SaveFolderPath = _configManager.GetDefaultFolderPath();
                }

                if (!_configManager.IsFolderWritable(_currentConfig.SaveFolderPath))
                {
                    Console.WriteLine($"[Ошибка] Папка '{_currentConfig.SaveFolderPath}' недоступна для записи.");
                    Console.WriteLine("Пожалуйста, измените путь сохранения в настройках");
                    return false;
                }

                _currentDataFilePath = _configManager.GetFullFilePath(_currentConfig, _monitoringStartTime, _monitoringDuration);
            }

            Console.WriteLine($"[Система] Проверка доступности сервера {_currentConfig.ServerAddress}:{_currentConfig.ServerPort}...");
            try
            {
                var testStatus = await _pinger.GetServerStatusAsync(_currentConfig.ServerAddress, _currentConfig.ServerPort);

                Console.WriteLine($"[Система] Сервер доступен!");
                Console.WriteLine($"[Система] Версия сервера: {testStatus.Version}");
                Console.WriteLine($"[Система] MOTD: {testStatus.Description}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ошибка] Сервер недоступен: {ex.Message}");
                Console.WriteLine("Проверьте адрес и порт сервера");
                return false;
            }

            _successfulChecks = 0;
            _failedChecks = 0;
            _cts = new CancellationTokenSource();

            _isMonitoring = true;
            _monitoringTask = Task.Run(() => MonitoringLoopAsync(_cts.Token));

            Console.WriteLine($"[Система] Мониторинг запущен. Интервал: {_currentConfig.CheckIntervalSeconds} сек.");
            Console.WriteLine($"[Система] Сохранение данных: {(_currentConfig.EnableFileSave ? "Включено" : "Отключено")}");
            if (_currentConfig.EnableFileSave)
            {
                Console.WriteLine($"[Система] Папка сохранения: {_currentConfig.SaveFolderPath}");
                Console.WriteLine($"[Система] Файл сохранения: {Path.GetFileName(_currentDataFilePath)}");
            }

            return true;
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                return;
            }

            _cts?.Cancel();

            try
            {
                if (_monitoringTask != null)
                {
                    await _monitoringTask;
                }
            }
            catch (OperationCanceledException)
            {
                
            }

            _isMonitoring = false;

            var stats = new MonitoringStats
            {
                TotalChecks = _successfulChecks + _failedChecks,
                SuccessfulChecks = _successfulChecks,
                FailedChecks = _failedChecks,
                DataFilePath = _currentDataFilePath
            };

            OnMonitoringStopped?.Invoke(this, stats);

            Console.WriteLine("[Система] Мониторинг остановлен");
        }

        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            int checkCounter = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var status = await _pinger.GetServerStatusAsync(
                        _currentConfig.ServerAddress,
                        _currentConfig.ServerPort);

                    _successfulChecks++;
                    checkCounter++;

                    var data = new MonitoringData
                    {
                        Timestamp = DateTime.Now,
                        ServerAddress = _currentConfig.ServerAddress,
                        ServerPort = _currentConfig.ServerPort,
                        PlayersOnline = status.OnlinePlayers,
                        PlayersMax = status.MaxPlayers,
                        Latency = status.Latency,
                        Version = status.Version,
                        Description = status.Description,
                        SamplePlayers = status.SamplePlayers,
                        CheckNumber = checkCounter,
                        MonitoringStartTime = _monitoringStartTime,
                        IsSuccess = true
                    };

                    if (_currentConfig.EnableFileSave)
                    {
                        await SaveDataAsync(data);
                    }

                    OnDataCollected?.Invoke(this, data);
                }
                catch (Exception ex)
                {
                    _failedChecks++;
                    checkCounter++;

                    var data = new MonitoringData
                    {
                        Timestamp = DateTime.Now,
                        ServerAddress = _currentConfig.ServerAddress,
                        ServerPort = _currentConfig.ServerPort,
                        CheckNumber = checkCounter,
                        MonitoringStartTime = _monitoringStartTime,
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    };

                    if (_currentConfig.EnableFileSave)
                    {
                        await SaveDataAsync(data);
                    }

                    OnDataCollected?.Invoke(this, data);
                }

                try
                {
                    await Task.Delay(_currentConfig.CheckIntervalSeconds * 1000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task SaveDataAsync(MonitoringData data)
        {
            try
            {
                string directory = Path.GetDirectoryName(_currentDataFilePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string line = data.ToFileString();
                await File.AppendAllTextAsync(_currentDataFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"[Ошибка] Нет прав на запись в файл '{_currentDataFilePath}'. Сохранение отключено.");
                _currentConfig.EnableFileSave = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ошибка] Не удалось сохранить данные: {ex.Message}");
            }
        }

        public async Task<List<string>> GetLastRecordsAsync(int count = 20)
        {
            var records = new List<string>();

            if (!_currentConfig.EnableFileSave)
            {
                records.Add("Сохранение данных отключено в настройках");
                return records;
            }

            try
            {
                if (Directory.Exists(_currentConfig.SaveFolderPath))
                {
                    var files = Directory.GetFiles(_currentConfig.SaveFolderPath, "monitor_*.txt");
                    if (files.Length > 0)
                    {
                        var latestFile = files.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                        var lines = await File.ReadAllLinesAsync(latestFile, Encoding.UTF8);
                        int start = Math.Max(0, lines.Length - count);
                        for (int i = start; i < lines.Length; i++)
                        {
                            records.Add(lines[i]);
                        }

                        if (records.Count > 0)
                        {
                            records.Insert(0, $"=== Файл: {Path.GetFileName(latestFile)} ===");
                            records.Insert(1, "");
                        }
                    }
                    else
                    {
                        records.Add("Файлы с данными ещё не созданы");
                    }
                }
                else
                {
                    records.Add("Папка с данными не найдена");
                }
            }
            catch (UnauthorizedAccessException)
            {
                records.Add($"Нет прав на чтение папки: {_currentConfig.SaveFolderPath}");
            }
            catch (Exception ex)
            {
                records.Add($"Ошибка чтения файла: {ex.Message}");
            }

            return records;
        }

        public Config GetCurrentConfig() => _currentConfig;

        public async Task<bool> UpdateConfigAsync(Config newConfig)
        {
            _currentConfig = newConfig;

            if (_currentConfig.EnableFileSave && !string.IsNullOrEmpty(_currentConfig.SaveFolderPath) && !_configManager.IsFolderWritable(_currentConfig.SaveFolderPath))
            {
                Console.WriteLine($"[Ошибка] Папка '{_currentConfig.SaveFolderPath}' недоступна для записи.");
                return false;
            }

            return await _configManager.SaveConfigAsync(_currentConfig);
        }

        public bool IsFolderWritable(string folderPath)
        {
            return _configManager.IsFolderWritable(folderPath);
        }

        public string GetDefaultFolderPath()
        {
            return _configManager.GetDefaultFolderPath();
        }
    }

    public class MonitoringData
    {
        public DateTime Timestamp { get; set; }
        public string ServerAddress { get; set; } = "";
        public int ServerPort { get; set; }
        public int? PlayersOnline { get; set; }
        public int? PlayersMax { get; set; }
        public int Latency { get; set; }
        public string Version { get; set; } = "";
        public string MinecraftVersion { get; set; } = "";
        public string Description { get; set; } = "";
        public MCStatus.Player[] SamplePlayers { get; set; } = Array.Empty<MCStatus.Player>();
        public int CheckNumber { get; set; }
        public DateTime MonitoringStartTime { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = "";

        public string ToFileString()
        {
            if (IsSuccess)
            {
                string result = $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] #{CheckNumber} | Игроков (из {PlayersMax}): {PlayersOnline} | Пинг (мс): {Latency} ";

                if (SamplePlayers.Length > 0)
                {
                    result += $" | Игроки: {ToPlayersString(SamplePlayers)}";
                }

                return result;
            }
            else
            {
                return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] #{CheckNumber} | Ошибка: {ErrorMessage}";
            }
        }

        public string ToDisplayString()
        {
            if (IsSuccess)
            {
                string playersColor = PlayersOnline > 100 ? "\x1b[93m" : (PlayersOnline > 50 ? "\x1b[92m" : "\x1b[96m");
                string pingColor = Latency > 200 ? "\x1b[91m" : (Latency > 100 ? "\x1b[93m" : "\x1b[92m");

                string playersShort = ToShortPlayersString(SamplePlayers);

                return $"[{Timestamp:HH:mm:ss}] | Онлайн: {playersColor}{PlayersOnline}/{PlayersMax}\x1b[0m | Пинг: {pingColor}{Latency}мс\x1b[0m | #{CheckNumber} | {playersShort}";
            }
            else
            {
                return $"[{Timestamp:HH:mm:ss}] \x1b[91m{ServerAddress}:{ServerPort} | Ошибка: {ErrorMessage}\x1b[0m | #{CheckNumber}";
            }
        }

        public static string ToPlayersString(MCStatus.Player[] players, int maxPlayersToShow = 20)
        {
            if (players == null || players.Length == 0) return "";

            var playerNames = players
                .Select(p => p.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int playersToShow = Math.Min(playerNames.Count, maxPlayersToShow);
            string result = string.Join(", ", playerNames.Take(playersToShow));

            if (playerNames.Count > maxPlayersToShow)
                result += $" и ещё {playerNames.Count - maxPlayersToShow} игроков";

            return result;
        }

        public static string ToShortPlayersString(MCStatus.Player[] players, int maxPlayersToShow = 3)
        {
            if (players == null || players.Length == 0) return "";

            var playerNames = players
                .Select(p => p.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int playersToShow = Math.Min(playerNames.Count, maxPlayersToShow);
            string result = string.Join(", ", playerNames.Take(playersToShow));

            if (playerNames.Count > maxPlayersToShow)
                result += $" +{playerNames.Count - maxPlayersToShow}";

            return result;
        }
    }

    public class MonitoringStats
    {
        public int TotalChecks { get; set; }
        public int SuccessfulChecks { get; set; }
        public int FailedChecks { get; set; }
        public string DataFilePath { get; set; } = "";

        public double SuccessRate => TotalChecks > 0 ? (double)SuccessfulChecks / TotalChecks * 100 : 0;
    }
}