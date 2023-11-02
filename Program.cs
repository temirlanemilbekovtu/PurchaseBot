using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Components.Forms;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public static class Program
{
    public static async Task Main(string[] args)
    {
        SqliteConnection bd = new ("Data Source=/home/temirlan/.sqlitedb/cerberus.db");
        bd.Open();

        TelegramBot bot = new ("6934414496:AAEaQ11l2KpCdpQSRLphy_mk0qLZe8w8UwI", bd);
        await bot.StartBot();

        Console.Write("To exit tap any key: ");
        Console.ReadKey();
        bot.ExitBot();

        Console.WriteLine();
    }
}

public class TelegramBot
{
    private TelegramBotClient _botClient;
    private CancellationTokenSource _cts;
    private SqliteConnection _db;

    public TelegramBot(string token, SqliteConnection db)
    {
        _botClient = new (token);
        _cts = new();
        _db = db;
    }

    public async Task StartBot()
    {
        ReceiverOptions receiverOptions = new ()
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cts.Token
        );

        await _botClient.SetMyCommandsAsync
        (
            new BotCommand[]
            {
                new BotCommand { Command = "start", Description = "Начать"},
                new BotCommand { Command = "help", Description = "Помощь"},
                new BotCommand { Command = "subscribe", Description = "Подписаться на рассылку"},
                new BotCommand { Command = "unsubscribe", Description = "Отписаться от рассылки"},
                new BotCommand { Command = "switch_role", Description = "Переключить роль"}
            }
        );


        var me = await _botClient.GetMeAsync();

        Console.WriteLine($"Start listening for @{me.Username}");
    }

    public void ExitBot() => _cts.Cancel();

    private async Task HandleUpdateAsync(ITelegramBotClient botClient,
        Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
            return;
        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;

        switch (messageText)
        {
            case "/start":
                await HandleStartCommandAsync();
                break;

        }

        Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient,
        Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    private async Task HandleStartCommandAsync()
    {
        
    }

    private async Task HandleHelpCommandAsync()
    {

    }

    private async Task HandleSubscribeCommandAsync()
    {

    }

    private async Task HandleUnsubscribeCommandAsync()
    {

    }

    private async Task HandleSwitchRoleCommandAsync()
    {

    }
}
