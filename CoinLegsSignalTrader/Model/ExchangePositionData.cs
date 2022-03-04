namespace CoinLegsSignalTrader.Model
{
    public class ExchangePositionData
    {
        public decimal UnrealizedPnL { get; set; }
        public decimal Quantity { get; set; }
        public decimal Margin { get; set; }
        public bool IsValid { get; set; }
    }
}
