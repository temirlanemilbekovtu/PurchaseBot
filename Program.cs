using Microsoft.Data.Sqlite;

namespace PurchaseBot;

public static class Program
{
    public static async Task Main(string[] args)
    {
        SqliteConnection db = new("Data Source=/home/temirlan/.sqlitedb/cerberus.db");
        db.Open();

        TelegramBot bot = new("6934414496:AAEaQ11l2KpCdpQSRLphy_mk0qLZe8w8UwI", db);
        await bot.StartBot();

        Console.WriteLine("To exit tap any key");
        Console.ReadKey();
        bot.ExitBot();

        Console.WriteLine();
    }
}