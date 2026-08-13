using AspireServiceBus.Receiver;
using System.Text.Json;

namespace AspireServiceBus.Receiver.Tests;

public class ReceiverEnvelopeTests
{
    [Fact]
    public void Envelope_CanRoundTripThroughJson()
    {
        var envelope = new ReceivedMessageEnvelope(
            MessageId: "abc123",
            Body: "{\"message\":\"hello\"}",
            Headers: new Dictionary<string, string> { ["entity-name"] = "default-queue", ["trace"] = "demo" });

        var json = JsonSerializer.Serialize(envelope);
        var roundTripped = JsonSerializer.Deserialize<ReceivedMessageEnvelope>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(envelope.MessageId, roundTripped!.MessageId);
        Assert.Equal(envelope.Body, roundTripped.Body);
        Assert.Equal(envelope.Headers["entity-name"], roundTripped.Headers["entity-name"]);
        Assert.Equal(envelope.Headers["trace"], roundTripped.Headers["trace"]);
    }

    [Theory]
    [InlineData("store", true)]
    [InlineData("STORE", true)]
    [InlineData("non-store", false)]
    [InlineData("none-store", false)]
    [InlineData(null, false)]
    public void Worker_DetectsStoreMessages(string? entityName, bool expected)
    {
        Assert.Equal(expected, Worker.IsStoreMessage(entityName));
    }
}
