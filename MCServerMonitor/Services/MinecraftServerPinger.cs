using MCStatus;

namespace MCServerMonitor.Services
{
    public class MinecraftServerPinger
    {
        /// <summary>
        /// Получение статуса Minecraft сервера через MCStatus.NET
        /// </summary>
        public async Task<ServerStatus> GetServerStatusAsync(string host, int port)
        {
            try
            {
                var startTime = DateTime.Now;

                // Используем библиотеку MCStatus.NET для получения статуса
                var status = await ServerListClient.GetStatusAsync(host, (ushort)port);

                var latency = (int)(DateTime.Now - startTime).TotalMilliseconds;

                var result = new ServerStatus
                {
                    OnlinePlayers = status.Players.Online,
                    MaxPlayers = status.Players.Max,
                    Version = status.Version.Name ?? "Неизвестно",
                    Description = status.Description ?? status.Description?.ToString() ?? "Нет описания",
                    Latency = latency,
                    ProtocolVersion = status.Version.Protocol,
                    SamplePlayers = status.Players.Sample
                };

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при подключении к серверу {host}:{port}: {ex.Message}");
            }
        }
    }

    public class ServerStatus
    {
        public int OnlinePlayers { get; set; }
        public int MaxPlayers { get; set; }
        public string Version { get; set; } = "Неизвестно";
        public string Description { get; set; } = "Нет описания";
        public int Latency { get; set; }
        public int ProtocolVersion { get; set; }
        public MCStatus.Player[] SamplePlayers { get; set; } = Array.Empty<MCStatus.Player>();
    }
}