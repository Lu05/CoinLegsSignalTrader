using CoinLegsSignalTrader.Enums;
using CoinLegsSignalTrader.Interfaces;

namespace CoinLegsSignalTrader.Model
{
    public class Signal : ISignal
    {
        public int Type { get; set; }
        public int SignalTypeId { get; set; }
        public string Exchange { get; set; }
        public decimal Leverage { get; set; }
        public decimal RiskPerTrade { get; set; }
        public string SignalName { get; set; }
        public string Strategy { get; set; }
        public int TakeProfitIndex { get; set; }
        public bool UseStopLossFromSignal { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TrailingStartOffset { get; set; }
        public decimal TrailingOffset { get; set; }
        public SignalDirection Direction { get; set; }
        public bool IsActive { get; set; } = true;
        public decimal RiskFactor { get; set; } = 1.0M;
    }
}