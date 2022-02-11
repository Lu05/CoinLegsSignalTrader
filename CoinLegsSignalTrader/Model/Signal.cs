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
    }
}