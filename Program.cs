using Microsoft.Data.Sqlite;
using System.IO;
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
        SqliteConnection db = new ("Data Source=/home/temirlan/.sqlitedb/cerberus.db");
        db.Open();

        TelegramBot bot = new ("6934414496:AAEaQ11l2KpCdpQSRLphy_mk0qLZe8w8UwI", db);
        await bot.StartBot();

        Console.WriteLine("To exit tap any key");
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
        ReceiverOptions receiverOptions = new () {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cts.Token
        );

        await _botClient.SetMyCommandsAsync(
            new BotCommand[] {
                new BotCommand { Command = "start", Description = "Начать"},
                new BotCommand { Command = "help", Description = "Помощь"},
                new BotCommand { Command = "subscribe", Description = "Подписаться на рассылку"},
                new BotCommand { Command = "unsubscribe", Description = "Отписаться от рассылки"},
                new BotCommand { Command = "switch_role", Description = "Переключить роль"}
            }
        );

        var me = await _botClient.GetMeAsync();

        Console.WriteLine($"Start listening for @{me.Username}");

        _db.Open();
    }

    public void ExitBot() => _cts.Cancel();

    private async Task HandleUpdateAsync(ITelegramBotClient botClient,
        Update update, CancellationToken cancellationToken)
    {
        if(update.Message is { } message)
        {
            var chatId = message.Chat.Id;
            await OnMessageAsync(botClient, chatId, message, cancellationToken);
        }
        else if(update.CallbackQuery is { } callbackQuery)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            await OnCallbackQueryAsync(botClient, chatId, callbackQuery, cancellationToken);
        }
    }

    private async Task OnMessageAsync(ITelegramBotClient botClient,
        ChatId chatId, Message message, CancellationToken cancellationToken)
    {
        if (message.Text is not { } messageText)
            return;

        string command = messageText.Split(' ')[0];

        switch (command)
        {
            case "/start":
                await HandleStartCommandAsync(chatId, cancellationToken);
                break;

        }

        Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");
    }

    private async Task OnCallbackQueryAsync(ITelegramBotClient botClient,
        ChatId chatId, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if(callbackQuery.Data is not { } query)
            return;

        string command = query.Split(' ')[0];

        switch (command)
        {
            case "/set_role":
                await HandleSetRoleQueryAsync(chatId, query, cancellationToken);
                break;
        }

        await _botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId);

        Console.WriteLine($"Received a '{query}' query in chat {chatId}.");
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

    private async Task HandleStartCommandAsync(ChatId chatId, CancellationToken cancellationToken)
    {
        FileStream gifStream = null;
        try{
            gifStream = new FileStream("/home/temirlan/code/PurchaseBot/anim/meet.gif",
                FileMode.Open, FileAccess.Read);
            await _botClient.SendAnimationAsync(chatId: chatId,
                animation: InputFile.FromStream(gifStream),
                caption: "Приветик!",
                cancellationToken: cancellationToken
            );
        }
        catch (Exception e) {
            Console.WriteLine(e.Message);
        }
        finally{
            if(gifStream != null)
                gifStream.Close();
        }

        SqliteDataReader idReader = new SqliteCommand($"select user_id from users where user_id = {chatId};", _db)
            .ExecuteReader();
        SqliteDataReader roleReader =
            new SqliteCommand($"select user_role from users where user_id = {chatId};", _db)
            .ExecuteReader();

        if(!idReader.HasRows)
        {
            new SqliteCommand($"insert into users (user_id) values ({chatId});", _db).ExecuteNonQuery();
        }
        if(!roleReader.HasRows)
        {
            InlineKeyboardMarkup inlineKeyboard = new (
                new [] {
                    InlineKeyboardButton.WithCallbackData(text: "обычный мэн",
                        callbackData: $"/set_role regular true"),
                    InlineKeyboardButton.WithCallbackData(text: "при деньгах",
                        callbackData: $"/set_role enterpreneur true"),
                }
            );

            Message sentMessage = await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Привет, давай определим кто ты:",
                replyMarkup: inlineKeyboard
            );
        }
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

    private async Task HandleSetRoleQueryAsync(ChatId chatId, string query,
        CancellationToken cancellationToken)
    {
        try {
            string[] args = query.Split(' ');

            SqliteCommand updateRoleCommand =
                new ($"update users set user_role = '{args[1]}' where user_id = {chatId};", _db);
            updateRoleCommand.ExecuteNonQuery();
            // updateRoleCommand.Parameters.AddWithValue("@UserRole", args[1]);
            // updateRoleCommand.Parameters.AddWithValue("@UserId", chatId);
            // updateRoleCommand.ExecuteNonQuery();

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Отлично!"
            );

            await HandleStartCommandAsync(chatId, cancellationToken);
        }
        catch (Exception e) {
            Console.WriteLine(e.Message);

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Извини, видимо, что-то пошло не так, попробуй еще раз"
            );
        }
    }
}
