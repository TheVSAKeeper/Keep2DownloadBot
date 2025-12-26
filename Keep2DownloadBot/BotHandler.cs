using Serilog;
using TL;
using WTelegram;

namespace Keep2DownloadBot;

public class BotHandler : IDisposable
{
    private readonly Client _client;
    private readonly Dictionary<long, (List<Message> Messages, InputPeer Peer)> _mediaGroups = new();

    public BotHandler(Client client)
    {
        _client = client;
        _client.OnUpdates += HandleUpdatesInternal;
    }

    public void Dispose()
    {
        _client.OnUpdates -= HandleUpdatesInternal;
    }

    private async Task HandleUpdatesInternal(IObject arg)
    {
        if (arg is not UpdatesBase updates)
        {
            return;
        }

        foreach (var update in updates.UpdateList)
        {
            if (update is not UpdateNewMessage { message: Message message })
            {
                continue;
            }

            var peerInfo = updates.UserOrChat(message.peer_id);
            if (peerInfo == null)
            {
                continue;
            }

            var targetPeer = peerInfo.ToInputPeer();

            await HandleMessageAsync(message, targetPeer);
        }
    }

    private async Task HandleMessageAsync(Message message, InputPeer targetPeer)
    {
        if (IsVideoMessage(message))
        {
            Log.Information("Received video from peer {PeerId}", message.peer_id.ID);

            if (message.grouped_id != 0)
            {
                await HandleMediaGroupAsync(message, targetPeer);
            }
            else
            {
                await ProcessVideoAsync(message, targetPeer);
            }
        }
    }

    private bool IsVideoMessage(Message message)
    {
        if (message.media is MessageMediaDocument { document: Document doc })
        {
            return doc.mime_type?.StartsWith("video/") == true
                   || doc.attributes.OfType<DocumentAttributeVideo>().Any();
        }

        return false;
    }

    private Task HandleMediaGroupAsync(Message message, InputPeer targetPeer)
    {
        var groupId = message.grouped_id;

        lock (_mediaGroups)
        {
            if (!_mediaGroups.TryGetValue(groupId, out var value))
            {
                value = ([], targetPeer);
                _mediaGroups[groupId] = value;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);

                    List<Message> messages;
                    InputPeer peer;

                    lock (_mediaGroups)
                    {
                        if (!_mediaGroups.TryGetValue(groupId, out var data))
                        {
                            return;
                        }

                        messages = data.Messages;
                        peer = data.Peer;
                        _mediaGroups.Remove(groupId);
                    }

                    foreach (var msg in messages)
                    {
                        await ProcessVideoAsync(msg, peer);
                    }
                });
            }

            value.Messages.Add(message);
        }

        return Task.CompletedTask;
    }

    private async Task ProcessVideoAsync(Message message, InputPeer targetPeer)
    {
        var fileName = "video.mp4";
        Document? documentToDownload = null;

        if (message.media is MessageMediaDocument { document: Document doc })
        {
            documentToDownload = doc;
            fileName = doc.Filename;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"video_{doc.id}.mp4";
            }
        }

        if (documentToDownload == null)
        {
            return;
        }

        try
        {
            await _client.SendMessageAsync(targetPeer, $"Processing: {fileName}...", reply_to_msg_id: message.id);

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fileName}");

            await using (var saveStream = File.OpenWrite(tempPath))
            {
                await _client.DownloadFileAsync(documentToDownload, saveStream);
            }

            var inputFile = await _client.UploadFileAsync(tempPath);

            await _client.SendMediaAsync(targetPeer, null, inputFile,mimeType:"video" , reply_to_msg_id: message.id);

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing video {FileName}", fileName);
            await _client.SendMessageAsync(targetPeer, $"Error: {ex.Message}", reply_to_msg_id: message.id);
        }
    }
}
