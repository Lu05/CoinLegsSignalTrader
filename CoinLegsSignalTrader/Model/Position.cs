using CoinLegsSignalTrader.Interfaces;

namespace CoinLegsSignalTrader.Model;

internal class Position : IPosition
{
    public Position(INotification notification)
    {
        Notification = notification;
    }

    public INotification Notification { get; }

    public bool IsShort => Notification.Signal < 0;

    public decimal LastLoss { get; set; }
    public decimal LastPrice { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
}