using CoinLegsSignalTrader.Enums;

namespace CoinLegsSignalTrader.Exchanges.Bybit
{
    public class BybitFuturesExchangeConfig
    {
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
        public string RestApiBaseAddress { get; set; }
        public string SocketPublicBaseAddress { get; set; }
        public string SocketPrivateBaseAddress { get; set; }
        public int OrderTimeOut { get; set; }
        public MarginMode MarginMode { get; set; }
    }
}