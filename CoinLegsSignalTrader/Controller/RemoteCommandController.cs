using System.Text.Json.Nodes;
using CoinLegsSignalTrader.Interfaces;
using CoinLegsSignalTrader.Model;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;
using ILogger = NLog.ILogger;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CoinLegsSignalTrader.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class RemoteCommandController : Microsoft.AspNetCore.Mvc.Controller
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ISignalManager _signalManager;

        public RemoteCommandController(ISignalManager signalManager)
        {
            _signalManager = signalManager;
        }

        [HttpPost("Execute")]
        public IActionResult Execute([FromBody] JsonObject content)
        {
            Task.Run(() =>
            {
                var command = JsonConvert.DeserializeObject<RemoteCommand>(content.ToString());
                Logger.Debug($"Command received: {JsonSerializer.Serialize(command)}");
                _signalManager.ExecuteRemoteCommand(command);
            });
            return Ok();
        }
    }
}
