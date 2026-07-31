using System.Text.Json;

namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

internal static class StorageJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
