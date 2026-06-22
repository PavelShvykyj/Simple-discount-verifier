using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.CustomerProfiles;
using SimpleDiscountVerifier.Api.Contracts.Admin;
using SimpleDiscountVerifier.Api.Functions.Http;

namespace SimpleDiscountVerifier.Api.Functions.Admin;

public sealed class AdminCustomerProfilesFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AdminCustomerProfileService _profiles;
    private readonly ILogger<AdminCustomerProfilesFunction> _logger;

    public AdminCustomerProfilesFunction(
        AdminCustomerProfileService profiles,
        ILogger<AdminCustomerProfilesFunction> logger)
    {
        _profiles = profiles;
        _logger = logger;
    }

    [Function(nameof(CreateCustomerProfile))]
    public async Task<HttpResponseData> CreateCustomerProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = AdminApiRoutes.BackofficeCustomerProfiles)]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await ReadProfileRequestAsync(request, cancellationToken);

        if (!body.IsSuccess)
        {
            return await HttpResponseWriter.WriteErrorAsync(request, body.Error!, cancellationToken);
        }

        var result = await _profiles.CreateAsync(
            body.Value,
            GetAdminActorId(request),
            cancellationToken);

        return await WriteResultAsync(request, result, HttpStatusCode.Created, cancellationToken);
    }

    [Function(nameof(ListCustomerProfiles))]
    public async Task<HttpResponseData> ListCustomerProfiles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = AdminApiRoutes.BackofficeCustomerProfiles)]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = QueryHelpers.ParseQuery(request.Url.Query);
        int? pageSize = null;

        if (query.TryGetValue("pageSize", out var pageSizeValue)
            && int.TryParse(pageSizeValue.FirstOrDefault(), out var parsedPageSize))
        {
            pageSize = parsedPageSize;
        }

        var result = await _profiles.ListAsync(
            new AdminCustomerProfileListQuery(
                query.TryGetValue("phone", out var phone) ? phone.FirstOrDefault() : null,
                pageSize,
                query.TryGetValue("continuationToken", out var token) ? token.FirstOrDefault() : null),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return await HttpResponseWriter.WriteErrorAsync(request, result.Error!, cancellationToken);
        }

        var body = new CustomerProfileListResponse(
            result.Value!.Items,
            result.Value.ContinuationToken);

        return await HttpResponseWriter.WriteJsonAsync(request, HttpStatusCode.OK, body, cancellationToken);
    }

    [Function(nameof(GetCustomerProfileByPhone))]
    public async Task<HttpResponseData> GetCustomerProfileByPhone(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = AdminApiRoutes.BackofficeCustomerProfileByPhone)]
        HttpRequestData request,
        string phone,
        CancellationToken cancellationToken)
    {
        var result = await _profiles.GetByPhoneAsync(
            Uri.UnescapeDataString(phone),
            cancellationToken);

        return await WriteResultAsync(request, result, HttpStatusCode.OK, cancellationToken);
    }

    [Function(nameof(UpdateCustomerProfile))]
    public async Task<HttpResponseData> UpdateCustomerProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = AdminApiRoutes.BackofficeCustomerProfileByPhone)]
        HttpRequestData request,
        string phone,
        CancellationToken cancellationToken)
    {
        var body = await ReadProfileRequestAsync(request, cancellationToken);

        if (!body.IsSuccess)
        {
            return await HttpResponseWriter.WriteErrorAsync(request, body.Error!, cancellationToken);
        }

        var result = await _profiles.UpdateAsync(
            Uri.UnescapeDataString(phone),
            body.Value,
            GetAdminActorId(request),
            cancellationToken);

        return await WriteResultAsync(request, result, HttpStatusCode.OK, cancellationToken);
    }

    private async Task<ApplicationResult<CustomerProfileRequest?>> ReadProfileRequestAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await JsonSerializer.DeserializeAsync<CustomerProfileRequest>(
                request.Body,
                JsonOptions,
                cancellationToken);

            return ApplicationResult<CustomerProfileRequest?>.Success(body);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Admin customer profile request body is invalid JSON.");
            return ApplicationResult<CustomerProfileRequest?>.Failure(
                new ApplicationError(
                    CustomerProfileErrorCodes.InvalidRequest,
                    "Request body must be valid JSON.",
                    HttpStatusCode.BadRequest));
        }
    }

    private static async Task<HttpResponseData> WriteResultAsync<T>(
        HttpRequestData request,
        ApplicationResult<T> result,
        HttpStatusCode successStatusCode,
        CancellationToken cancellationToken)
    {
        return result.IsSuccess
            ? await HttpResponseWriter.WriteJsonAsync(request, successStatusCode, result.Value, cancellationToken)
            : await HttpResponseWriter.WriteErrorAsync(request, result.Error!, cancellationToken);
    }

    private static string? GetAdminActorId(HttpRequestData request)
    {
        return request.Headers.TryGetValues("x-ms-client-principal-name", out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
