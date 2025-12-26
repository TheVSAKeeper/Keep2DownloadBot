using Microsoft.Extensions.Configuration;
using Serilog;
using WTelegram;

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
            using var client = new Client(config.ApiId, config.ApiHash);

            var handler = new BotHandler(client);

            // Handle logging from WTelegramClient
            Helpers.Log = (lvl, str) =>
            {
                if (lvl >= 3)
                {
                    Log.Error(str);
                }
                else if (lvl == 2)
                {
                    Log.Warning(str);
                }
                else
                {
                    Log.Debug(str);
                }
            };

            await client.LoginBotIfNeeded(config.BotToken);
            Log.Information("Bot logged in as: {User}", client.User);

            //client.OnOther += handler.HandleUpdateAsync;

            Log.Information("Start listening for updates...");

            // Keep the app running
            var tcs = new TaskCompletionSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                tcs.TrySetResult();
            };

            await tcs.Task;
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
