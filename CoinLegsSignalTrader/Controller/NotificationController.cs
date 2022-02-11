using System.Text.Json;
using System.Text.Json.Nodes;
using CoinLegsSignalTrader.Interfaces;
using CoinLegsSignalTrader.Model;
using Microsoft.AspNetCore.Mvc;
using NLog;
using ILogger = NLog.ILogger;

namespace CoinLegsSignalTrader.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : Microsoft.AspNetCore.Mvc.Controller
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ISignalManager _signalManager;

        public NotificationController(ISignalManager signalManager)
        {
            _signalManager = signalManager;
        }

        [HttpPost("Listen")]
        public async Task<IActionResult> Notify([FromBody] JsonObject content)
        {
            var legsNotification = content.Deserialize<CoinLegsNotification>();

            if (legsNotification != null)
            {
                Logger.Debug($"Notification received: {content}");
                try
                {
                    var notification = new Notification(legsNotification);
                    await _signalManager.Execute(notification);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }

            return Ok();
        }
    }
}