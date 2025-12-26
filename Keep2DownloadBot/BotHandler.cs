using Serilog;
using TL;
using WTelegram;

namespace Keep2DownloadBot;

public class BotHandler : IDisposable
{
    private readonly Client _client;

    // Храним сообщения и InputPeer (куда отвечать) для группы
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
            if (update is UpdateNewMessage unm && unm.message is Message message)
            {
                // 1. Получаем инфо о чате/пользователе
                var peerInfo = updates.UserOrChat(message.peer_id);
                if (peerInfo == null)
                {
                    continue;
                }

                // 2. Преобразуем в InputPeer для отправки ответов
                var targetPeer = peerInfo.ToInputPeer();

                await HandleMessageAsync(message, targetPeer);
            }
        }
    }

    private async Task HandleMessageAsync(Message message, InputPeer targetPeer)
    {
        // Проверка: является ли сообщение видео (через Document)
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

    // В современной схеме TL видео — это всегда Document с mime-type video/*
    private bool IsVideoMessage(Message message)
    {
        if (message.media is MessageMediaDocument mmd && mmd.document is Document doc)
        {
            // Проверяем по MIME-типу
            if (doc.mime_type?.StartsWith("video/") == true)
            {
                return true;
            }

            // Дополнительная проверка: иногда MIME может быть application/octet-stream,
            // но есть атрибут Video
            foreach (var attr in doc.attributes)
            {
                if (attr is DocumentAttributeVideo)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task HandleMediaGroupAsync(Message message, InputPeer targetPeer)
    {
        var groupId = message.grouped_id;

        lock (_mediaGroups)
        {
            if (!_mediaGroups.ContainsKey(groupId))
            {
                _mediaGroups[groupId] = (new(), targetPeer);

                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000); // Ждем сбора группы

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

            _mediaGroups[groupId].Messages.Add(message);
        }
    }

    private async Task ProcessVideoAsync(Message message, InputPeer targetPeer)
    {
        var fileName = "video.mp4";
        Document? documentToDownload = null;

        // Извлекаем документ (единственный способ передачи видео)
        if (message.media is MessageMediaDocument mmd && mmd.document is Document doc)
        {
            documentToDownload = doc;
            fileName = doc.Filename; // WTelegramClient helper property

            // Если имя пустое, генерируем его
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

            // 1. Скачивание
            await using (var saveStream = File.OpenWrite(tempPath))
            {
                await _client.DownloadFileAsync(documentToDownload, saveStream);
            }

            // 2. Загрузка
            var inputFile = await _client.UploadFileAsync(tempPath);

            // 3. Отправка
            // Для отправки видео используем SendMediaAsync с InputMediaUploadedDocument
            // Важно указать mime-type и атрибуты, чтобы Telegram распознал это как видео
            var inputMedia = new InputMediaUploadedDocument
            {
                file = inputFile,
                mime_type = "video/mp4",
                attributes = new[] { new DocumentAttributeVideo { duration = 0, w = 0, h = 0 } },
            };

            await _client.SendMediaAsync(targetPeer, null, inputFile, reply_to_msg_id: message.id);

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
