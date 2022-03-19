using CoinLegsSignalTrader.Enums;

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
        public bool UseStopLossFromSignal { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TrailingStartOffset { get; set; }
        public decimal TrailingOffset { get; set; }
        public SignalDirection Direction { get; set; }
        public bool IsActive { get; set; }
        public decimal RiskFactor { get; set; }
    }
}