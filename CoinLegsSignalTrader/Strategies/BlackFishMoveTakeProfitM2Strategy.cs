﻿using CoinLegsSignalTrader.Enums;
using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Helpers;
using CoinLegsSignalTrader.Interfaces;
using CoinLegsSignalTrader.Model;
using CoinLegsSignalTrader.Telegram;
using NLog;
using ILogger = NLog.ILogger;

namespace CoinLegsSignalTrader.Strategies
{
    /// <summary>
    ///     Strategy that moves take profit, starting with tp2
    ///     Take profit will be moved to last TP hit - 2 for TP 2 and 3 and to TP hit - 1 for TP 4
    ///     Position will be closed directly if TP5 is hit.
    /// </summary>
    public class BlackFishMoveTakeProfitM2Strategy : IStrategy
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SemaphoreSlim _waitHandle = new(1, 1);
        private INotification _notification;
        private IPosition _position;
        private ISignal _signal;
        private readonly TimeSpan _waitTimeout = TimeSpan.FromMinutes(1);

        public BlackFishMoveTakeProfitM2Strategy()
        {
            Id = Guid.NewGuid();
        }

        public static string Name => "BlackFishMoveTakeProfitM2Strategy";

        public async Task<bool> Execute(IExchange exchange, INotification notification, ISignal signal)
        {
            await _waitHandle.WaitAsync(_waitTimeout);
            try
            {
                _notification = notification;
                Exchange = exchange;
                _signal = signal;
                SymbolName = notification.SymbolName;

                var symbolExists = await Exchange.SymbolExists(_notification.SymbolName);
                if (!symbolExists)
                {
                    Logger.Info($"Symbol {_notification.SymbolName} not found on exchange {signal.Exchange}");
                    await TelegramBot.Instance.SendMessage($"Symbol {_notification.SymbolName} not found on exchange {signal.Exchange}");
                    return false;
                }

                var tickerDigits = await Exchange.GetSymbolDigits(_notification.SymbolName);
                _notification.Round(tickerDigits);

                RegisterExchangeEvents();

                var amount =
                    CalculationHelper.CalculateAmount(_signal.RiskPerTrade, _notification.StopLoss, _notification.SignalPrice);
                var order = await Exchange.PlaceOrderAsync(_notification.SymbolName, _notification.SignalPrice, _notification.Signal < 0, true, amount, _notification.StopLoss, _notification.Target5,
                    signal.Leverage);
                if (!order)
                {
                    UnregisterExchangeEvents();
                    return false;
                }

                return true;
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        public event EventHandler<PositionClosedEventArgs> OnPositionClosed;
        public IExchange Exchange { get; private set; }

        public string SymbolName { get; set; }
        public Guid Id { get; }

        private void RegisterExchangeEvents()
        {
            Exchange.OnOrderFilled += ExchangeOrderFilled;
            Exchange.OnTickerChanged += ExchangeOnTickerChanged;
            Exchange.OnPositionClosed += ExchangeOnPositionClosed;
        }

        private void ExchangeOnPositionClosed(object sender, PositionClosedEventArgs e)
        {
            _waitHandle.Wait(_waitTimeout);
            try
            {
                if (_notification.SymbolName != e.SymbolName)
                    return;

                UnregisterExchangeEvents();
                string message = string.Empty;
                if (_position != null)
                {
                    _position.ExitPrice = e.ExitPrice;
                    message =
                        $"Position closed for {_position.Notification.SymbolName}. Entry {Math.Round(_position.EntryPrice, _notification.Decimals)}, exit {Math.Round(_position.ExitPrice, _notification.Decimals)}, pnl {CalculationHelper.GetPnL(_position.Quantity, _position.EntryPrice, _position.ExitPrice, _position.IsShort)}";
                }
                else if (e.ClosedReason == PositionClosedReason.PositionCancled)
                {
                    message = $"Position cancled for {e.SymbolName} because of order timeout - was never opened!";
                }

                Logger.Info(message);
                TelegramBot.Instance.SendMessage(message).GetAwaiter().GetResult();

                OnPositionClosed?.Invoke(this, e);
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private void UnregisterExchangeEvents()
        {
            Exchange.OnOrderFilled -= ExchangeOrderFilled;
            Exchange.OnPositionClosed -= ExchangeOnPositionClosed;
            Exchange.OnTickerChanged -= ExchangeOnTickerChanged;
        }

        private void ExchangeOnTickerChanged(object sender, TickerUpdateEventArgs e)
        {
            if (_position?.Notification.SymbolName != e.SymbolName)
                return;

            _waitHandle.Wait(_waitTimeout);
            try
            {
                Logger.Debug($"Ticker updated for {_notification.SymbolName} to {Math.Round(e.LastPrice, _notification.Decimals)}");

                var needsUpdate = false;
                decimal stopLoss = 0;
                var lastPrice = e.LastPrice;
                _position!.LastPrice = e.LastPrice;
                if (_position.IsShort)
                {
                    if (lastPrice < _position.Notification.Target4)
                    {
                        stopLoss = _position.Notification.Target3;
                        needsUpdate = true;
                    }
                    else if (lastPrice < _position.Notification.Target3)
                    {
                        stopLoss = _position.Notification.Target1;
                        needsUpdate = true;
                    }
                    else if (lastPrice < _position.Notification.Target2)
                    {
                        stopLoss = _position.Notification.SignalPrice;
                        needsUpdate = true;
                    }

                    if (needsUpdate && stopLoss >= _position.LastLoss) needsUpdate = false;
                }
                else
                {
                    if (lastPrice > _position.Notification.Target4)
                    {
                        stopLoss = _position.Notification.Target3;
                        needsUpdate = true;
                    }
                    else if (lastPrice > _position.Notification.Target3)
                    {
                        stopLoss = _position.Notification.Target1;
                        needsUpdate = true;
                    }
                    else if (lastPrice > _position.Notification.Target2)
                    {
                        stopLoss = _position.Notification.SignalPrice;
                        needsUpdate = true;
                    }

                    if (needsUpdate && stopLoss <= _position.LastLoss) needsUpdate = false;
                }

                if (needsUpdate)
                {
                    _position.LastLoss = stopLoss;
                    Exchange.SetStopLoss(_position.Notification.SymbolName, _position.IsShort, stopLoss);
                    var message = $"Stop loss updated for {_notification.SymbolName} to {stopLoss}";
                    Logger.Info(message);
                    TelegramBot.Instance.SendMessage(message).GetAwaiter().GetResult();
                }
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private void ExchangeOrderFilled(object sender, OrderFilledEventArgs e)
        {
            _waitHandle.Wait(_waitTimeout);
            try
            {
                if(e.SymbolName != _notification.SymbolName)
                    return;

                if (_position != null)
                {
                    _position.Quantity += e.Quantity;
                    return;
                }

                var message = $"Position created for {_notification.SymbolName}, entry {Math.Round(e.EntryPrice, _notification.Decimals)}";
                Logger.Info(message);
                TelegramBot.Instance.SendMessage(message).GetAwaiter().GetResult();
                _position = new Position(_notification)
                {
                    EntryPrice = e.EntryPrice,
                    LastPrice = e.EntryPrice,
                    LastLoss = _notification.StopLoss,
                    Quantity = e.Quantity
                };
            }
            finally
            {
                _waitHandle.Release();
            }
        }
    }
}