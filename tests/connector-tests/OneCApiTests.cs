using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sdv.Connector.Api;
using Sdv.Connector.Contracts;

namespace Sdv.Connector;

public class OneCResponseTests
{
    const string FoundOrder = "\"order\":{\"orderNumber\":\"00042\",\"orderStateCode\":\"Open\",\"receiptState\":\"NotFiscalized\",\"amount\":{\"value\":\"10.00\",\"currency\":\"UAH\"}}";

    static LookupResult Normalize(int status, string body) => OneCResponse.Normalize(status, JsonDocument.Parse(body).RootElement, "00042");

    [Fact]
    public void FoundOrderIsAccepted()
    {
        var result = Normalize(200, "{\"contractVersion\":1,\"outcome\":\"Found\",\"reasonCode\":\"Success\"," + FoundOrder + "}");
        Assert.Equal(Outcomes.Found, result.Outcome);
        Assert.NotNull(result.Order);
    }

    [Fact]
    public void MissingOrderIsAccepted()
    {
        var result = Normalize(200, "{\"contractVersion\":1,\"outcome\":\"NotFound\",\"reasonCode\":\"OrderMissing\"}");
        Assert.Equal(ReasonCodes.OrderMissing, result.Reason);
    }

    [Theory]
    [InlineData(400, "InvalidArguments", "InvalidArguments", "ApiRejected")]
    [InlineData(400, "UnsupportedContractVersion", "InvalidArguments", "ApiProtocolFailure")]
    [InlineData(401, "AccessDenied", "AccessDenied", null)]
    [InlineData(408, "Expired", "Expired", null)]
    [InlineData(404, "InvalidArguments", "InvalidArguments", "ApiRejected")]
    [InlineData(503, "ApiUnavailable", "ApiUnavailable", null)]
    [InlineData(200, "ApiOutcomeUnknown", "ApiOutcomeUnknown", null)]
    public void FailureStatusesMapToReasons(int status, string apiReason, string reason, string? diagnostic)
    {
        var result = Normalize(status, $"{{\"contractVersion\":1,\"outcome\":\"Failed\",\"reasonCode\":\"{apiReason}\"}}");
        Assert.Equal(Outcomes.Failed, result.Outcome);
        Assert.Equal(reason, result.Reason);
        Assert.Equal(diagnostic, result.Diagnostic);
    }

    [Theory]
    [InlineData(200, "{\"contractVersion\":1,\"outcome\":\"Failed\",\"reasonCode\":\"AccessDenied\"}")]
    [InlineData(500, "{\"contractVersion\":1,\"outcome\":\"NotFound\",\"reasonCode\":\"OrderMissing\"}")]
    [InlineData(200, "{\"contractVersion\":2,\"outcome\":\"NotFound\",\"reasonCode\":\"OrderMissing\"}")]
    [InlineData(200, "{\"contractVersion\":1,\"outcome\":\"NotFound\",\"reasonCode\":\"OrderMissing\",\"extra\":1}")]
    public void ContractViolationsAreRejected(int status, string body)
    {
        Assert.Throws<InvalidDataException>(() => Normalize(status, body));
    }

    [Fact]
    public void FoundOrderForAnotherNumberIsRejected()
    {
        var body = "{\"contractVersion\":1,\"outcome\":\"Found\",\"reasonCode\":\"Success\"," + FoundOrder.Replace("00042", "00043") + "}";
        Assert.Throws<InvalidDataException>(() => Normalize(200, body));
    }
}

public class OneCApiServiceTests
{
    const string NotFound = "{\"contractVersion\":1,\"outcome\":\"NotFound\",\"reasonCode\":\"OrderMissing\"}";

    sealed class FakeOneC : HttpMessageHandler, IHttpClientFactory
    {
        public int Logins, Pings, Finishes, Lookups;
        public bool PingOk = true, LoginReturnsCookie = true;
        public int LoginStatus = 200, LookupStatus = 200;
        public bool LookupThrows;
        public string? LookupContentType = "application/json";
        public string? LastAuthorization, LastLookupCookie;

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("http://127.0.0.1:18101/") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastAuthorization = request.Headers.Authorization?.ToString();
            var ib = request.Headers.TryGetValues("IBSession", out var values) ? values.Single() : null;
            var cookie = request.Headers.TryGetValues("Cookie", out var cookies) ? cookies.Single() : null;
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/hs/ping")
            {
                if (ib == "start")
                {
                    Logins++;
                    var login = new HttpResponseMessage((HttpStatusCode)LoginStatus);
                    if (LoginReturnsCookie) login.Headers.TryAddWithoutValidation("Set-Cookie", $"ibsession=session-{Logins}; path=/; HttpOnly");
                    return Task.FromResult(login);
                }
                if (ib == "finish") { Finishes++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); }
                Pings++;
                return Task.FromResult(new HttpResponseMessage(PingOk ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
            }
            Assert.Equal("/hs/orders/lookup", path);
            Lookups++;
            LastLookupCookie = cookie;
            if (LookupThrows) throw new HttpRequestException("connection reset");
            var content = new StringContent(NotFound, Encoding.UTF8);
            content.Headers.ContentType = LookupContentType is null ? null : new(LookupContentType);
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)LookupStatus) { Content = content });
        }
    }

    static OneCApiService Create(FakeOneC fake, TimeProvider? time = null) =>
        new(fake, TestData.Wrap(TestData.Settings()), time ?? new TestTime(TestData.Now), NullLogger<OneCApiService>.Instance);

    [Fact]
    public async Task LookupStartsSessionAndSendsBasicAuthorizationAndCookie()
    {
        var fake = new FakeOneC();
        var result = await Create(fake).LookupAsync(TestData.Command(), default);
        Assert.Equal(Outcomes.NotFound, result.Outcome);
        Assert.Equal(1, fake.Logins);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("svc:secret")), fake.LastAuthorization);
        Assert.Equal("ibsession=session-1", fake.LastLookupCookie);
    }

    [Fact]
    public async Task MissingContentTypeIsAcceptedButAForeignOneIsNot()
    {
        Assert.Equal(Outcomes.NotFound, (await Create(new FakeOneC { LookupContentType = null }).LookupAsync(TestData.Command(), default)).Outcome);
        var result = await Create(new FakeOneC { LookupContentType = "text/html" }).LookupAsync(TestData.Command(), default);
        Assert.Equal(ReasonCodes.ApiOutcomeUnknown, result.Reason);
    }

    [Fact]
    public async Task SecondLookupReusesTheSessionAfterPing()
    {
        var fake = new FakeOneC();
        var api = Create(fake);
        await api.LookupAsync(TestData.Command(), default);
        await api.LookupAsync(TestData.Command(), default);
        Assert.Equal((1, 1, 2), (fake.Logins, fake.Pings, fake.Lookups));
    }

    [Fact]
    public async Task SessionIsRecreatedWhenPingFails()
    {
        var fake = new FakeOneC();
        var api = Create(fake);
        await api.LookupAsync(TestData.Command(), default);
        fake.PingOk = false;
        var result = await api.LookupAsync(TestData.Command(), default);
        Assert.Equal(Outcomes.NotFound, result.Outcome);
        Assert.Equal((2, 1), (fake.Logins, fake.Finishes));
        Assert.Equal("ibsession=session-2", fake.LastLookupCookie);
    }

    [Fact]
    public async Task ConnectionFailureDuringLookupIsUnknownAndNeverRetried()
    {
        var fake = new FakeOneC { LookupThrows = true };
        var api = Create(fake);
        var result = await api.LookupAsync(TestData.Command(), default);
        Assert.Equal(ReasonCodes.ApiOutcomeUnknown, result.Reason);
        Assert.Equal(1, fake.Lookups);
        Assert.Equal(ApiState.Unknown, api.Snapshot().State);
    }

    [Fact]
    public async Task ForbiddenLookupIsAccessDeniedAndDropsTheSession()
    {
        var fake = new FakeOneC { LookupStatus = 403 };
        var api = Create(fake);
        var result = await api.LookupAsync(TestData.Command(), default);
        Assert.Equal(ReasonCodes.AccessDenied, result.Reason);
        Assert.Equal(ApiState.NoSession, api.Snapshot().State);
        Assert.Equal("NoSession", await api.LogoutAsync(default));
    }

    [Fact]
    public async Task LoginFailuresAreReportedWithoutALookup()
    {
        var unavailable = new FakeOneC { LoginStatus = 503 };
        Assert.Equal(ReasonCodes.ApiUnavailable, (await Create(unavailable).LookupAsync(TestData.Command(), default)).Reason);
        Assert.Equal(0, unavailable.Lookups);
        var denied = new FakeOneC { LoginStatus = 401 };
        Assert.Equal(ReasonCodes.AccessDenied, (await Create(denied).LookupAsync(TestData.Command(), default)).Reason);
        var noCookie = new FakeOneC { LoginReturnsCookie = false };
        var result = await Create(noCookie).LookupAsync(TestData.Command(), default);
        Assert.Equal((ReasonCodes.ApiUnavailable, ApiErrors.ApiProtocolFailure), (result.Reason, result.Diagnostic));
    }

    [Fact]
    public async Task DisabledApiDoesNotCallOneC()
    {
        var fake = new FakeOneC();
        var api = Create(fake);
        await api.DisableAsync(default);
        var result = await api.LookupAsync(TestData.Command(), default);
        Assert.Equal(ReasonCodes.ApiUnavailable, result.Reason);
        Assert.Equal(0, fake.Logins + fake.Lookups);
        Assert.Equal(ApiState.Disabled, api.Snapshot().State);
    }

    [Fact]
    public async Task ManagementCommandsFollowTheSessionLifecycle()
    {
        var fake = new FakeOneC();
        var api = Create(fake);
        Assert.Equal("NoSession", await api.PingAsync(default));
        Assert.Equal("Started", await api.StartAsync(default));
        Assert.Equal("AlreadyEnabled", await api.StartAsync(default));
        Assert.Equal("Active", await api.PingAsync(default));
        Assert.Equal("Closed", await api.LogoutAsync(default));
        Assert.Equal(1, fake.Finishes);
    }

    [Fact]
    public async Task CommandThatCannotMeetItsDeadlineExpiresWithoutCallingOneC()
    {
        var fake = new FakeOneC();
        var command = TestData.Command();
        var late = new TestTime(command.Expiry.AddSeconds(-10));
        var result = await Create(fake, late).LookupAsync(command, default);
        Assert.Equal(ReasonCodes.Expired, result.Reason);
        Assert.Equal(0, fake.Logins);
    }

    [Theory]
    [InlineData("ibsession=abc123; path=/", "abc123")]
    [InlineData("IBSession=abc123", "abc123")]
    [InlineData("other=1; path=/", null)]
    [InlineData("ibsession=; path=/", null)]
    [InlineData("ibsession=a b", null)]
    public void SessionIdIsReadFromSetCookie(string header, string? expected)
    {
        Assert.Equal(expected, OneCApiService.ExtractSessionId([header]));
    }

    [Fact]
    public void BudgetIsLimitedByStageAndByCommandExpiry()
    {
        var now = TestData.Now;
        Assert.Equal(TimeSpan.Zero, OneCApiService.Budget(now.AddSeconds(15), now, TimeSpan.Zero));
        Assert.True(OneCApiService.Budget(now.AddSeconds(15).AddTicks(1), now, TimeSpan.Zero) > TimeSpan.Zero);
        Assert.Equal(1, OneCApiService.Budget(now.AddSeconds(120), now, TimeSpan.FromSeconds(29)).TotalSeconds);
    }
}
