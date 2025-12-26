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
            config.EnsureDirectoriesExist();

            await using var client = new Client(config.ApiId, config.ApiHash);

            var handler = new BotHandler(client, config);

            Helpers.Log = (lvl, str) =>
            {
                switch (lvl)
                {
                    case >= 4:
                        Log.Error(str);
                        break;

                    case 3:
                        Log.Warning(str);
                        break;

                    case 2:
                        Log.Information(str);
                        break;

                    default:
                        Log.Debug(str);
                        break;
                }
            };

            await client.LoginBotIfNeeded(config.BotToken);
            Log.Information("Bot logged in as: {User}", client.User);

            client.OnOther += o =>
            {
                Log.Information("Client other: {Object}", o);
                return Task.CompletedTask;
            };

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
