using System.Text.Json;
using Sdv.Connector.Contracts;
using Sdv.Connector.Management;

namespace Sdv.Connector.Telegram;

/// <summary>Accepts only fresh, exact operator commands from the configured private chat and allowed users.</summary>
internal static class TelegramUpdateFilter
{
    internal static bool Advance(ref long offset, long updateId)
    {
        if (updateId < offset || updateId == long.MaxValue) return false;
        offset = updateId + 1;
        return true;
    }

    internal static string? Authorize(JsonElement update, long chatId, HashSet<long> allowedUsers, DateTimeOffset now, DateTimeOffset startup)
    {
        try
        {
            if (!update.TryGetProperty("message", out var message) || update.TryGetProperty("edited_message", out _) || update.TryGetProperty("channel_post", out _)) return null;
            if (message.TryGetProperty("forward_origin", out _) || message.TryGetProperty("forward_from", out _) || message.TryGetProperty("forward_date", out _) || message.TryGetProperty("sender_chat", out _)) return null;
            var chat = message.GetProperty("chat");
            var sender = message.GetProperty("from");
            if (StrictJson.Text(chat.GetProperty("type")) != "private" || chat.GetProperty("id").GetInt64() != chatId
                || sender.GetProperty("is_bot").GetBoolean() || !allowedUsers.Contains(sender.GetProperty("id").GetInt64())) return null;
            var at = DateTimeOffset.FromUnixTimeSeconds(message.GetProperty("date").GetInt64());
            if (at <= startup || at < now.AddSeconds(-60) || at > now.AddSeconds(5)) return null;
            var text = StrictJson.Text(message.GetProperty("text"));
            return ManagementCommands.All.Contains(text, StringComparer.Ordinal) ? text : null;
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or KeyNotFoundException or FormatException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
