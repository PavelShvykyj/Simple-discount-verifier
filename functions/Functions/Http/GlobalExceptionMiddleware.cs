using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using SimpleDiscountVerifier.Api.Contracts.Common;

namespace SimpleDiscountVerifier.Api.Functions.Http;

public sealed class GlobalExceptionMiddleware : IFunctionsWorkerMiddleware
{
    private const string InternalErrorCode = "internal_error";
    private const string InternalErrorMessage = "Unexpected server error.";

    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(ILogger<GlobalExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception in function {FunctionName}. InvocationId: {InvocationId}.",
                context.FunctionDefinition.Name,
                context.InvocationId);

            var request = await context.GetHttpRequestDataAsync();

            if (request is null)
            {
                throw;
            }

            try
            {
                var response = request.CreateResponse();
                await response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        new ApiError(
                            InternalErrorCode,
                            InternalErrorMessage,
                            context.InvocationId)),
                    HttpStatusCode.InternalServerError);

                context.GetInvocationResult().Value = response;
            }
            catch (Exception responseException)
            {
                _logger.LogCritical(
                    responseException,
                    "Failed to write global error response for function {FunctionName}. InvocationId: {InvocationId}.",
                    context.FunctionDefinition.Name,
                    context.InvocationId);

                throw;
            }
        }
    }
}
