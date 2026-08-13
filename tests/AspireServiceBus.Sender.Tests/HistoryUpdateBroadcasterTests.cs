using System.Net.WebSockets;
using System.Text;
using AspireServiceBus.Sender;

namespace AspireServiceBus.Sender.Tests;

public class HistoryUpdateBroadcasterTests
{
    [Fact]
    public async Task BroadcastAsync_SendsJsonNotificationToConnectedClient()
    {
        var broadcaster = new HistoryUpdateBroadcaster();
        var socket = new TestWebSocket();

        await broadcaster.RegisterClientAsync(socket, CancellationToken.None);
        await broadcaster.BroadcastAsync(new { type = "history-changed" }, CancellationToken.None);

        Assert.Single(socket.Messages);
        Assert.Equal("{\"type\":\"history-changed\"}", socket.Messages[0]);
    }

    private sealed class TestWebSocket : WebSocket
    {
        public List<string> Messages { get; } = new();

        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        public override WebSocketState State => WebSocketState.Open;

        public override string? SubProtocol => null;

        public override void Abort() { }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Dispose() { }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var payload = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            Messages.Add(payload);
            return Task.CompletedTask;
        }
    }
}
