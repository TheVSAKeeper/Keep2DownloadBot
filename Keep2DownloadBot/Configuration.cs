using Microsoft.Extensions.Configuration;

namespace Keep2DownloadBot;

public class Configuration
{
    public string BotToken { get; set; } = string.Empty;

    public static Configuration Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true)
            .AddJsonFile("appsettings.Development.json", true)
            .AddEnvironmentVariables();

        var config = builder.Build();

        var token = config["BotToken"];
        if (string.IsNullOrEmpty(token))
        {
            throw new("BotToken is not set in appsettings.json or TELEGRAM_BOT_TOKEN environment variable.");
        }

        return new()
            { BotToken = token };
    }
}
