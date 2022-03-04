using CoinLegsSignalTrader.Enums;
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
    ///  This strategy has strailing stop loss with an offset where it starts to trail
    /// </summary>
    public class MarketPlaceTrailingStopLossStrategy : IStrategy
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SemaphoreSlim _waitHandle = new(1, 1);
        private INotification _notification;
        private IPosition _position;
        private ISignal _signal;
        private bool _isTrailingActive;

        public MarketPlaceTrailingStopLossStrategy()
        {
            Id = Guid.NewGuid();
        }

        public static string Name => "MarketPlaceTrailingStopLossStrategy";

        public async Task<bool> Execute(IExchange exchange, INotification notification, ISignal signal)
        {
            await _waitHandle.WaitAsync(5000);
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

                decimal takeProfit;
                decimal stopLoss;
                if (_notification.Signal < 0)
                {
                    takeProfit = Math.Round(notification.SignalPrice - (notification.SignalPrice * signal.TakeProfit), tickerDigits);
                    stopLoss = Math.Round(notification.SignalPrice + (notification.SignalPrice * signal.StopLoss), tickerDigits);
                }
                else
                {
                    takeProfit = Math.Round(notification.SignalPrice + (notification.SignalPrice * signal.TakeProfit), tickerDigits);
                    stopLoss = Math.Round(notification.SignalPrice - (notification.SignalPrice * signal.StopLoss), tickerDigits);
                }

                if (signal.UseStopLossFromSignal)
                {
                    stopLoss = _notification.StopLoss;
                }

                RegisterExchangeEvents();

                var amount =
                    CalculationHelper.CalculateAmount(_signal.RiskPerTrade, _notification.StopLoss, _notification.SignalPrice);
                var order = await Exchange.PlaceOrderAsync(_notification.SymbolName, _notification.SignalPrice, _notification.Signal < 0, true, amount, stopLoss, takeProfit,
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
            _waitHandle.Wait(5000);
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
            if (_position == null || _position.Notification.SymbolName != e.SymbolName)
                return;

            _waitHandle.Wait(5000);
            try
            {
                Logger.Debug($"Ticker updated for {_notification.SymbolName} to {Math.Round(e.LastPrice, _notification.Decimals)}");
                decimal stopLoss = 0;
                bool needsUpdate = false;
                if (_position.IsShort)
                {
                    if (!_isTrailingActive)
                    {
                        var offset = 1 - e.LastPrice / _position.EntryPrice;
                        if (offset > _signal.TrailingStartOffset)
                        {
                            Logger.Info($"Enabled trailing for {SymbolName} at {e.LastPrice}");
                            TelegramBot.Instance.SendMessage($"Enabled trailing for {SymbolName} at {e.LastPrice}");
                            _isTrailingActive = true;
                        }
                    }

                    if (!_isTrailingActive)
                        return;
                    var sl = e.LastPrice + e.LastPrice * _signal.TrailingOffset;
                    var digits = CalculationHelper.GetDigits(_notification.SignalPrice);
                    var slRound = Math.Round(sl, digits);
                    if (_position.LastLoss > slRound)
                    {
                        stopLoss = slRound;
                        needsUpdate = true;
                    }
                }
                else
                {
                    if (!_isTrailingActive)
                    {
                        var offset = e.LastPrice / _position.EntryPrice - 1;
                        if (offset > _signal.TrailingStartOffset)
                        {
                            Logger.Info($"Enabled trailing for {SymbolName} at {e.LastPrice}");
                            TelegramBot.Instance.SendMessage($"Enabled trailing for {SymbolName} at {e.LastPrice}");
                            _isTrailingActive = true;
                        }
                    }

                    if (!_isTrailingActive)
                        return;
                    var sl = e.LastPrice - e.LastPrice * _signal.TrailingOffset;
                    var digits = CalculationHelper.GetDigits(_notification.SignalPrice);
                    var slRound = Math.Round(sl, digits);
                    if (_position.LastLoss < slRound)
                    {
                        stopLoss = slRound;
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    _position.LastLoss = stopLoss;
                    Exchange.SetStopLoss(_position.Notification.SymbolName, _position.IsShort, stopLoss);
                    var message = $"Stop loss updated for {_notification.SymbolName} to {stopLoss}";
                    Logger.Debug(message);
                }
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private void ExchangeOrderFilled(object sender, OrderFilledEventArgs e)
        {
            if (e.SymbolName != SymbolName)
                return;

            _waitHandle.Wait(5000);
            try
            {
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