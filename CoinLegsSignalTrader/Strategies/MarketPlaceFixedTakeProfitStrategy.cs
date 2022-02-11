using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Helpers;
using CoinLegsSignalTrader.Interfaces;
using CoinLegsSignalTrader.Model;
using NLog;
using ILogger = NLog.ILogger;

namespace CoinLegsSignalTrader.Strategies
{
    /// <summary>
    ///     Strategy with fixed take profit target based on the config file
    /// </summary>
    public class MarketPlaceFixedTakeProfitStrategy : IStrategy
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private IExchange _exchange;
        private INotification _notification;
        private IPosition _position;
        private ISignal _signal;
        private readonly SemaphoreSlim _waitHandle = new(1, 1);

        public MarketPlaceFixedTakeProfitStrategy()
        {
            Id = Guid.NewGuid();
        }

        public static string Name => "MarketPlaceFixedTakeProfitStrategy";

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

                decimal takeProfit;
                try
                {
                    takeProfit = (decimal)notification.GetType().GetProperty($"Target{signal.TakeProfitIndex}")?.GetValue(notification)!;
                }
                catch (Exception e)
                {
                    Logger.Info($"Could not read take profit for {notification.SymbolName} index {signal.TakeProfitIndex}");
                    Logger.Error(e);
                    return false;
                }

                RegisterExchangeEvents();

                var amount =
                    CalculationHelper.CalculateAmount(_signal.RiskPerTrade, _notification.StopLoss, _notification.SignalPrice);
                var order = await _exchange.PlaceOrderAsync(_notification.SymbolName, _notification.SignalPrice, _notification.Signal < 0, false, amount, _notification.StopLoss, takeProfit,
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