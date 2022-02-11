using Bybit.Net.Clients;
using Bybit.Net.Enums;
using Bybit.Net.Objects;
using Bybit.Net.Objects.Models.Socket;
using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Helpers;
using CoinLegsSignalTrader.Interfaces;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Newtonsoft.Json;
using NLog;
using ILogger = NLog.ILogger;

namespace CoinLegsSignalTrader.Exchanges.Bybit
{
    internal class BybitFuturesExchange : IExchange
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BybitClient _client;
        private readonly BybitSocketClient _socketClient;
        private readonly List<string> _symbols = new();
        private readonly Dictionary<string, int> _symbolSubscriptions = new();
        private readonly SemaphoreSlim _waitHandle = new(1, 1);

        public BybitFuturesExchange(BybitFuturesExchangeConfig config)
        {
            _client = new BybitClient(new BybitClientOptions
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey),
                UsdPerpetualApiOptions = new RestApiClientOptions(config.RestApiBaseAddress)
            });
            _socketClient = new BybitSocketClient(new BybitSocketClientOptions
            {
                ApiCredentials = new ApiCredentials(config.ApiKey, config.SecretKey),
                UsdPerpetualStreamsOptions = new BybitSocketApiClientOptions(
                    config.SocketPublicBaseAddress,
                    config.SocketPrivateBaseAddress),
                AutoReconnect = true
            });

            var subscription = _socketClient.UsdPerpetualStreams.SubscribeToUserTradeUpdatesAsync(TradeUpdates).GetAwaiter().GetResult();
            if (!subscription.Success)
            {
                Logger.Error($"Could not subscribe to UserTradeUpdates {subscription.Error}");
            }
        }

        public static string Name => "BybitFutures";

        public async Task<bool> PlaceOrderAsync(string symbolName, decimal signalPrice, bool isShort, bool isLimitOrder, decimal amount, decimal stopLoss, decimal takeProfit, decimal leverage)
        {
            await _waitHandle.WaitAsync(5000);
            try
            {
                if (_symbols.Contains(symbolName))
                    return false;
                
                var result = await _client.UsdPerpetualApi.Account.SetIsolatedPositionModeAsync(symbolName, true,
                    leverage, leverage);
                if (!result.Success)
                {
                    Logger.Info($"Could not update leverage {result.Error} - {symbolName}");
                }

                var side = isShort ? OrderSide.Sell : OrderSide.Buy;
                var orderType = isLimitOrder ? OrderType.Limit : OrderType.Market;
                
                var order = await _client.UsdPerpetualApi.Trading.PlaceOrderAsync(symbolName, side,
                    orderType, amount, TimeInForce.ImmediateOrCancel, false, false, null, null,
                    takeProfit, stopLoss, TriggerType.LastPrice, TriggerType.LastPrice);

                if (order.Success)
                {
                    Logger.Debug($"Order placed {JsonConvert.SerializeObject(order)}");
                    var subscription = await
                        _socketClient.UsdPerpetualStreams.SubscribeToTickerUpdatesAsync(symbolName,
                            TickerUpdateHandler);
                    if (subscription.Success)
                    {
                        Logger.Debug("Subscribed sucessfully!");
                        _symbolSubscriptions.Add(symbolName, subscription.Data.Id);

                        return true;
                    }

                    Logger.Error($"Failed on subscription {subscription.Error} - {symbolName}");
                }
                else
                {
                    Logger.Error($"Order failed {order.Error} - {symbolName}");
                }
            }


            finally
            {
                _waitHandle.Release();
            }

            return false;
        }

        public async Task<bool> SymbolExists(string symbolName)
        {
            var symbol = await _client.UsdPerpetualApi.ExchangeData.GetTickerAsync(symbolName);
            return symbol.Success && symbol.Data.Any();
        }

        public async Task<int> GetSymbolDigits(string symbolName)
        {
            var symbol = await _client.UsdPerpetualApi.ExchangeData.GetTickerAsync(symbolName);
            return CalculationHelper.GetDigits(symbol.Data.First().LastPrice);
        }

        public async Task<bool> SetStopLoss(string symbolName, bool isShort, decimal stopLoss)
        {
            await _waitHandle.WaitAsync(5000);
            try
            {
                var update = await
                    _client.UsdPerpetualApi.Trading.SetTradingStopAsync(symbolName, isShort ? PositionSide.Sell : PositionSide.Buy, null, stopLoss);
                if (!update.Success)
                {
                    Logger.Error($"Stop loss of {symbolName} could not be updates: {update.Error}");
                }
                return update.Success;
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        public event EventHandler<OrderFilledEventArgs> OnOrderFilled;
        public event EventHandler<PositionClosedEventArgs> OnPositionClosed;
        public event EventHandler<TickerUpdateEventArgs> OnTickerChanged;

        private void TradeUpdates(DataEvent<IEnumerable<BybitUserTradeUpdate>> obj)
        {
            _waitHandle.Wait(5000);
            try
            {
                var symbols = obj.Data.GroupBy(d => d.Symbol).ToDictionary(d => d.Key, d => d.ToList());
                foreach (var symbol in symbols)
                {
                    if (symbol.Value.Any(d => d.QuantityRemaining == 0))
                    {
                        Logger.Debug($"Symbol {symbol.Key} has quantity 0!");
                        if (!_symbols.Contains(symbol.Key))
                        {
                            _symbols.Add(symbol.Key);
                            var entryPrice = symbol.Value.Average(o => o.Price);
                            OnOrderFilled?.Invoke(this, new OrderFilledEventArgs(symbol.Key, entryPrice));
                        }
                        else
                        {
                            _symbols.Remove(symbol.Key);
                            var exitPrice = symbol.Value.Average(o => o.Price);
                            OnPositionClosed?.Invoke(this, new PositionClosedEventArgs(symbol.Key, exitPrice));
                            if (_symbolSubscriptions.TryGetValue(symbol.Key, out var id))
                            {
                                _socketClient.UnsubscribeAsync(id).GetAwaiter().GetResult();
                                _symbolSubscriptions.Remove(symbol.Key);
                            }
                        }
                    }
                }
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private void TickerUpdateHandler(DataEvent<BybitTickerUpdate> obj)
        {
            if (obj.Data.LastPrice != null)
            {
                OnTickerChanged?.Invoke(this, new TickerUpdateEventArgs(obj.Data.Symbol, (decimal)obj.Data.LastPrice));
            }
        }
    }
}