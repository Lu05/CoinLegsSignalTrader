﻿using System.Timers;
using Bybit.Net.Clients;
using Bybit.Net.Enums;
using Bybit.Net.Objects;
using Bybit.Net.Objects.Models.Socket;
using CoinLegsSignalTrader.Enums;
using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Helpers;
using CoinLegsSignalTrader.Interfaces;
using CoinLegsSignalTrader.Model;
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
        private readonly int _orderTimeout;
        private readonly List<Order> _orderTimeouts = new();
        private readonly Dictionary<string, DateTime> _positionTimeouts = new();
        private readonly BybitSocketClient _socketClient;
        private readonly List<string> _symbols = new();
        private readonly Dictionary<string, int> _symbolSubscriptions = new();
        private readonly SemaphoreSlim _waitHandle = new(1, 1);
        private readonly TimeSpan _waitTimeout = TimeSpan.FromMinutes(1);
        private readonly int _positionTimeout;

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

            _orderTimeout = config.OrderTimeout;
            _marginMode = config.MarginMode;
            _positionTimeout = config.PositionTimeout;

            var subscription = _socketClient.UsdPerpetualStreams.SubscribeToUserTradeUpdatesAsync(TradeUpdates).GetAwaiter().GetResult();
            if (!subscription.Success)
            {
                Logger.Error($"Could not subscribe to UserTradeUpdates {subscription.Error} - software is not working, please restart!");
            }

            var timeoutTimer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds)
            {
                AutoReset = true
            };
            timeoutTimer.Elapsed += TimeoutOnElapsed;
            timeoutTimer.Start();
        }

        public static string Name => "BybitFutures";

        public async Task<bool> PlaceOrderAsync(string symbolName, decimal signalPrice, bool isShort, bool isLimitOrder, decimal amount, decimal stopLoss, decimal takeProfit, decimal leverage)
        {
            await _waitHandle.WaitAsync(_waitTimeout);
            try
            {
                if (_symbols.Contains(symbolName))
                {
                    Logger.Info($"Position for {symbolName} already opened!");
                    await TelegramBot.Instance.SendMessage($"Position for {symbolName} already opened!");
                    return false;
                }

                await UpdateMarginMode(symbolName, leverage);

                var subscription = await _socketClient.UsdPerpetualStreams.SubscribeToTickerUpdatesAsync(symbolName, TickerUpdateHandler);
                if (!subscription.Success)
                {
                    Logger.Error($"Failed on subscription {subscription.Error} - {symbolName} - no order placed");
                    await TelegramBot.Instance.SendMessage($"Failed on subscription {subscription.Error} - {symbolName} - no order placed");
                    return false;
                }
                _symbolSubscriptions.Add(symbolName, subscription.Data.Id);
                
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
                        _orderTimeouts.Add(new Order
                        {
                            Id = order.Data.Id,
                            Symbol = symbolName,
                            Timeout = DateTime.Now.AddSeconds(_orderTimeout)
                        });
                    }

                    if (_positionTimeout > 0)
                    {
                        _positionTimeouts.Add(symbolName, DateTime.Now.AddSeconds(_positionTimeout));
                    }

                    return true;
                }
                else
                {
                    Logger.Error($"Order failed {order.Error} - {symbolName}");
                    await _socketClient.UnsubscribeAsync(subscription.Data.Id);
                    _symbolSubscriptions.Remove(symbolName);
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
            await _waitHandle.WaitAsync(_waitTimeout);
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

        public async Task<ExchangePositionData> GetUnrealizedPnlForSymbol(string symbolName)
        {
            var position = await _client.UsdPerpetualApi.Account.GetPositionAsync(symbolName);
            if (position.Success && position.Data.Any())
            {
                var openPositions = position.Data.Where(p => p.Quantity > 0).ToList();
                return new ExchangePositionData
                {
                    UnrealizedPnL = openPositions.Sum(p => p.UnrealizedPnl),
                    Quantity = openPositions.Sum(p => p.Quantity),
                    Margin = openPositions.Sum(p => p.PositionMargin),
                    IsValid = true
                };
            }

            return new ExchangePositionData
            {
                IsValid = false
            };
        }

        public event EventHandler<OrderFilledEventArgs> OnOrderFilled;
        public event EventHandler<PositionClosedEventArgs> OnPositionClosed;
        public event EventHandler<TickerUpdateEventArgs> OnTickerChanged;

        private void TimeoutOnElapsed(object sender, ElapsedEventArgs e)
        {
            _waitHandle.Wait(_waitTimeout);
            try
            {
                foreach (var timeoutOrder in _orderTimeouts.ToList())
                {
                    var order = _client.UsdPerpetualApi.Trading.GetOrdersAsync(timeoutOrder.Symbol, timeoutOrder.Id).GetAwaiter().GetResult();
                    if (order.Success)
                    {
                        var orderItem = order.Data.Data.FirstOrDefault();

                        if (orderItem != null && DateTime.Now > timeoutOrder.Timeout)
                        {
                            var cancled = _client.UsdPerpetualApi.Trading.CancelOrderAsync(timeoutOrder.Symbol, timeoutOrder.Id).GetAwaiter().GetResult();
                            if (!cancled.Success)
                            {
                                Logger.Debug(cancled.Error);
                            }
                            
                            //only remove from symbols if no position has been created
                            bool needSymbolRemove = orderItem.Status != OrderStatus.PartiallyFilled && orderItem.Status != OrderStatus.Filled;
                            if (needSymbolRemove)
                            {
                                Logger.Debug($"Position closed because order timeout {timeoutOrder.Symbol}");
                                ClosePosition(timeoutOrder.Symbol, 0, PositionClosedReason.PositionCancled);
                            }
                            else
                            {
                                _orderTimeouts.Remove(timeoutOrder);
                                var remaining = orderItem.Quantity - orderItem.QuoteQuantityFilled;
                                //Only send a notification if there was remaining quantity
                                if(remaining > 0.1M)
                                {  
                                    var msg = $"Order {timeoutOrder.Symbol} cancled - remaining {remaining} of {orderItem.Quantity}";
                                    Logger.Info(msg);
                                    TelegramBot.Instance.SendMessage(msg);
                                }
                            }
                        }
                    }
                }

                foreach (var positionTimeout in _positionTimeouts)
                {
                    if (DateTime.Now > positionTimeout.Value)
                    {
                        var positions = _client.UsdPerpetualApi.Account.GetPositionAsync(positionTimeout.Key).GetAwaiter().GetResult();
                        if (positions.Success)
                        {
                            var pos = positions.Data.FirstOrDefault(p => p.Quantity > 0);
                            if (pos != null)
                            {
                                var side = pos.Side == PositionSide.Buy ? OrderSide.Sell : OrderSide.Buy;
                                var qty = pos.Quantity;
                                var result = _client.UsdPerpetualApi.Trading.PlaceOrderAsync(pos.Symbol, side, OrderType.Market, qty, TimeInForce.FillOrKill, true, true).GetAwaiter().GetResult();
                                if (result.Success)
                                {
                                    Logger.Info($"Closed position by timeout {pos.Symbol}");
                                    TelegramBot.Instance.SendMessage($"Closed position by timeout {pos.Symbol}").GetAwaiter().GetResult();
                                }
                                else
                                {
                                    Logger.Error(result.Error);
                                }
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
            _waitHandle.Wait(_waitTimeout);
            try
            {
                var symbols = obj.Data.GroupBy(d => d.Symbol).ToDictionary(d => d.Key, d => d.ToList());
                foreach (var symbol in symbols)
                {
                    var positions = _client.UsdPerpetualApi.Account.GetPositionAsync(symbol.Key).GetAwaiter().GetResult();
                    bool isOpen = positions.Data.Sum(d => d.Quantity) > 0;

                    if (isOpen)
                    {
                        if (!_symbols.Contains(symbol.Key))
                        {
                            _symbols.Add(symbol.Key);
                        }

                        var entryPrice = symbol.Value.Average(o => o.Price);
                        var quantity = symbol.Value.Sum(o => o.Quantity);

                        OnOrderFilled?.Invoke(this, new OrderFilledEventArgs(symbol.Key, entryPrice, quantity));
                    }
                    else
                    {
                        var exitPrice = symbol.Value.Average(o => o.Price);
                        ClosePosition(symbol.Key, exitPrice, PositionClosedReason.PositionClosedSell);
                    }
                }
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private void ClosePosition(string symbolName, decimal exitPrice, PositionClosedReason reason)
        {
            _symbols.Remove(symbolName);
            OnPositionClosed?.Invoke(this, new PositionClosedEventArgs(symbolName, exitPrice, reason));
            if (_symbolSubscriptions.TryGetValue(symbolName, out var id))
            {
                _socketClient.UnsubscribeAsync(id).GetAwaiter().GetResult();
                _symbolSubscriptions.Remove(symbolName);
            }

            var orderTimeout = _orderTimeouts.FirstOrDefault(o => o.Symbol == symbolName);
            if (orderTimeout != null)
            {
                _client.UsdPerpetualApi.Trading.CancelOrderAsync(orderTimeout.Symbol, orderTimeout.Id).GetAwaiter().GetResult();
                _orderTimeouts.Remove(orderTimeout);
            }
            if (_positionTimeouts.ContainsKey(symbolName))
            {
                _positionTimeouts.Remove(symbolName);
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