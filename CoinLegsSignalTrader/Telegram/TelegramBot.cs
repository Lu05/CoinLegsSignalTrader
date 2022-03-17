using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Interfaces;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ILogger = NLog.ILogger;

namespace CoinLegsSignalTrader.Telegram
{
    public static class TelegramBot
    {
        private static ITelegramBot _instance;

        public static ITelegramBot Instance => _instance ??= new TelegramBotInstance();
    }

    public class TelegramBotInstance : ITelegramBot
    {
        private readonly ChatId _chatId;
        private readonly TelegramBotClient _client;
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public TelegramBotInstance()
        {
#if DEBUG
            return;
#endif

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, false)
                .AddJsonFile("appsettings.Development.json", true, false)
                .AddEnvironmentVariables().Build();

            var telegramConfig = config.GetSection("Telegram");
            var chatId = telegramConfig["ChatId"];
            var botToken = telegramConfig["BotToken"];
            if (chatId != null && botToken != null)
            {
                _client = new TelegramBotClient(botToken);
                _chatId = new ChatId(chatId);

                _client.SetMyCommandsAsync(new List<BotCommand>
                {
                    new()
                    {
                        Command = TelegramCommands.Ping,
                        Description = "Checks if the bot is alive"
                    },
                    new()
                    {
                        Command = TelegramCommands.GetOpenPositions,
                        Description = "Gets the open possitions"
                    },
                    new()
                    {
                        Command = TelegramCommands.GetPositionInfos,
                        Description = "Gets infos of open positions"
                    }
                }).GetAwaiter().GetResult();

                _client.StartReceiving(UpdateHandler, ErrorHandler);
            }
        }

        public async Task SendMessage(string message)
        {
            if (_client != null)
            {
                await _client.SendTextMessageAsync(_chatId, message);
            }
        }

        public event EventHandler<TelegramCommandEventArgs> OnCommand;

        private Task ErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            LogManager.GetCurrentClassLogger().Error(errorMessage);
            return Task.CompletedTask;
        }

        private async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Type != UpdateType.Message)
                return;
            // Only process text messages
            if (update.Message!.Type != MessageType.Text)
                return;

            var messageText = update.Message.Text;
            if (messageText != null && messageText.StartsWith('/'))
            {
                var commandName = messageText.Remove(0, 1);
                if (commandName == TelegramCommands.Ping || commandName == TelegramCommands.GetOpenPositions || commandName == TelegramCommands.GetPositionInfos)
                {
                    if (commandName == TelegramCommands.Ping)
                    {
                        await botClient.SendTextMessageAsync(_chatId, "pong", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        try
                        {
                            OnCommand?.Invoke(this, new TelegramCommandEventArgs(messageText.Remove(0, 1)));
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e);
                        }
                    }
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(_chatId, $"Unknown command {messageText}", cancellationToken: cancellationToken);
            }
        }
    }
}