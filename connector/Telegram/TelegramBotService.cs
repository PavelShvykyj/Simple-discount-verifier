using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdv.Connector.Configuration;
using Sdv.Connector.Contracts;
using Sdv.Connector.Management;

namespace Sdv.Connector.Telegram;

/// <summary>Long-polls the Telegram Bot API for operator commands. The bot token is part of every URL and is never logged.</summary>
internal sealed class TelegramBotService(
    IHttpClientFactory factory,
    IOptions<ConnectorOptions> options,
    IManagementService management,
    TimeProvider time,
    ILogger<TelegramBotService> logger) : BackgroundService
{
    internal const string HttpClientName = "telegram";
    const int MaxResponseBytes = 262144;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.Telegram;
        if (!settings.Enabled)
        {
            logger.LogInformation("Telegram management is disabled by configuration");
            return;
        }
        var allowedUsers = settings.AllowedUserIds.ToHashSet();
        var startup = time.GetUtcNow();
        long offset = 0;
        var discard = true;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(35));
                    var updates = await CallAsync("getUpdates", new { offset = discard ? -1 : offset, timeout = discard ? 0 : 25, allowed_updates = new[] { "message" } }, timeout.Token);
                    foreach (var update in updates.EnumerateArray())
                    {
                        var id = update.GetProperty("update_id").GetInt64();
                        if (!TelegramUpdateFilter.Advance(ref offset, id) || discard) continue;
                        var command = TelegramUpdateFilter.Authorize(update, settings.ChatId, allowedUsers, time.GetUtcNow(), startup);
                        if (command is null) continue;
                        var answer = await management.ExecuteAsync(command, stoppingToken);
                        await ReplyAsync($"📍 {management.Identity}\n{answer}", stoppingToken);
                    }
                    discard = false;
                }
                catch (Exception e) when (!stoppingToken.IsCancellationRequested && e is not OutOfMemoryException)
                {
                    logger.LogWarning("Telegram unavailable: {ExceptionType}", e.GetType().Name);
                    discard = true;
                    await Task.Delay(TimeSpan.FromSeconds(5), time, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    async Task ReplyAsync(string text, CancellationToken stop)
    {
        try
        {
            using var send = CancellationTokenSource.CreateLinkedTokenSource(stop);
            send.CancelAfter(TimeSpan.FromSeconds(5));
            await CallAsync("sendMessage", new { chat_id = options.Value.Telegram.ChatId, text }, send.Token);
        }
        catch (Exception e) when (!stop.IsCancellationRequested)
        {
            logger.LogWarning("Telegram reply failed: {ExceptionType}", e.GetType().Name);
        }
    }

    async Task<JsonElement> CallAsync(string operation, object body, CancellationToken cancel)
    {
        using var client = factory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/bot{options.Value.Telegram.BotToken}/{operation}")
        {
            Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body))
        };
        request.Content.Headers.ContentType = new("application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
        if (!response.IsSuccessStatusCode) throw new InvalidDataException();
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        var bytes = new byte[MaxResponseBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count), cancel);
            if (read == 0) break;
            count += read;
        }
        var result = StrictJson.Parse(bytes[..count], MaxResponseBytes);
        if (!result.GetProperty("ok").GetBoolean()) throw new InvalidDataException();
        return result.GetProperty("result");
    }
}
