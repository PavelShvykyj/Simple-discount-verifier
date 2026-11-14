using System.Text;
using System.Text.Json.Nodes;
using Sdv.Connector.Contracts;

namespace Sdv.Connector;

public class CommandParserTests
{
    [Fact]
    public void ValidLookupCommandIsParsed()
    {
        var command = TestData.Command();
        Assert.Equal("00042", command.OrderNumber);
        Assert.Null(command.Rejection);
        Assert.Equal(TestData.Now.AddSeconds(120), command.Expiry);
    }

    [Fact]
    public void ForeignBranchIsRejectedAsAccessDenied()
    {
        var command = CommandParser.Parse(TestData.Bytes(TestData.Envelope("102")), "application/json", "101", TestData.Now);
        Assert.Equal(ReasonCodes.AccessDenied, command.Rejection);
    }

    [Theory]
    [InlineData("FiscalizeReceipt")]
    [InlineData("Invented")]
    [InlineData("")]
    public void UnsupportedCommandTypeIsInvalidArguments(string type)
    {
        var envelope = TestData.Envelope();
        envelope["commandType"] = type;
        var command = CommandParser.Parse(TestData.Bytes(envelope), "application/json", "101", TestData.Now);
        Assert.Equal(ReasonCodes.InvalidArguments, command.Rejection);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"orderNumber\":42}")]
    [InlineData("{\"orderNumber\":\" 00042\"}")]
    [InlineData("{\"orderNumber\":null}")]
    [InlineData("{\"orderNumber\":\"00042\",\"orderNumber\":\"42\"}")]
    [InlineData("{\"orderNumber\":\"00042\",\"extra\":true}")]
    [InlineData("{\"orderNumber\":\"00042\",\"amountHint\":{\"value\":\"1.234\",\"currency\":\"UAH\"}}")]
    [InlineData("{\"orderNumber\":\"\\uD800\"}")]
    public void InvalidArgumentsAreRejectedWithoutCallingTheApi(string data)
    {
        var command = CommandParser.Parse(TestData.Bytes(TestData.Envelope(data: data)), "application/json", "101", TestData.Now);
        Assert.Equal(ReasonCodes.InvalidArguments, command.Rejection);
    }

    [Fact]
    public void AmountHintIsKept()
    {
        var data = "{\"orderNumber\":\"00042\",\"amountHint\":{\"value\":\"12.50\",\"currency\":\"UAH\"}}";
        var command = CommandParser.Parse(TestData.Bytes(TestData.Envelope(data: data)), "application/json", "101", TestData.Now);
        Assert.NotNull(command.AmountHint);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("kind")]
    [InlineData("eventId")]
    [InlineData("requestId")]
    [InlineData("operationId")]
    [InlineData("commandId")]
    [InlineData("toolCallId")]
    [InlineData("commandType")]
    [InlineData("branchId")]
    [InlineData("issuedAt")]
    [InlineData("expiresAt")]
    [InlineData("route")]
    [InlineData("dataB64")]
    public void MissingOrNullEnvelopeFieldIsRejected(string field)
    {
        var missing = TestData.Envelope();
        missing.Remove(field);
        Assert.Throws<InvalidDataException>(() => CommandParser.Parse(TestData.Bytes(missing), "application/json", "101", TestData.Now));
        var nulled = TestData.Envelope();
        nulled[field] = null;
        Assert.Throws<InvalidDataException>(() => CommandParser.Parse(TestData.Bytes(nulled), "application/json", "101", TestData.Now));
    }

    [Fact]
    public void UnknownFieldIsRejected()
    {
        var envelope = TestData.Envelope();
        envelope["data"] = new JsonObject();
        Assert.Throws<InvalidDataException>(() => CommandParser.Parse(TestData.Bytes(envelope), "application/json", "101", TestData.Now));
    }

    [Theory]
    [InlineData("\"schemaVersion\":1.0")]
    [InlineData("\"schemaVersion\":1,\"schemaVersion\":1")]
    public void NonCanonicalSchemaVersionAndDuplicatesAreRejected(string replacement)
    {
        var json = TestData.Envelope().ToJsonString().Replace("\"schemaVersion\":1", replacement);
        Assert.Throws<InvalidDataException>(() => CommandParser.Parse(Encoding.UTF8.GetBytes(json), "application/json", "101", TestData.Now));
    }

    [Fact]
    public void WrongContentTypeBomAndOversizeAreRejected()
    {
        var bytes = TestData.Bytes(TestData.Envelope());
        Assert.Throws<InvalidDataException>(() => CommandParser.Parse(bytes, "application/json; charset=utf-8", "101", TestData.Now));
        Assert.Throws<InvalidDataException>(() => CommandParser.Parse([0xEF, 0xBB, 0xBF, .. bytes], "application/json", "101", TestData.Now));
        Assert.Throws<InvalidDataException>(() => CommandParser.Parse(new byte[CommandParser.MaxEnvelopeBytes + 1], "application/json", "101", TestData.Now));
    }

    [Fact]
    public void ExpiredOrTooLongLifetimeIsRejected()
    {
        var expired = TestData.Envelope();
        expired["expiresAt"] = StrictJson.Stamp(TestData.Now);
        Assert.Throws<InvalidDataException>(() => CommandParser.Parse(TestData.Bytes(expired), "application/json", "101", TestData.Now));
        var tooLong = TestData.Envelope();
        tooLong["expiresAt"] = StrictJson.Stamp(TestData.Now.AddSeconds(121));
        Assert.Throws<InvalidDataException>(() => CommandParser.Parse(TestData.Bytes(tooLong), "application/json", "101", TestData.Now));
    }

    [Fact]
    public void ResultKeepsRoutingFieldsAndAddsOutcome()
    {
        var command = TestData.Command();
        var eventId = Guid.NewGuid().ToString("D");
        var result = StrictJson.Parse(ResultEnvelope.Create(command, new(Outcomes.NotFound, ReasonCodes.OrderMissing), TestData.Now, eventId), CommandParser.MaxEnvelopeBytes);
        foreach (var name in new[] { "schemaVersion", "requestId", "operationId", "commandId", "toolCallId", "commandType", "branchId", "expiresAt", "route" })
            Assert.Equal(command.Envelope.GetProperty(name).GetRawText(), result.GetProperty(name).GetRawText());
        Assert.Equal("tool.result", result.GetProperty("kind").GetString());
        Assert.Equal(eventId, result.GetProperty("eventId").GetString());
        var data = StrictJson.Parse(StrictJson.FromBase64Url(result.GetProperty("dataB64").GetString()!), CommandParser.MaxEnvelopeBytes);
        Assert.Equal(Outcomes.NotFound, data.GetProperty("outcome").GetString());
        Assert.Equal(ReasonCodes.OrderMissing, data.GetProperty("reasonCode").GetString());
    }
}
