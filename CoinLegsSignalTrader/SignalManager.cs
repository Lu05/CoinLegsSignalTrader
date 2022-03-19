using System.Text.Json;
using CoinLegsSignalTrader.Enums;
using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Exchanges.Bybit;
using CoinLegsSignalTrader.Interfaces;
using CoinLegsSignalTrader.Model;
using CoinLegsSignalTrader.Strategies;
using CoinLegsSignalTrader.Telegram;
using NLog;
using ILogger = NLog.ILogger;

namespace CoinLegsSignalTrader
{
    public class SignalManager : ISignalManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, IExchange> _exchanges = new();
        private readonly List<ISignal> _signals = new();
        private readonly Dictionary<Guid, IStrategy> _strategies = new();
        private readonly int _maxPositions;
        private readonly SemaphoreSlim _waitHandle = new(1, 1);
        private readonly TimeSpan _waitTimeout = TimeSpan.FromMinutes(2);

        public SignalManager(IConfiguration config)
        {
            TelegramBot.Instance.OnCommand += TelegramBotOnCommand;
            _maxPositions = config.GetValue<int>("MaxPositions");

            foreach (var configuration in config.GetSection("Exchanges").GetChildren())
            {
                if (configuration["Name"] == BybitFuturesExchange.Name)
                {
                    Logger.Debug($"Adding {BybitFuturesExchange.Name} exchange");
                    var exchangeConfig = new BybitFuturesExchangeConfig();
                    configuration.Bind(exchangeConfig);
                    var exchange = new BybitFuturesExchange(exchangeConfig);
                    _exchanges.Add(BybitFuturesExchange.Name, exchange);
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
                Logger.Info("No signals configured!");
                await TelegramBot.Instance.SendMessage("No signals configured!");
                return;
            }

            if (_exchanges.Count == 0)
            {
                Logger.Info("No exchanges configured!");
                await TelegramBot.Instance.SendMessage("No exchanges configured!");
                return;
            }

            if (_strategies.Count >= _maxPositions)
            {
                Logger.Info($"Max positions reached {_maxPositions}!");
                await TelegramBot.Instance.SendMessage($"Max positions reached {_maxPositions}!");
                return;
            }

            await _waitHandle.WaitAsync(_waitTimeout);

            try
            {
                foreach (var signal in _signals)
                {
                    Logger.Debug($"Executing {JsonSerializer.Serialize(signal)}");
                    if (signal.Type == notification.Type && signal.SignalTypeId == notification.SignalTypeId)
                    {
                        if (_exchanges.TryGetValue(signal.Exchange, out var exchange))
                        {
                            Logger.Info($"Found exchange {signal.Exchange} - {notification.SymbolName}");
                            await TelegramBot.Instance.SendMessage($"Found exchange {signal.Exchange} - {notification.SymbolName}");
                            var strategy = GetStrategyByName(signal.Strategy);
                            if (strategy != null)
                            {
                                Logger.Debug($"Strategy found {signal.Strategy} - {notification.SymbolName}");
                                if (await strategy.Execute(exchange, notification, signal))
                                {
                                    Logger.Debug($"Strategy executed {signal.Strategy} on {signal.Exchange} - {notification.SymbolName}");
                                    strategy.OnPositionClosed += SignalOnPositionClosed;
                                    _strategies.Add(strategy.Id, strategy);
                                    break;
                                }
                            }
                            else
                            {
                                Logger.Info($"No strategy found for {signal.Strategy}");
                                await TelegramBot.Instance.SendMessage($"No strategy found for {signal.Strategy}");
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

        public async Task ExecuteRemoteCommand(IRemoteCommand command)
        {
            await _waitHandle.WaitAsync(_waitTimeout);

            try
            {
                switch (command.Type)
                {
                    case RemoteCommandType.ChangeStrategyState:
                    {
                        var signals = GetSignalsByDirection(command.Target);
                        foreach (var signal in signals)
                        {
                            if (command.IsSignalActive != null)
                            {
                                Logger.Debug($"Set state of {signal.Strategy} to {command.IsSignalActive}");
                                signal.IsActive = (bool)command.IsSignalActive;
                            }
                        }

                        break;
                    }
                    case RemoteCommandType.ChangeStrategyRisk:
                    {
                        var signals = GetSignalsByDirection(command.Target);
                        foreach (var signal in signals)
                        {
                            if (command.RiskFactor != null)
                            {
                                var factor = Math.Min((decimal)command.RiskFactor, 1);
                                Logger.Debug($"Set risk factor of {signal.Strategy} to {factor}");
                                signal.RiskFactor = factor;
                            }
                        }

                        break;
                    }
                }
            }
            finally
            {
                _waitHandle.Release();
            }
        }

        private IEnumerable<ISignal> GetSignalsByDirection(RemoteCommandTarget commandTarget)
        {
            foreach (var signal in _signals)
            {
                switch (commandTarget)
                {
                    case RemoteCommandTarget.All:
                        yield return signal;
                        break;
                    case RemoteCommandTarget.Long when signal.Direction == SignalDirection.Long:
                        yield return signal;
                        break;
                    case RemoteCommandTarget.Short when signal.Direction == SignalDirection.Short:
                        yield return signal;
                        break;
                }
            }
        }

        private void TelegramBotOnCommand(object sender, TelegramCommandEventArgs e)
        {
            if (e.Command == TelegramCommands.GetOpenPositions)
            {
                if (_strategies.Count == 0)
                {
                    TelegramBot.Instance.SendMessage("No open positions!");
                }
                else
                {
                    var positions = _strategies.Select(s => s.Value.SymbolName);
                    TelegramBot.Instance.SendMessage(string.Join(Environment.NewLine, positions));
                }
            }
            else if (e.Command == TelegramCommands.GetPositionInfos)
            {
                if (_strategies.Count == 0)
                {
                    TelegramBot.Instance.SendMessage("No open positions!");
                }
                else
                {
                    var result = new List<string>();
                    foreach (var strategy in _strategies)
                    {
                        var data = strategy.Value.Exchange.GetPositionInfos(strategy.Value.SymbolName).GetAwaiter().GetResult();
                        if(data.IsValid)
                        {
                            result.Add(data.AsString());
                        }
                        else
                        {
                            result.Add($"{strategy.Value.SymbolName} -> Unknown");
                        }
                    }

                    TelegramBot.Instance.SendMessage(string.Join(Environment.NewLine, result));
                }
            }
        }

        private IStrategy GetStrategyByName(string strategyName)
        {
            if (strategyName == BlackFishMoveTakeProfitM2Strategy.Name)
                return new BlackFishMoveTakeProfitM2Strategy();
            if (strategyName == MarketPlaceFixedTakeProfitStrategy.Name)
                return new MarketPlaceFixedTakeProfitStrategy();
            if (strategyName == MarketPlaceTrailingStopLossStrategy.Name)
                return new MarketPlaceTrailingStopLossStrategy();
            if (strategyName == MarketPlaceCustomTakeProfitStrategy.Name)
                return new MarketPlaceCustomTakeProfitStrategy();
            return null;
        }

        private void SignalOnPositionClosed(object sender, PositionClosedEventArgs e)
        {
            if (sender is IStrategy strategy)
            {
                Logger.Debug($"Removing position from manager {strategy.Id}");
                strategy.OnPositionClosed -= SignalOnPositionClosed;
                _strategies.Remove(strategy.Id);
            }
        }
    }
}