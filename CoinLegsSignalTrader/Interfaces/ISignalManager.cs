namespace CoinLegsSignalTrader.Interfaces;

public interface ISignalManager
{
    Task Execute(INotification notification);
    Task ExecuteRemoteCommand(IRemoteCommand command);
}