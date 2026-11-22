using Sdv.Worker;
using Xunit;

public sealed class TransportTests
{
    [Theory]
    [InlineData("sdv:test:alice", "sdv-worker-c71e04710c72032b08cb7da273ab24b5d28aec47bb7db20cf6c0b61897bb296e")]
    [InlineData("sdv:test:alice&bob", "sdv-worker-15f50b529cb666b90666561f5ffd5f7373993888de185816cb8d487703bda43e")]
    public void NotificationGroupMatchesTheD2Contract(string owner, string expected)
    {
        var caller = new Caller(owner, "sandbox-a", "test-call");
        Assert.Equal(expected, AzureTransport.Group(caller));
    }
}
