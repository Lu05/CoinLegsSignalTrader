namespace CoinLegsSignalTrader.Interfaces
{
    public interface IPosition
    {
        INotification Notification { get; }
        bool IsShort { get; }
        decimal LastLoss { get; set; }
        decimal LastPrice { get; set; }
        decimal EntryPrice { get; set; }
        decimal ExitPrice { get; set; }
    }
}