namespace CoinLegsSignalTrader.Interfaces
{
    public interface INotification
    {
        int Type { get; set; }
        string SymbolName { get; set; }
        int Signal { get; set; }
        int SignalTypeId { get; set; }
        decimal SignalPrice { get; set; }
        decimal StopLoss { get; set; }
        decimal Target1 { get; set; }
        decimal Target2 { get; set; }
        decimal Target3 { get; set; }
        decimal Target4 { get; set; }
        decimal Target5 { get; set; }
        bool Closed { get; set; }
        int Decimals { get; set; }
        void Round(int decimals);
    }
}