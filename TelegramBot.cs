using System.Net.Mime;
using Microsoft.Data.Sqlite;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;
using System;

namespace PurchaseBot;

public class TelegramBot
{
    private readonly TelegramBotClient _botClient;
    private readonly CancellationTokenSource _cts;
    private readonly SqliteConnection _db;

    public TelegramBot(string token, SqliteConnection db)
    {
        _botClient = new(token);
        _cts = new();
        _db = db;
    }

    public async Task StartBot()
    {
        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cts.Token
        );

        var me = await _botClient.GetMeAsync();

        Console.WriteLine($"Start listening for @{me.Username}");

        _db.Open();
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient,
        Update update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery is { } callbackQuery)
        {
            await HandleCallbackQueryAsync(callbackQuery, cancellationToken);
        }
        else if (update.Message is { } message)
        {
            await HandleMessageAsync(message, cancellationToken);
        }
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is not { } query)
            return;
        if (callbackQuery.Message is not { } message)
            return;

        var chatId = message.Chat.Id;

        string command = query.Split(' ')[0];

        Console.WriteLine($"Received a '{query}' query in chat {chatId}.");
        
        try
        {
            await _botClient.DeleteMessageAsync(
                chatId: chatId,
                messageId: message.MessageId,
                cancellationToken: cancellationToken
            );
        }
        catch{}
        
        switch (command)
        {
            case "/to_article":
                await HandleToArticleQueryAsync(chatId, query, cancellationToken);
                break;
            case "/to_help":
                await HandleHelpCommandAsync(chatId, cancellationToken);
                break;
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;

        messageText = messageText.ToLower();

        Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

        switch (messageText)
        {
            case "/start":
                await HandleStartCommandAsync(chatId, cancellationToken);
                break;
            case "помощь":
                await HandleAboutCommandAsync(chatId, cancellationToken);
                break;
            case "частное лицо":
                await HandleSetRoleQueryAsync(chatId, "regular", cancellationToken);
                break;
            case "предприниматель":
                await HandleSetRoleQueryAsync(chatId, "enterpreneur", cancellationToken);
                break;
            case "о закупках":
                await HandleHelpCommandAsync(chatId, cancellationToken);
                break;
        }
    }

    private async Task HandleStartCommandAsync(ChatId chatId, CancellationToken cancellationToken)
    {
        string meetGifUrl = "https://github.com/temirlanemilbekovtu/PurchaseBot/blob/main/anim/meet.gif?raw=true";

        /*SqliteDataReader userReader =
            await new SqliteCommand($"select * from users where user_id = {chatId};", _db)
                .ExecuteReaderAsync(cancellationToken);

        userReader.Read();
        
        if (userReader.HasRows)
        {
            SqliteCommand command = new SqliteCommand($"insert into users (user_id) values (@chatId);", _db);
            command.Parameters.AddWithValue("@chatId", chatId);
            command.ExecuteNonQuery();
            Console.WriteLine($"added {chatId}");
        }*/

        try
        {
            SqliteCommand command = new SqliteCommand("insert into users (user_id) values (@chatId);", _db);
            command.Parameters.AddWithValue("@chatId", chatId.Identifier);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        if (string.IsNullOrEmpty(await GetUserRoleAsync(chatId, cancellationToken)))
        {
            ReplyKeyboardMarkup setRoleKeyboard = new(
                new[]
                {
                    new KeyboardButton("частное лицо"),
                    new KeyboardButton("предприниматель")
                }
            ) { ResizeKeyboard = true };

            await _botClient.SendAnimationAsync(
                chatId: chatId,
                animation: InputFile.FromUri(meetGifUrl),
                caption: "Привет, давай определим кто ты:",
                replyMarkup: setRoleKeyboard,
                cancellationToken: cancellationToken
            );

            return;
        }

        ReplyKeyboardMarkup mainMenuKeyboard = new(
            new[]
            {
                new[]
                {
                    new KeyboardButton("Изменить роль")
                },
                new[]
                {
                    new KeyboardButton("О закупках")
                },
                new[]
                {
                    new KeyboardButton("Помощь"),
                    new KeyboardButton("Перейти к сайту")
                }
            }
        ) { ResizeKeyboard = true, OneTimeKeyboard = true };

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Ну-с, добро пожаловать",
            replyMarkup: mainMenuKeyboard,
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleHelpCommandAsync(ChatId chatId, CancellationToken cancellationToken)
    {
        string userRole = await GetUserRoleAsync(chatId, cancellationToken);
        SqliteDataReader articlesReader = await GetArticlesAsync(userRole);

        int articlesNum = await GetArticlesCountAsync(userRole);

        int[] articlesId = new int[articlesNum];
        string replyText = "Инфа: \n\n";

        for (int i = 0; articlesReader.Read(); i++)
        {
            var title = articlesReader["title"];
            var articleId = articlesReader["article_id"];
            replyText += $"{i + 1}. {title}\n\n";
            articlesId[i] = (int)(long)articleId;
        }

        int columnsNum = 3;
        int rowsNum = articlesNum / columnsNum + (articlesNum % columnsNum == 0 ? 0 : 1);

        InlineKeyboardButton[][] helpMenuButtons = new InlineKeyboardButton[rowsNum + 1][];

        for (int i = 0; i < articlesNum; i++)
        {
            int row = i / columnsNum;

            if (i % columnsNum == 0)
            {
                int rowLength = (articlesNum - i) < columnsNum ? articlesNum - i : columnsNum;
                helpMenuButtons[row] = new InlineKeyboardButton[rowLength];
            }

            helpMenuButtons[row][i % columnsNum] =
                InlineKeyboardButton.WithCallbackData(
                    text: $"{i + 1}",
                    callbackData: $"/to_article {articlesId[i]} {i + 1} {userRole}"
                );
        }

        helpMenuButtons[^1] =
            new[] { InlineKeyboardButton.WithCallbackData(text: "Назад", callbackData: $"/start") };

        InlineKeyboardMarkup helpMenuKeyboard = new(helpMenuButtons);

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: replyText,
            replyMarkup: helpMenuKeyboard,
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleAboutCommandAsync(ChatId chatId, CancellationToken cancellationToken)
    {
        string text = "О нас:/n";
        text = "/nВы можете:";

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: text,
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleSetRoleQueryAsync(ChatId chatId, string role,
        CancellationToken cancellationToken)
    {
        try
        {
            SqliteCommand updateRoleCommand =
                new($"update users set user_role = '{role}' where user_id = {chatId};", _db);
            updateRoleCommand.ExecuteNonQuery();

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Отлично! ты в бдшке",
                cancellationToken: cancellationToken
            );

            await HandleStartCommandAsync(chatId, cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Извини, видимо, что-то пошло не так, попробуй еще раз",
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task HandleToArticleQueryAsync(ChatId chatId, string query,
        CancellationToken cancellationToken)
    {
        InlineKeyboardMarkup? keyboard;
        string[] args = query.Split(" ");
        string content;

        FileStream? articleStream = null;

        try
        {
            int articleId = Int32.Parse(args[1]);
            int articleSn = Int32.Parse(args[2]);

            SqliteDataReader articlePathReader =
                await new SqliteCommand($"select content_path from articles where article_id = {articleId};", _db)
                    .ExecuteReaderAsync(cancellationToken);

            articlePathReader.Read();

            content = await File.ReadAllTextAsync(articlePathReader.GetString(0), cancellationToken);

            int articlesNum = await GetArticlesCountAsync(args[3]);

            int prevId = await GetPreviousArticleIdAsync(articleId, args[3]);
            int nextId = await GetNextArticleIdAsync(articleId, args[3]);

            if (articleSn == 1)
            {
                keyboard = new(
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(text: "Назад", callbackData: "/to_help"),
                        InlineKeyboardButton.WithCallbackData(text: ">>",
                            callbackData: $"/to_article {nextId} {articleSn + 1} {args[3]}"
                        )
                    }
                );
            }
            else if (articleSn == articlesNum)
            {
                keyboard = new(
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            text: "<<",
                            callbackData: $"/to_article {prevId} {articleSn - 1} {args[3]}"
                        ),
                        InlineKeyboardButton.WithCallbackData(text: "Назад", callbackData: "/to_help")
                    }
                );
            }
            else
            {
                keyboard = new(
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            text: "<<",
                            callbackData: $"/to_article {prevId} {articleSn - 1} {args[3]}"
                        ),
                        InlineKeyboardButton.WithCallbackData(text: "Назад", callbackData: "/to_help"),
                        InlineKeyboardButton.WithCallbackData(
                            text: ">>",
                            callbackData: $"/to_article {nextId} {articleSn + 1} {args[3]}"
                        )
                    }
                );
            }
        }
        finally
        {
            if (articleStream != null)
                articleStream.Close();
        }

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: content,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken
        );
    }

    private async Task<string> GetUserRoleAsync(ChatId chatId, CancellationToken cancellationToken)
    {
        try
        {
            var roleReader = await new SqliteCommand($"select user_role from users where user_id = {chatId};", _db)
                .ExecuteReaderAsync(cancellationToken);

            roleReader.Read();

            var res = roleReader.GetString(0);

            Console.WriteLine("success");

            return res;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return String.Empty;
        }
    }

    private async Task<int> GetNextArticleIdAsync(int id, string userRole)
    {
        try
        {
            Object? nextObj =
                await new SqliteCommand(
                        $"select article_id from articles " +
                        $"where article_id > {id} and user_role = '{userRole}' order by article_id limit 1;", _db)
                    .ExecuteScalarAsync();

            return Convert.ToInt32(nextObj ?? id);
        }
        catch
        {
            return id;
        }
    }

    private async Task<int> GetPreviousArticleIdAsync(int id, string userRole)
    {
        try
        {
            Object? prevObj =
                await new SqliteCommand(
                        $"select article_id from articles " +
                        $"where article_id < {id} and user_role = '{userRole}' order by article_id limit 1;", _db)
                    .ExecuteScalarAsync();
            
            return Convert.ToInt32(prevObj ?? id);
        }
        catch
        {
            return id;
        }
    }

    private async Task<int> GetArticlesCountAsync(string userRole)
    {
        try
        {
            Object? countObj =
                await new SqliteCommand(
                        $"select count(*) from articles where user_role = '{userRole}' order by article_id limit 1;",
                        _db)
                    .ExecuteScalarAsync();

            return Convert.ToInt32(countObj ?? 0);
        }
        catch
        {
            return 0;
        }
    }

    private Task<SqliteDataReader> GetArticlesAsync(string userRole)
    {
        SqliteDataReader reader =
            new SqliteCommand($"select * from articles where user_role = '{userRole}'", _db).ExecuteReader();

        return Task.FromResult(reader);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient,
        Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }

    public Task ExitBot()
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }
}
