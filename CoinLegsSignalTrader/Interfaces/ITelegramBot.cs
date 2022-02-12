using CoinLegsSignalTrader.EventArgs;

namespace CoinLegsSignalTrader.Interfaces
{
    public interface ITelegramBot
    {
        Task SendMessage(string message);
        event EventHandler<TelegramCommandEventArgs> OnCommand;
    }
}