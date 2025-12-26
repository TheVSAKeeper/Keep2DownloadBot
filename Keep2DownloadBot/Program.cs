using Telegram.Bot;
using Telegram.Bot.Polling;

namespace Keep2DownloadBot;

internal static class Program
{
    private static async Task Main()
    {
        try
        {
            var config = Configuration.Load();
            var botClient = new TelegramBotClient(config.BotToken);
            var handler = new BotHandler(botClient);

            using var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = [],
            };

            botClient.StartReceiving(handler.HandleUpdateAsync,
                handler.HandleErrorAsync,
                receiverOptions,
                cts.Token);

            var me = await botClient.GetMe(cts.Token);
            Console.WriteLine($"Start listening for @{me.Username}");

            await Task.Delay(-1, cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
        }
    }
}
