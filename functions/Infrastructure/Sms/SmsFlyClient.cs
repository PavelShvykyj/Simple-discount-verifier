using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Sms;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Infrastructure.Sms;

public sealed class SmsFlyClient : ISmsSender
{
    private const string SmsFlyEndpoint = "https://sms-fly.ua/api/v2/api.php";
    private const string SmsFlyApiKeySettingName = "SmsFlyApiKey";
    private const string SmsFlySenderSettingName = "SmsFlySender";
    private const string SendMessageAction = "SENDMESSAGE";
    private const string SmsChannel = "sms";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly SmsOptions _options;
    private readonly ILogger<SmsFlyClient> _logger;

    public SmsFlyClient(
        HttpClient httpClient,
        IOptions<SmsOptions> options,
        ILogger<SmsFlyClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SmsSendResult> SendAsync(
        SmsSendRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new SmsFlySendPayload(
            new SmsFlyAuth(RequireSetting(_options.SmsFlyApiKey, SmsFlyApiKeySettingName)),
            SendMessageAction,
            new SmsFlySendData(
                request.Phone.StorageKey,
                [SmsChannel],
                new SmsFlySmsData(
                    RequireSetting(_options.SmsFlySender, SmsFlySenderSettingName),
                    request.Message)));

        using var response = await _httpClient.PostAsJsonAsync(
            SmsFlyEndpoint,
            payload,
            JsonOptions,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "SMS-Fly request failed with HTTP {StatusCode}.",
                (int)response.StatusCode);

            return new SmsSendResult(false, FailureReason: $"http_{(int)response.StatusCode}");
        }

        if (LooksRejected(body))
        {
            _logger.LogWarning("SMS-Fly response did not accept the SMS message.");
            return new SmsSendResult(false, FailureReason: "provider_rejected");
        }

        return new SmsSendResult(true, ExtractProviderMessageId(body));
    }

    private static bool LooksRejected(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        using var document = TryParseJson(body);

        if (document is null)
        {
            return false;
        }

        var root = document.RootElement;

        if (TryGetBoolean(root, "success", out var success))
        {
            return !success;
        }

        if (TryGetBoolean(root, "accepted", out var accepted))
        {
            return !accepted;
        }

        return HasProblemProperty(root, "error") || HasProblemProperty(root, "errors");
    }

    private static string? ExtractProviderMessageId(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var document = TryParseJson(body);

        if (document is null)
        {
            return null;
        }

        var root = document.RootElement;

        return TryGetString(root, "messageId")
            ?? TryGetString(root, "message_id")
            ?? TryGetString(root, "id");
    }

    private static bool TryGetBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;

        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool HasProblemProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(property.GetString()),
            JsonValueKind.Array => property.GetArrayLength() > 0,
            JsonValueKind.Object => property.EnumerateObject().Any(),
            _ => true
        };
    }

    private static JsonDocument? TryParseJson(string value)
    {
        try
        {
            return JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string RequireSetting(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Application setting '{settingName}' is required.");
        }

        return value;
    }

    private sealed record SmsFlySendPayload(
        SmsFlyAuth Auth,
        string Action,
        SmsFlySendData Data);

    private sealed record SmsFlyAuth(string Key);

    private sealed record SmsFlySendData(
        string Recipient,
        IReadOnlyList<string> Channels,
        SmsFlySmsData Sms);

    private sealed record SmsFlySmsData(
        string Source,
        string Text);
}
