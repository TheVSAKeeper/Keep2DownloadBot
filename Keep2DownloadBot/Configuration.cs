using Microsoft.Extensions.Configuration;

namespace Keep2DownloadBot;

public class Configuration
{
    public string BotToken { get; set; } = string.Empty;
    public int ApiId { get; set; }
    public string ApiHash { get; set; } = string.Empty;

    public static Configuration Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true)
            .AddJsonFile("appsettings.Development.json", true)
            .AddEnvironmentVariables();

        var config = builder.Build();

        var token = config["BotToken"];
        var apiIdStr = config["ApiId"];
        var apiHash = config["ApiHash"];

        if (string.IsNullOrEmpty(token))
        {
            throw new("BotToken is not set in configuration.");
        }

        if (string.IsNullOrEmpty(apiIdStr) || !int.TryParse(apiIdStr, out var apiId))
        {
            throw new("ApiId is not set or invalid in configuration.");
        }

        if (string.IsNullOrEmpty(apiHash))
        {
            throw new("ApiHash is not set in configuration.");
        }

        return new()
        {
            BotToken = token,
            ApiId = apiId,
            ApiHash = apiHash,
        };
    }
}
