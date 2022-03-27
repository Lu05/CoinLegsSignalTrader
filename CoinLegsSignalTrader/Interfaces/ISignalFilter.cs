namespace CoinLegsSignalTrader.Interfaces
{
    public interface ISignalFilter
    {
        public string Name { get; }
        public string Message { get; set; }
        Task<bool> Pass(ISignal signal, INotification notification, IExchange exchange);
    }
}
