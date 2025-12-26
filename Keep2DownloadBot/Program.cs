using Telegram.Bot;
using Telegram.Bot.Polling;
using Serilog;
using Microsoft.Extensions.Configuration;

namespace Keep2DownloadBot;

internal static class Program
{
    private static async Task Main()
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true)
                .AddJsonFile("appsettings.Development.json", true)
                .AddEnvironmentVariables()
                .Build())
            .CreateLogger();

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
            Log.Information("Start listening for @{Username}", me.Username);

            await Task.Delay(-1, cts.Token);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
