using CoinLegsSignalTrader.EventArgs;

namespace CoinLegsSignalTrader.Interfaces
{
    public interface IStrategy
    {
        Guid Id { get; }
        Task<bool> Execute(IExchange exchange, INotification notification, ISignal signal);
        event EventHandler<PositionClosedEventArgs> OnPositionClosed;
    }
}