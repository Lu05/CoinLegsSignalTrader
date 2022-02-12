using NLog;
using ILogger = NLog.ILogger;

namespace CoinLegsSignalTrader.Middleware
{
    public class CustomExceptionHandlerMiddleware
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly RequestDelegate _next;

        public CustomExceptionHandlerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (BadHttpRequestException ex)
            {
                Logger.Debug(ex, "BadHttpRequestException");
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Exception on middleware");
            }
        }
    }
}