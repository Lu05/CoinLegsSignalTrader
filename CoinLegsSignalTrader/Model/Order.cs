namespace CoinLegsSignalTrader.Model
{
    public class Order
    {
        public string Id { get; set; }
        public string Symbol { get; set; }
        public DateTime Timeout { get; set; }
    }
}
