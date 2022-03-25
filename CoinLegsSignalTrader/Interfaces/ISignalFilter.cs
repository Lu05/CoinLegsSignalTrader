namespace CoinLegsSignalTrader.Interfaces
{
    public interface ISignalFilter
    {
        public string Name { get; }
        Task<bool> Pass(ISignal signal, INotification notification, IExchange exchange);
    }
}
