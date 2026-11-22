using System.Text;

namespace Sdv.Worker;

internal static class StateKey
{
    private const int MaximumLength = 128;
    private const string HashPrefix = "sha256-";

    internal static string Compound(string session, string suffix)
    {
        var key = session + suffix;
        return key.Length <= MaximumLength ? key : HashPrefix + Wire.Hash(Encoding.UTF8.GetBytes(key));
    }
}
