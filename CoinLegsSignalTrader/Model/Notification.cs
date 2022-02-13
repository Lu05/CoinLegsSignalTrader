using CoinLegsSignalTrader.Interfaces;

namespace CoinLegsSignalTrader.Model
{
    public class Notification : INotification
    {
        public Notification(CoinLegsNotification notification)
        {
            Type = notification.Type ?? 0;
            if (!string.IsNullOrEmpty(notification.MarketName))
                SymbolName = notification.MarketName;
            Signal = notification.Signal ?? 0;
            SignalTypeId = notification.SignalTypeId ?? 0;
            SignalPrice = notification.SignalPrice ?? 0;
            StopLoss = notification.StopLoss ?? 0;
            Target1 = notification.Target1 ?? 0;
            Target2 = notification.Target2 ?? 0;
            Target3 = notification.Target3 ?? 0;
            Target4 = notification.Target4 ?? 0;
            Target5 = notification.Target5 ?? 0;
            Closed = notification.Closed ?? false;
        }

        public int Type { get; set; }
        public string SymbolName { get; set; } = string.Empty;
        public int Signal { get; set; }
        public int SignalTypeId { get; set; }
        public decimal SignalPrice { get; set; }
        public decimal StopLoss { get; set; }
        public decimal Target1 { get; set; }
        public decimal Target2 { get; set; }
        public decimal Target3 { get; set; }
        public decimal Target4 { get; set; }
        public decimal Target5 { get; set; }
        public bool Closed { get; set; }
        public int Decimals { get; set; }
        public void Round(int decimals)
        {
            Decimals = decimals;
            SignalPrice = Math.Round(SignalPrice, decimals);
            StopLoss = Math.Round(StopLoss, decimals);
            Target1 = Math.Round(Target1, decimals);
            Target2 = Math.Round(Target2, decimals);
            Target3 = Math.Round(Target3, decimals);
            Target4 = Math.Round(Target4, decimals);
            Target5 = Math.Round(Target5, decimals);
        }

        
    }
}