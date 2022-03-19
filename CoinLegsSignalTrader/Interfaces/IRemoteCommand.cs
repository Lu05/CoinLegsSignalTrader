using CoinLegsSignalTrader.Enums;

namespace CoinLegsSignalTrader.Interfaces
{
    public interface IRemoteCommand
    {
        RemoteCommandType Type { get; set; }
        RemoteCommandTarget Target { get; set; }
        decimal? RiskFactor { get; set; }
        bool? IsSignalActive { get; set; }
    }
}
