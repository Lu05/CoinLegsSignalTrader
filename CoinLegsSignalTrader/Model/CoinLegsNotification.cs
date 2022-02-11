namespace CoinLegsSignalTrader.Model
{
    public class CoinLegsNotification
    {
        /*
         * Fields for Alerts And Custom Signals
         */
        public int? Type { get; set; }
        public string CustomSignalName { get; set; }
        public string Exchange { get; set; }
        public string MarketName { get; set; }
        public int? Period { get; set; }
        public int? Signal { get; set; }
        public DateTime? SignalDate { get; set; }

        /*
         * Fields for Coinlegs Market Place Signals
         */
        public int? SignalTypeId { get; set; }
        public decimal? SignalPrice { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? Target1 { get; set; }
        public decimal? Target2 { get; set; }
        public decimal? Target3 { get; set; }
        public decimal? Target4 { get; set; }
        public decimal? Target5 { get; set; }
        public bool? Closed { get; set; }
        public DateTimeOffset? CloseDate { get; set; }
        public decimal? ClosePrice { get; set; }
    }
}