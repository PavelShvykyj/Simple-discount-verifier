using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Contracts.Common;

namespace SimpleDiscountVerifier.Api.Functions.Http;

public static class HttpResponseWriter
{
    public static async Task<HttpResponseData> WriteJsonAsync<T>(
        HttpRequestData request,
        HttpStatusCode statusCode,
        T body,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(body, cancellationToken);
        return response;
    }

    public static Task<HttpResponseData> WriteErrorAsync(
        HttpRequestData request,
        ApplicationError error,
        CancellationToken cancellationToken)
    {
        var body = new ApiErrorResponse(
            new ApiError(error.Code, error.Message, error.CorrelationId));

        return WriteJsonAsync(request, error.StatusCode, body, cancellationToken);
    }
}
