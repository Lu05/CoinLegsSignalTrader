namespace CoinLegsSignalTrader.EventArgs
{
    public class TelegramCommandEventArgs : System.EventArgs
    {
        public TelegramCommandEventArgs(string command)
        {
            Command = command;
        }

        public string Command { get; }
    }
}