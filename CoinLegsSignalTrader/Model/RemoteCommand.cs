using CoinLegsSignalTrader.Enums;
using CoinLegsSignalTrader.Interfaces;

namespace CoinLegsSignalTrader.Model
{
    public class RemoteCommand : IRemoteCommand
    {
        public RemoteCommandType Type { get; set; }
        public RemoteCommandTarget Target { get; set; }
        public decimal? RiskFactor { get; set; }
        public bool? IsSignalActive { get; set; }
    }
}
