using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Helpers;
using CoinLegsSignalTrader.Interfaces;
using CoinLegsSignalTrader.Model;
using NLog;
using ILogger = NLog.ILogger;

namespace CoinLegsSignalTrader.Strategies
{
    /// <summary>
    /// Strategy that moves take profit, starting with tp2
    /// Take profit will be moved to last TP hit - 2 for TP 2 and 3 and to TP hit - 1 for TP 4
    /// Position will be closed directly if TP5 is hit.
    /// </summary>
    public class BlackFishMoveTakeProfitM2Strategy : IStrategy
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private IExchange _exchange;
        private INotification _notification;
        private IPosition _position;
        private ISignal _signal;
        private readonly SemaphoreSlim _waitHandle = new(1, 1);

        public BlackFishMoveTakeProfitM2Strategy()
        {
            Id = Guid.NewGuid();
        }

        public static string Name => "BlackFishMoveTakeProfitM2Strategy";

        public async Task<bool> Execute(IExchange exchange, INotification notification, ISignal signal)
        {
            await _waitHandle.WaitAsync(5000);
            try
            {
                _notification = notification;
                _exchange = exchange;
                _signal = signal;
                
                var symbolExists = await _exchange.SymbolExists(_notification.SymbolName);
                if (!symbolExists)
                {
                    Logger.Info($"Symbol {_notification.SymbolName} not found on exchange {signal.Exchange}");
                    return false;
                }

                var tickerDigits = await _exchange.GetSymbolDigits(_notification.SymbolName);
                _notification.Round(tickerDigits);

                RegisterExchangeEvents();

                var amount =
                    CalculationHelper.CalculateAmount(_signal.RiskPerTrade, _notification.StopLoss, _notification.SignalPrice);
                var order = await _exchange.PlaceOrderAsync(_notification.SymbolName, _notification.SignalPrice, _notification.Signal < 0, false, amount, _notification.StopLoss, _notification.Target5,
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

        private void RegisterExchangeEvents()
        {
            _exchange.OnOrderFilled += ExchangeOrderFilled;
            _exchange.OnTickerChanged += ExchangeOnTickerChanged;
            _exchange.OnPositionClosed += ExchangeOnPositionClosed;
        }

        public event EventHandler<PositionClosedEventArgs> OnPositionClosed;
        public Guid Id { get; }

        private void ExchangeOnPositionClosed(object sender, PositionClosedEventArgs e)
        {
            _waitHandle.Wait(5000);
            try
            {
                if (_position.Notification.SymbolName != e.SymbolName)
                    return;

                UnregisterExchangeEvents();
                if (_position != null)
                {
                    _position.ExitPrice = e.ExitPrice;
                }

                if (_position != null)
                {
                    Logger.Info(
                        $"Position closed for {_position.Notification.SymbolName}. Entry {_position.EntryPrice}, exit {_position.ExitPrice}, pnl {CalculationHelper.GetPnL(_position.EntryPrice, _position.ExitPrice, _position.IsShort, _signal.Leverage)}");
                }

                OnPositionClosed?.Invoke(this, e);
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private void UnregisterExchangeEvents()
        {
            _exchange.OnOrderFilled -= ExchangeOrderFilled;
            _exchange.OnPositionClosed -= ExchangeOnPositionClosed;
            _exchange.OnTickerChanged -= ExchangeOnTickerChanged;
        }

        private void ExchangeOnTickerChanged(object sender, TickerUpdateEventArgs e)
        {
            if (_position?.Notification.SymbolName != e.SymbolName)
                return;

            _waitHandle.Wait(5000);
            try
            {
                Logger.Debug($"Ticker updated for {_notification.SymbolName} to {Math.Round(e.LastPrice, 3)}");

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
                    _exchange.SetStopLoss(_position.Notification.SymbolName, _position.IsShort, stopLoss);
                    Logger.Info($"Stop loss updated for {_notification.SymbolName} to {stopLoss}");
                }
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private void ExchangeOrderFilled(object sender, OrderFilledEventArgs e)
        {
            _waitHandle.Wait(5000);
            try
            {
                if (_position != null)
                    return;

                Logger.Info($"Position created for {_notification.SymbolName}, entry {Math.Round(e.EntryPrice, 3)}");
                _position = new Position(_notification)
                {
                    EntryPrice = e.EntryPrice,
                    LastPrice = e.EntryPrice,
                    LastLoss = _notification.StopLoss
                };
            }
            finally
            {
                _waitHandle.Release();
            }
        }
    }
}