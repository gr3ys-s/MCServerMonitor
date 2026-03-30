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

            var monitor = new MonitorService();
            var menu = new MenuManager(monitor);

            await menu.RunAsync();
        }
    }
}