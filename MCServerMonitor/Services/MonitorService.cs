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

            // Проверяем путь перед запуском, если включено сохранение
            if (_currentConfig.EnableFileSave)
            {
                if (!_configManager.IsPathWritable(_currentConfig.FilePath))
                {
                    Console.WriteLine($"[Ошибка] Нет прав на запись в '{_currentConfig.FilePath}'");
                    Console.WriteLine("Пожалуйста, измените путь сохранения в настройках");
                    return false;
                }
            }

            // Проверяем доступность сервера
            Console.WriteLine($"[Система] Проверка доступности сервера {_currentConfig.ServerAddress}:{_currentConfig.ServerPort}...");
            try
            {
                var testStatus = await _pinger.GetServerStatusAsync(_currentConfig.ServerAddress, _currentConfig.ServerPort);

                Console.WriteLine($"[Система] Сервер доступен!");
                Console.WriteLine($"[Система] Версия сервера: {testStatus.Version}");
                Console.WriteLine($"[Система] MOTD: {testStatus.Description} \n");
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
                Console.WriteLine($"[Система] Файл сохранения: {_currentConfig.FilePath}");
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
                // Ожидаемое исключение при отмене
            }

            _isMonitoring = false;

            var stats = new MonitoringStats
            {
                TotalChecks = _successfulChecks + _failedChecks,
                SuccessfulChecks = _successfulChecks,
                FailedChecks = _failedChecks
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
                        IsSuccess = true
                    };

                    // Сохраняем данные, если включено сохранение
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
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    };

                    OnDataCollected?.Invoke(this, data);
                }

                // Ожидание следующей проверки
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
                string directory = Path.GetDirectoryName(_currentConfig.FilePath);

                // Создаём директорию, если её нет
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string line = data.ToFileString();
                await File.AppendAllTextAsync(_currentConfig.FilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"[Ошибка] Нет прав на запись в файл '{_currentConfig.FilePath}'. Сохранение отключено.");
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
                if (File.Exists(_currentConfig.FilePath))
                {
                    var lines = await File.ReadAllLinesAsync(_currentConfig.FilePath, Encoding.UTF8);
                    int start = Math.Max(0, lines.Length - count);
                    for (int i = start; i < lines.Length; i++)
                    {
                        records.Add(lines[i]);
                    }
                }
                else
                {
                    records.Add("Файл с данными ещё не создан");
                }
            }
            catch (UnauthorizedAccessException)
            {
                records.Add($"Нет прав на чтение файла: {_currentConfig.FilePath}");
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

            // Проверяем новый путь перед сохранением
            if (_currentConfig.EnableFileSave && !_configManager.IsPathWritable(_currentConfig.FilePath))
            {
                Console.WriteLine($"[Ошибка] Путь '{_currentConfig.FilePath}' недоступен для записи.");
                return false;
            }

            return await _configManager.SaveConfigAsync(_currentConfig);
        }

        public bool IsPathWritable(string path)
        {
            return _configManager.IsPathWritable(path);
        }

        public string GetDefaultFilePath()
        {
            return _configManager.GetDefaultFilePath();
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
        public string Description { get; set; } = "";
        public MCStatus.Player[] SamplePlayers { get; set; } = Array.Empty<MCStatus.Player>();
        public int CheckNumber { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = "";

        public string ToFileString()
        {
            if (IsSuccess)
            {
                return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] | #{CheckNumber} | Игроков (макс. {PlayersMax}): {PlayersOnline} | Пинг (в мс): {Latency} " +
                    $"| Игроки: {ToPlayersString(SamplePlayers, PlayersOnline)}";
            }
            else
            {
                return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] | Проверка #{CheckNumber} | Ошибка: {ErrorMessage}";
            }
        }
        public static string ToPlayersString(MCStatus.Player[] players, int? playersActual, int maxPlayersCount = 20)
        {
            if (players == null || players.Length == 0)
                return "нет игроков";

            // Извлекаем имена и сортируем по алфавиту
            var playerNames = players
                .Select(p => p.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int playersToShow = Math.Min(playerNames.Count, maxPlayersCount);
            var namesToShow = playerNames.Take(playersToShow);

            string result = string.Join(", ", namesToShow);

            if (playerNames.Count > maxPlayersCount)
            {
                result += $" и ещё {playerNames.Count - maxPlayersCount} игроков";
            }

            if (playerNames.Count != playersActual)
            {
                result += $" и ещё {playersActual - players.Length} не отображаются в списке";
            }

            return result;
        }

        public string ToDisplayString()
        {
            if (IsSuccess)
            {
                // Цвет для игроков
                string playersColor;
                if (PlayersOnline > 100)
                    playersColor = "\x1b[93m"; // Жёлтый
                else if (PlayersOnline > 50)
                    playersColor = "\x1b[92m"; // Зелёный
                else
                    playersColor = "\x1b[96m"; // Голубой

                // Цвет для пинга
                string pingColor;
                if (Latency > 200)
                    pingColor = "\x1b[91m"; // Красный
                else if (Latency > 100)
                    pingColor = "\x1b[93m"; // Жёлтый
                else
                    pingColor = "\x1b[92m"; // Зелёный

                return $"[{Timestamp:HH:mm:ss}] | Онлайн: {playersColor}{PlayersOnline}/{PlayersMax}\x1b[0m | Пинг: {pingColor}{Latency}мс\x1b[0m | #{CheckNumber} | {ToPlayersString(SamplePlayers, PlayersOnline)}";
            }
            else
            {
                return $"[{Timestamp:HH:mm:ss}] | Ошибка: {ErrorMessage}\x1b[0m | #{CheckNumber}";
            }
        }
    }

    public class MonitoringStats
    {
        public int TotalChecks { get; set; }
        public int SuccessfulChecks { get; set; }
        public int FailedChecks { get; set; }

        public double SuccessRate => TotalChecks > 0 ? (double)SuccessfulChecks / TotalChecks * 100 : 0;
    }
}