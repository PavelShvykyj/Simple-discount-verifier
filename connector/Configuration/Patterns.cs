using System.Text.RegularExpressions;

namespace Sdv.Connector.Configuration;

internal static partial class Patterns
{
    [GeneratedRegex(@"\A[0-9]{1,128}\z", RegexOptions.CultureInvariant)]
    internal static partial Regex BranchId();

    [GeneratedRegex(@"\A[0-9]{1,64}\z", RegexOptions.CultureInvariant)]
    internal static partial Regex OrderNumber();

    [GeneratedRegex(@"\A[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\z", RegexOptions.CultureInvariant)]
    internal static partial Regex Route();

    [GeneratedRegex(@"\A[A-Za-z0-9_-]+\z", RegexOptions.CultureInvariant)]
    internal static partial Regex Base64Url();

    [GeneratedRegex(@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?Z\z", RegexOptions.CultureInvariant)]
    internal static partial Regex Timestamp();

    [GeneratedRegex(@"\A[0-9]{1,12}\.[0-9]{2}\z", RegexOptions.CultureInvariant)]
    internal static partial Regex MoneyValue();

    [GeneratedRegex(@"\A[0-9]+:[A-Za-z0-9_-]{20,}\z", RegexOptions.CultureInvariant)]
    internal static partial Regex TelegramBotToken();

    [GeneratedRegex(@"\A[^\s;,""\\]{1,256}\z", RegexOptions.CultureInvariant)]
    internal static partial Regex SessionId();
}
