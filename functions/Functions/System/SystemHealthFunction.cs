using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SimpleDiscountVerifier.Api.Contracts.System;
using SimpleDiscountVerifier.Api.Functions.Http;

namespace SimpleDiscountVerifier.Api.Functions.System;

public sealed class SystemHealthFunction
{
    [Function(nameof(GetHealth))]
    public async Task<HttpResponseData> GetHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "system/health")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return await HttpResponseWriter.WriteJsonAsync(
            request,
            HttpStatusCode.OK,
            new HealthResponse("ok", "simple-discount-verifier-api"),
            cancellationToken);
    }
}
