namespace CoinLegsSignalTrader.EventArgs;

public class TickerUpdateEventArgs : System.EventArgs
{
    public TickerUpdateEventArgs(string symbolName, decimal lastPrice)
    {
        SymbolName = symbolName;
        LastPrice = lastPrice;
    }

    public decimal LastPrice { get; }
    public string SymbolName { get; }
}