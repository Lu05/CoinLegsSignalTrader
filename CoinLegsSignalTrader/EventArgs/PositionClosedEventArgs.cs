namespace CoinLegsSignalTrader.EventArgs;

public class PositionClosedEventArgs : System.EventArgs
{
    public PositionClosedEventArgs(string symbolName, decimal exitPrice)
    {
        ExitPrice = exitPrice;
        SymbolName = symbolName;
    }

    public decimal ExitPrice { get; }
    public string SymbolName { get; }
}