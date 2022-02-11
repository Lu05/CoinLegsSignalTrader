using System.Runtime.InteropServices;
using CoinLegsSignalTrader.Helpers;
using NLog;
using LogLevel = NLog.LogLevel;

namespace CoinLegsSignalTrader
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var path = FileSystemHelper.GetBaseDirectory();
            LogManager.LoadConfiguration(Path.Combine(path, "nlog.config"));
            LogManager.Configuration.Variables["basedir"] = path;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            LogManager.GetCurrentClassLogger().Info(@$"public IP is {await GetIp()}");

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddEnvironmentVariables().Build();

            var port = config.GetValue<int>("Port");

            var builder = Host.CreateDefaultBuilder()
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration);
                    logging.AddConsole();
                }).ConfigureWebHostDefaults(webbuilder => { webbuilder.UseUrls($"http://0.0.0.0:{port}").UseStartup<Startup>(); });

            await builder.RunConsoleAsync();
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogManager.GetCurrentClassLogger().Log(LogLevel.Error, e.ExceptionObject);
        }

        private static async Task<string> GetIp()
        {
            using var client = new HttpClient();
            var response = await client.GetAsync("https://api.ipify.org");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}