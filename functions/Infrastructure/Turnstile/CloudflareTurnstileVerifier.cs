using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Redemptions;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Infrastructure.Turnstile;

public sealed class CloudflareTurnstileVerifier : ITurnstileVerifier
{
    private const string VerifyEndpoint = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private const int MaxTokenLength = 2048;
    private static readonly TimeSpan VerifyTimeout = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TurnstileOptions _options;
    private readonly ILogger<CloudflareTurnstileVerifier> _logger;

    public CloudflareTurnstileVerifier(
        HttpClient httpClient,
        IOptions<TurnstileOptions> options,
        ILogger<CloudflareTurnstileVerifier> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey)
            || string.IsNullOrWhiteSpace(_options.ExpectedHostname))
        {
            _logger.LogWarning("Turnstile is enabled but its secret key or expected hostname is not configured.");
            return false;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(VerifyTimeout);
            using var response = await _httpClient.PostAsJsonAsync(
                VerifyEndpoint,
                new TurnstileVerifyPayload(_options.SecretKey, token),
                JsonOptions,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verify request failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<TurnstileVerifyResponse>(JsonOptions, timeout.Token);
            return body is
            {
                Success: true,
                Action: TurnstileOptions.StartRedemptionAction
            } && string.Equals(body.Hostname, _options.ExpectedHostname, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Turnstile verify request timed out.");
            return false;
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Turnstile verify transport request failed.");
            return false;
        }
    }

    private sealed record TurnstileVerifyPayload(string Secret, string Response);

    private sealed record TurnstileVerifyResponse(bool Success, string? Action, string? Hostname);
}
