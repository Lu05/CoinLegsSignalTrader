using System.Timers;
using Bybit.Net.Clients;
using Bybit.Net.Enums;
using Bybit.Net.Objects;
using Bybit.Net.Objects.Models.Socket;
using CoinLegsSignalTrader.Enums;
using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Helpers;
using CoinLegsSignalTrader.Interfaces;
using CoinLegsSignalTrader.Telegram;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Newtonsoft.Json;
using NLog;
using ILogger = NLog.ILogger;
using Timer = System.Timers.Timer;

namespace CoinLegsSignalTrader.Exchanges.Bybit
{
    internal class BybitFuturesExchange : IExchange
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BybitClient _client;
        private readonly MarginMode _marginMode;
        private readonly int _maxPositions;
        private readonly int _orderTimeout;
        private readonly Dictionary<string, KeyValuePair<string, DateTime>> _orderTimeouts = new();
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
                UsdPerpetualStreamsOptions = new BybitSocketApiClientOptions
                {
                    BaseAddress = config.SocketPublicBaseAddress,
                    BaseAddressAuthenticated = config.SocketPrivateBaseAddress
                },
                AutoReconnect = true
            });

            _maxPositions = config.MaxOpenPositions;
            _orderTimeout = config.OrderTimeOut;
            _marginMode = config.MarginMode;

            var subscription = _socketClient.UsdPerpetualStreams.SubscribeToUserTradeUpdatesAsync(TradeUpdates).GetAwaiter().GetResult();
            if (!subscription.Success)
            {
                Logger.Error($"Could not subscribe to UserTradeUpdates {subscription.Error}");
            }

            var orderTimeoutTimer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds)
            {
                AutoReset = true
            };
            orderTimeoutTimer.Elapsed += OrderTimeoutOnElapsed;
            orderTimeoutTimer.Start();
        }

        public static string Name => "BybitFutures";

        public async Task<bool> PlaceOrderAsync(string symbolName, decimal signalPrice, bool isShort, bool isLimitOrder, decimal amount, decimal stopLoss, decimal takeProfit, decimal leverage)
        {
            await _waitHandle.WaitAsync(5000);
            try
            {
                if (_symbols.Contains(symbolName))
                {
                    Logger.Info($"Position for {symbolName} already opened!");
                    await TelegramBot.Instance.SendMessage($"Position for {symbolName} already opened!");
                    return false;
                }

                if (_symbols.Count > _maxPositions)
                {
                    Logger.Info($"Position limit reached: {_maxPositions}");
                    await TelegramBot.Instance.SendMessage($"Position limit reached: {_maxPositions}");
                    return false;
                }

                await UpdateMarginMode(symbolName, leverage);

                var side = isShort ? OrderSide.Sell : OrderSide.Buy;
                var orderType = isLimitOrder ? OrderType.Limit : OrderType.Market;

                var order = await _client.UsdPerpetualApi.Trading.PlaceOrderAsync(symbolName, side,
                    orderType, amount, TimeInForce.GoodTillCanceled, false, false, signalPrice,
                    null, takeProfit, stopLoss, TriggerType.LastPrice, TriggerType.LastPrice);

                if (order.Success)
                {
                    Logger.Debug($"Order placed {JsonConvert.SerializeObject(order)}");

                    Logger.Info($"Order successfully placed for {symbolName} at {signalPrice}!");
                    await TelegramBot.Instance.SendMessage($"Order successfully placed for {symbolName} at {signalPrice}!");

                    if (_orderTimeout > 0)
                    {
                        _orderTimeouts.Add(order.Data.Id, new KeyValuePair<string, DateTime>(symbolName, DateTime.Now.AddSeconds(_orderTimeout)));
                    }

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
                    var cancled = await _client.UsdPerpetualApi.Trading.CancelOrderAsync(symbolName, order.Data.Id);
                    if (!cancled.Success)
                    {
                        Logger.Info($"Could not cancel order for {symbolName}");
                        await TelegramBot.Instance.SendMessage($"Could not cancel order for {symbolName}");
                    }
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

        public async Task<decimal> GetUnrealizedPnlForSymbol(string symbolName)
        {
            var position = await _client.UsdPerpetualApi.Account.GetPositionAsync(symbolName);
            if (position.Success && position.Data.Any())
            {
                return position.Data.Sum(p => p.UnrealizedPnl);
            }

            return 0;
        }

        public event EventHandler<OrderFilledEventArgs> OnOrderFilled;
        public event EventHandler<PositionClosedEventArgs> OnPositionClosed;
        public event EventHandler<TickerUpdateEventArgs> OnTickerChanged;

        private void OrderTimeoutOnElapsed(object sender, ElapsedEventArgs e)
        {
            _waitHandle.Wait(5000);
            try
            {
                var keys = _orderTimeouts.Keys.ToList();
                foreach (var orderId in keys)
                {
                    var order = _client.UsdPerpetualApi.Trading.GetOrdersAsync(_orderTimeouts[orderId].Key, orderId).Result;
                    if (order.Success)
                    {
                        if (order.Data.Data.Any() && DateTime.Now > _orderTimeouts[orderId].Value)
                        {
                            var cancled = _client.UsdPerpetualApi.Trading.CancelOrderAsync(_orderTimeouts[orderId].Key, orderId).Result;
                            if (!cancled.Success)
                            {
                                Logger.Error($"Failed on cancel order {cancled.Error}");
                            }
                            else
                            {
                                _symbols.Remove(_orderTimeouts[orderId].Key);
                                _orderTimeouts.Remove(orderId);
                            }
                        }
                        else if (!order.Data.Data.Any())
                        {
                                       _symbols.Remove(_orderTimeouts[orderId].Key);
                            _orderTimeouts.Remove(orderId);
                        }
                    }
                }
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private async Task UpdateMarginMode(string symbolName, decimal leverage)
        {
            //first switch to not active mode, otherwise the leverage will not be updated
            WebCallResult marginUpdateResult;
            if (_marginMode == MarginMode.Isolated)
            {
                await _client.UsdPerpetualApi.Account.SetIsolatedPositionModeAsync(symbolName, false, leverage, leverage);
                marginUpdateResult = await _client.UsdPerpetualApi.Account.SetIsolatedPositionModeAsync(symbolName, true, leverage, leverage);
            }
            else
            {
                await _client.UsdPerpetualApi.Account.SetIsolatedPositionModeAsync(symbolName, true, leverage, leverage);
                marginUpdateResult = await _client.UsdPerpetualApi.Account.SetIsolatedPositionModeAsync(symbolName, false, leverage, leverage);
            }

            if (!marginUpdateResult.Success)
            {
                Logger.Info($"Could not update leverage {marginUpdateResult.Error} - {symbolName}");
                await TelegramBot.Instance.SendMessage($"Could not update leverage {marginUpdateResult.Error} - {symbolName}");
            }
        }

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
                            var quantity = symbol.Value.Sum(o => o.Quantity);

                            //remove order from timeouts
                            if (_orderTimeouts.Any(o => o.Value.Key == symbol.Key))
                            {
                                _orderTimeouts.Remove(_orderTimeouts.First(o => o.Value.Key == symbol.Key).Key);
                            }

                            OnOrderFilled?.Invoke(this, new OrderFilledEventArgs(symbol.Key, entryPrice, quantity));
                        }
                        else
                        {
                            var exitPrice = symbol.Value.Average(o => o.Price);
                            ClosePosition(symbol.Key, exitPrice);
                        }
                    }
                }
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private void ClosePosition(string symbolName, decimal exitPrice)
        {
            _symbols.Remove(symbolName);
            OnPositionClosed?.Invoke(this, new PositionClosedEventArgs(symbolName, exitPrice));
            if (_symbolSubscriptions.TryGetValue(symbolName, out var id))
            {
                _socketClient.UnsubscribeAsync(id).GetAwaiter().GetResult();
                _symbolSubscriptions.Remove(symbolName);
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