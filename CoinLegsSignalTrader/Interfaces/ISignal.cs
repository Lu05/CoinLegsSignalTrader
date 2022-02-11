namespace CoinLegsSignalTrader.Interfaces
{
    public interface ISignal
    {
        int Type { get; set; }
        int SignalTypeId { get; set; }
        string Exchange { get; set; }
        decimal Leverage { get; set; }
        decimal RiskPerTrade { get; set; }
        public string SignalName { get; set; }
        public string Strategy { get; set; }
        public int TakeProfitIndex { get; set; }
    }
}