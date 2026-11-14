using Microsoft.Extensions.Options;

namespace Sdv.Connector.Configuration;

internal sealed class ConnectorOptionsValidator : IValidateOptions<ConnectorOptions>
{
    public ValidateOptionsResult Validate(string? name, ConnectorOptions options)
    {
        var errors = new List<string>();
        var validBranch = Patterns.BranchId().IsMatch(options.BranchId);
        if (!validBranch) errors.Add("Connector:BranchId must be a string of 1 to 128 digits.");
        if (string.IsNullOrWhiteSpace(options.ServiceName)) errors.Add("Connector:ServiceName is required.");

        ValidateApi(options.Api, errors);

        var listenHost = CheckConnection(options.ServiceBus.CommandListenConnectionString, "CommandListenConnectionString",
            validBranch ? "commands" + options.BranchId : null, validBranch ? $"connector-{options.BranchId}-listen" : null);
        var sendHost = CheckConnection(options.ServiceBus.ResultSendConnectionString, "ResultSendConnectionString",
            "results", validBranch ? $"connector-{options.BranchId}-send" : null);
        if (listenHost is not null && sendHost is not null && listenHost != sendHost)
            errors.Add("Connector:ServiceBus:CommandListenConnectionString and Connector:ServiceBus:ResultSendConnectionString must use the same namespace.");

        ValidateTelegram(options.Telegram, errors);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);

        string? CheckConnection(string value, string setting, string? entity, string? policy)
        {
            var (host, error) = ServiceBusConnectionString.Validate(value, entity, policy);
            if (error is not null) errors.Add($"Connector:ServiceBus:{setting} {error}");
            return host;
        }
    }

    static void ValidateApi(OneCApiOptions api, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(api.BaseUri)) errors.Add("Connector:Api:BaseUri is required.");
        else if (!Uri.TryCreate(api.BaseUri, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !uri.IsLoopback || uri.Query.Length != 0 ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
                        errors.Add("Connector:Api:BaseUri must be a loopback HTTP(S) URL (a 1C publication path is allowed) without credentials, query or fragment.");

        if (string.IsNullOrEmpty(api.User)) errors.Add("Connector:Api:User is required.");
        else if (api.User.Contains(':')) errors.Add("Connector:Api:User must not contain ':'.");
        if (string.IsNullOrEmpty(api.Password)) errors.Add("Connector:Api:Password is required.");

        CheckPath(api.PingPath, nameof(api.PingPath));
        CheckPath(api.LookupPath, nameof(api.LookupPath));

        void CheckPath(string value, string setting)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add($"Connector:Api:{setting} is required.");
            else if (!value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal) || value.AsSpan().ContainsAny('?', '#') || value.Any(char.IsWhiteSpace))
                errors.Add($"Connector:Api:{setting} must be a path starting with '/', relative to BaseUri, without a query or fragment.");
        }
    }

    static void ValidateTelegram(TelegramOptions telegram, List<string> errors)
    {
        if (!telegram.Enabled) return;
        if (string.IsNullOrWhiteSpace(telegram.BotToken)) errors.Add("Connector:Telegram:BotToken is required when Telegram is enabled.");
        else if (!Patterns.TelegramBotToken().IsMatch(telegram.BotToken)) errors.Add("Connector:Telegram:BotToken has an invalid format.");
        if (telegram.ChatId <= 0) errors.Add("Connector:Telegram:ChatId must be a positive integer.");
        var users = telegram.AllowedUserIds;
        if (users.Length == 0 || users.Any(id => id <= 0) || users.Distinct().Count() != users.Length)
            errors.Add("Connector:Telegram:AllowedUserIds must contain distinct positive integers.");
    }
}
