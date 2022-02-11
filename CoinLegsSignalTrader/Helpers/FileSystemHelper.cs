using System.Runtime.InteropServices;

namespace CoinLegsSignalTrader.Helpers
{
    public static class FileSystemHelper
    {
        public static string GetBaseDirectory()
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                path = Directory.GetCurrentDirectory();
            }

            return path;
        }
    }
}
