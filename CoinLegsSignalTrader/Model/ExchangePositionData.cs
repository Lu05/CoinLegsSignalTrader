namespace CoinLegsSignalTrader.Model
{
    public class ExchangePositionData
    {
        public string Symbol { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public decimal Quantity { get; set; }
        public decimal Margin { get; set; }
        public bool IsValid { get; set; }
        public decimal Leverage { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal StopLoss { get; set; }
        public decimal PositionSize { get; set; }
        public bool IsShort { get; set; }

        public string AsString()
        {
            var directionSymbol = IsShort ? "📕" : "📗";
            return $"{Symbol} {directionSymbol}" + Environment.NewLine +
                   $"   UnrealizedPnL = {Math.Round(UnrealizedPnL, 2)}$" + Environment.NewLine +
                   $"   Quantity = {Math.Round(Quantity, 3)}" + Environment.NewLine +
                   $"   Margin = {Math.Round(Margin, 2)}$" + Environment.NewLine +
                   $"   Leverage = {Math.Round(Leverage, 2)}" + Environment.NewLine +
                   $"   TP = {Math.Round(TakeProfit, 4)}$" + Environment.NewLine +
                   $"   SL = {Math.Round(StopLoss, 4)}$" + Environment.NewLine +
                   $"   Size = {Math.Round(PositionSize, 2)}$";
        }
    }
}
