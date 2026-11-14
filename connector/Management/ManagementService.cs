using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdv.Connector.Api;
using Sdv.Connector.Configuration;
using Sdv.Connector.Contracts;
using Sdv.Connector.Messaging;

namespace Sdv.Connector.Management;

internal interface IManagementService
{
    string Identity { get; }
    Task<string> ExecuteAsync(string command, CancellationToken cancel);
}

internal static class ManagementCommands
{
    internal static readonly string[] All = ["/status", "/statuslistener", "/statusapi", "/stoplistener", "/startlistener", "/restartlistener", "/checkapi", "/stopapi", "/startapi", "/resetapi"];
}

/// <summary>Operator commands: status, listener and 1C API control. Control commands are exclusive and refused while a lookup is active.</summary>
internal sealed class ManagementService(
    IOptions<ConnectorOptions> options,
    IHostEnvironment environment,
    IOneCApi api,
    ICommandListener listener,
    ListenerHealth health,
    ILogger<ManagementService> logger) : IManagementService
{
    readonly object sync = new();
    bool managing;
    volatile bool desiredListening = true;

    public string Identity
    {
        get
        {
            var settings = options.Value;
            var name = string.IsNullOrWhiteSpace(settings.Environment) ? environment.EnvironmentName : settings.Environment;
            return $"{name} · филиал {settings.BranchId}";
        }
    }

    public async Task<string> ExecuteAsync(string command, CancellationToken cancel)
    {
        if (command is "/status" or "/statuslistener" or "/statusapi") return Status(command);
        lock (sync)
        {
            if (health.ActiveLookup || managing) return "🟡 Занято: идёт обработка заказа или другая команда. Повторите позже.";
            managing = true;
        }
        try
        {
            switch (command)
            {
                case "/stoplistener":
                    if (!desiredListening) return "🟡 Очередь уже остановлена";
                    await SetDesiredListeningAsync(false, cancel);
                    return "✅ Слушание очереди остановлено";
                case "/startlistener":
                    if (desiredListening) return "🟡 Очередь уже слушается";
                    await SetDesiredListeningAsync(true, cancel);
                    return ListeningResult("Слушание очереди запущено");
                case "/restartlistener":
                    await listener.ResetAsync(cancel);
                    await SetDesiredListeningAsync(true, cancel);
                    return ListeningResult("Подключение к очереди перезапущено");
                case "/checkapi":
                    return CheckResult(await api.PingAsync(cancel));
                case "/stopapi":
                    var closed = await api.DisableAsync(cancel);
                    await ReconcileAsync(cancel);
                    return closed == "LogoutUnknown"
                        ? "🟡 Работа с 1С отключена, но закрытие сессии не подтверждено. Очередь на паузе"
                        : "✅ Сессия 1С закрыта, работа с 1С отключена. Очередь на паузе";
                case "/startapi":
                    var started = await api.StartAsync(cancel);
                    await ReconcileAsync(cancel);
                    return StartResult(started, "Сессия 1С открыта");
                case "/resetapi":
                    await api.LogoutAsync(cancel);
                    var login = await api.StartAsync(cancel);
                    await ReconcileAsync(cancel);
                    return StartResult(login, "Сессия 1С пересоздана");
                default:
                    return "🔴 Неизвестная команда";
            }
        }
        catch (OperationCanceledException) { return "🔴 Команда прервана"; }
        catch (Exception e)
        {
            logger.LogError("Management command {Command} failed: {ExceptionType}", command, e.GetType().Name);
            return "🔴 Команда не выполнена из-за внутренней ошибки";
        }
        finally { lock (sync) managing = false; }
    }

    string ListeningResult(string done) => health.Snapshot().State switch
    {
        ListenerState.PausedForApi => $"🟡 {done}, но очередь на паузе: работа с 1С отключена (/startapi)",
        ListenerState.Stopped => "🔴 Очередь не слушается",
        _ => $"✅ {done}"
    };

    static string CheckResult(string result) => result switch
    {
        "Active" => "✅ Сессия 1С активна",
        "NoSession" => "🟡 Сессии 1С нет. Она откроется при следующей команде или по /startapi",
        ReasonCodes.AccessDenied => "🔴 1С отклонила логин или пароль",
        _ => "🔴 1С не отвечает или вернула ошибку"
    };

    static string StartResult(string result, string done) => result switch
    {
        "Started" or "AlreadyEnabled" => $"✅ {done}",
        ReasonCodes.AccessDenied => "🔴 Не удалось открыть сессию: 1С отклонила логин или пароль",
        ReasonCodes.ApiUnavailable or ApiErrors.SessionUnavailable => "🔴 Не удалось открыть сессию: 1С недоступна или вернула неожиданный ответ",
        _ => $"🔴 Не удалось открыть сессию 1С ({result})"
    };

    /// <summary>Applies the operator's wish and restores the previous one if the listener could not follow it.</summary>
    async Task SetDesiredListeningAsync(bool value, CancellationToken cancel)
    {
        var previous = desiredListening;
        desiredListening = value;
        try { await ReconcileAsync(cancel); }
        catch { desiredListening = previous; throw; }
    }

    /// <summary>Brings the listener in line with the operator's wishes: it listens only while wanted and the 1C API is enabled.</summary>
    internal async Task ReconcileAsync(CancellationToken cancel)
    {
        if (desiredListening && api.Enabled)
        {
            await listener.StartAsync(cancel);
            return;
        }
        await listener.StopAsync(cancel);
        health.SetState(desiredListening ? ListenerState.PausedForApi : ListenerState.Stopped);
    }

    internal async Task ShutdownAsync(CancellationToken cancel)
    {
        await listener.StopAsync(cancel);
        var closed = await api.LogoutAsync(cancel);
        logger.LogInformation("1C session on shutdown: {Result}", closed);
    }

    internal string Status(string command = "/status")
    {
        var listenerState = health.Snapshot();
        var apiState = api.Snapshot();
        var lines = new List<string>();
        if (command != "/statusapi")
        {
            lines.Add(listenerState.State switch
            {
                ListenerState.Listening => "🟢 Очередь: слушаю",
                ListenerState.Starting => "🟡 Очередь: запускается",
                ListenerState.Reconnecting => "🟡 Очередь: переподключение",
                ListenerState.PausedForApi => "🟡 Очередь: на паузе, 1С отключена",
                ListenerState.Stopped => "🔴 Очередь: остановлена",
                _ => "🔴 Очередь: ошибка подключения"
            });
            if (listenerState.State is ListenerState.Reconnecting or ListenerState.Faulted && listenerState.ReceiveErrorCode != "None")
                lines.Add($"    причина: {listenerState.ReceiveErrorCode}");
            if (listenerState.SendErrorCode != "None") lines.Add($"🟡 Последний результат не отправлен ({listenerState.SendErrorCode})");
        }
        if (command != "/statuslistener")
        {
            lines.Add(apiState.State switch
            {
                ApiState.Active => "🟢 Сессия 1С: активна",
                ApiState.NoSession => "🟡 Сессия 1С: нет, откроется при следующей команде",
                ApiState.Unknown => "🟡 Сессия 1С: состояние неизвестно",
                ApiState.Disabled => "🔴 Сессия 1С: отключена",
                _ => "🔴 Сессия 1С: ошибка"
            });
            if (apiState.State is ApiState.Faulted or ApiState.Unknown && apiState.ErrorCode != ApiErrors.None)
                lines.Add($"    причина: {apiState.ErrorCode}");
        }
        if (listenerState.ActiveLookup) lines.Add("⏳ Сейчас обрабатывается заказ");
        return string.Join('\n', lines);
    }
}
