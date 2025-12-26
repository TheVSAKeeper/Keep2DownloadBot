using Telegram.Bot;
using Telegram.Bot.Types;
using Serilog;

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

        // We are interested in Video, Document (if it's a video), VideoNote, or MediaGroups
        if (message.Video != null || 
            (message.Document != null && message.Document.MimeType?.StartsWith("video/") == true) ||
            message.VideoNote != null)
        {
            Log.Information("Received video/document from {Username} in chat {ChatId}", message.From?.Username, message.Chat.Id);
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
        Log.Error(exception, "Error in Telegram Bot");
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
        string? fileName = null;
        try
        {
            string fileId;

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
            else if (message.VideoNote != null)
            {
                fileId = message.VideoNote.FileId;
                fileName = $"video_note_{fileId}.mp4";
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
            Log.Error(ex, "Error processing video {FileName} from {ChatId}", fileName ?? "unknown", message.Chat.Id);
            await _botClient.SendMessage(message.Chat.Id,
                $"Error processing video: {ex.Message}",
                replyParameters: message.MessageId,
                cancellationToken: cancellationToken);
        }
    }
}
