namespace CoinLegsSignalTrader.EventArgs;

public class OrderFilledEventArgs : System.EventArgs
{
    public OrderFilledEventArgs(string symbolName, decimal entryPrice, decimal quantity)
    {
        SymbolName = symbolName;
        EntryPrice = entryPrice;
        Quantity = quantity;
    }

    public decimal Quantity { get; }

    public decimal EntryPrice { get; }

    public string SymbolName { get; }
}