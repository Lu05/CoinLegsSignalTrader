using CoinLegsSignalTrader.Enums;

namespace CoinLegsSignalTrader.EventArgs;

public class PositionClosedEventArgs : System.EventArgs
{
    public PositionClosedEventArgs(string symbolName, decimal exitPrice, PositionClosedReason positionClosedReason)
    {
        ExitPrice = exitPrice;
        SymbolName = symbolName;
        ClosedReason = positionClosedReason;
    }

    public PositionClosedReason ClosedReason { get; set; }
    public decimal ExitPrice { get; }
    public string SymbolName { get; }
}