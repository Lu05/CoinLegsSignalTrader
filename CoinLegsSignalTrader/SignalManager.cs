using System.Text.Json;
using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Exchanges.Bybit;
using CoinLegsSignalTrader.Interfaces;
using CoinLegsSignalTrader.Model;
using CoinLegsSignalTrader.Strategies;
using NLog;
using ILogger = NLog.ILogger;

namespace CoinLegsSignalTrader
{
    public class SignalManager : ISignalManager
    {
        private readonly Dictionary<string, IExchange> _exchanges = new();
        private readonly List<ISignal> _signals = new();
        private readonly Dictionary<Guid, IStrategy> _strategies = new();
        private static ILogger _logger = LogManager.GetCurrentClassLogger();

        public SignalManager(IConfiguration config)
        {
            foreach (var configuration in config.GetSection("Exchanges").GetChildren())
            {
                if (configuration["Name"] == BybitFuturesExchange.Name)
                {
                    _logger.Debug($"Adding {BybitFuturesExchange.Name} exchange");
                    var exchangeConfig = new BybitFuturesExchangeConfig();
                    configuration.Bind(exchangeConfig);
                    _exchanges.Add(BybitFuturesExchange.Name, new BybitFuturesExchange(exchangeConfig));
                }
            }

            foreach (var signalConfig in config.GetSection("Signals").GetChildren())
            {
                ISignal signal = new Signal();
                signalConfig.Bind(signal);
                _signals.Add(signal);
            }
        }

        public async Task Execute(INotification notification)
        {
            if (_signals.Count == 0)
            {
                _logger.Info("No signals configured!");
            }
            else if (_exchanges.Count == 0)
            {
                _logger.Info("No exchanges configured!");
            }
            foreach (var signal in _signals)
            {
                _logger.Debug($"Executing {JsonSerializer.Serialize(signal)}");
                if (signal.Type == notification.Type && signal.SignalTypeId == notification.SignalTypeId)
                    if (_exchanges.TryGetValue(signal.Exchange, out var exchange))
                    {
                        _logger.Info($"Found exchange {signal.Exchange} - {notification.SymbolName}");
                        var strategy = GetStrategyByName(signal.Strategy);
                        if (strategy != null)
                        {
                            _logger.Debug($"Strategy found {signal.Strategy} - {notification.SymbolName}");
                            if (await strategy.Execute(exchange, notification, signal))
                            {
                                _logger.Debug($"Strategy executed {signal.Strategy} on {signal.Exchange} - {notification.SymbolName}");
                                strategy.OnPositionClosed += SignalOnPositionClosed;
                                _strategies.Add(strategy.Id, strategy);
                                break;
                            }
                        }
                        else
                        {
                            _logger.Info($"No strategy found for {signal.Strategy}");
                        }
                    }
            }
        }

        private IStrategy GetStrategyByName(string strategyName)
        {
            if (strategyName == BlackFishMoveTakeProfitM2Strategy.Name)
                return new BlackFishMoveTakeProfitM2Strategy();
            if (strategyName == MarketPlaceFixedTakeProfitStrategy.Name)
                return new MarketPlaceFixedTakeProfitStrategy();
            return null;
        }

        private void SignalOnPositionClosed(object sender, PositionClosedEventArgs e)
        {
            if (sender is IStrategy strategy)
            {
                _logger.Debug($"Removing position from manager {strategy.Id}");
                strategy.OnPositionClosed -= SignalOnPositionClosed;
                _strategies.Remove(strategy.Id);
            }
        }
    }
}