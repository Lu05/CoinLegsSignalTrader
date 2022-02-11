namespace CoinLegsSignalTrader.EventArgs;

public class OrderFilledEventArgs : System.EventArgs
{
    public OrderFilledEventArgs(string symbolName, decimal entryPrice)
    {
        SymbolName = symbolName;
        EntryPrice = entryPrice;
    }

    public decimal EntryPrice { get; }

    public string SymbolName { get; }
}