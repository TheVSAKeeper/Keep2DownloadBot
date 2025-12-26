using Telegram.Bot;
using Telegram.Bot.Types;

namespace Keep2DownloadBot;

public class BotHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly Dictionary<string, List<Message>> _mediaGroups = new();

    public BotHandler(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
        {
            return;
        }

        // We are interested in Video, Document (if it's a video), or MediaGroups
        if (message.Video != null || message.Document != null && message.Document.MimeType?.StartsWith("video/") == true)
        {
            if (message.MediaGroupId != null)
            {
                await HandleMediaGroupAsync(message, cancellationToken);
            }
            else
            {
                await ProcessVideoAsync(message, cancellationToken);
            }
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error: {exception.Message}");
        return Task.CompletedTask;
    }

    private async Task HandleMediaGroupAsync(Message message, CancellationToken cancellationToken)
    {
        var groupId = message.MediaGroupId!;

        lock (_mediaGroups)
        {
            if (!_mediaGroups.ContainsKey(groupId))
            {
                _mediaGroups[groupId] = new();
                // Schedule processing after a short delay to ensure all messages in the group are received
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000, cancellationToken);
                    List<Message> messages;
                    lock (_mediaGroups)
                    {
                        messages = _mediaGroups[groupId];
                        _mediaGroups.Remove(groupId);
                    }

                    foreach (var msg in messages)
                    {
                        await ProcessVideoAsync(msg, cancellationToken);
                    }
                }, cancellationToken);
            }

            _mediaGroups[groupId].Add(message);
        }
    }

    private async Task ProcessVideoAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            string fileId;
            string fileName;

            if (message.Video != null)
            {
                fileId = message.Video.FileId;
                fileName = message.Video.FileName ?? $"video_{fileId}.mp4";
            }
            else if (message.Document != null)
            {
                fileId = message.Document.FileId;
                fileName = message.Document.FileName ?? $"video_{fileId}.mp4";
            }
            else
            {
                return;
            }

            await _botClient.SendMessage(message.Chat.Id,
                $"Processing video: {fileName}...",
                replyParameters: message.MessageId,
                cancellationToken: cancellationToken);

            var file = await _botClient.GetFile(fileId, cancellationToken);
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + "_" + fileName);

            await using (var saveStream = File.OpenWrite(tempPath))
            {
                await _botClient.DownloadFile(file.FilePath!, saveStream, cancellationToken);
            }

            await using (var uploadStream = File.OpenRead(tempPath))
            {
                await _botClient.SendVideo(message.Chat.Id,
                    InputFile.FromStream(uploadStream, fileName),
                    replyParameters: message.MessageId,
                    cancellationToken: cancellationToken);
            }

            File.Delete(tempPath);
        }
        catch (Exception ex)
        {
            await _botClient.SendMessage(message.Chat.Id,
                $"Error processing video: {ex.Message}",
                replyParameters: message.MessageId,
                cancellationToken: cancellationToken);
        }
    }
}
