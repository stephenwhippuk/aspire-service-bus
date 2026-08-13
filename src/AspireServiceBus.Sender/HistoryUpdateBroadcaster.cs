using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AspireServiceBus.Sender;

public sealed class HistoryUpdateBroadcaster
{
    private readonly ConcurrentDictionary<Guid, ClientConnection> _clients = new();

    public Task RegisterClientAsync(WebSocket socket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var id = Guid.NewGuid();
        _clients[id] = new ClientConnection(id, socket);
        return Task.CompletedTask;
    }

    public Task RemoveClientAsync(WebSocket socket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        foreach (var client in _clients.Where(item => ReferenceEquals(item.Value.WebSocket, socket)).ToList())
        {
            _clients.TryRemove(client.Key, out _);
        }

        return Task.CompletedTask;
    }

    public async Task BroadcastAsync(object payload, CancellationToken cancellationToken = default)
    {
        var payloadText = JsonSerializer.Serialize(payload);
        var buffer = Encoding.UTF8.GetBytes(payloadText);
        var messageSegment = new ArraySegment<byte>(buffer);

        var deadClients = new List<Guid>();
        foreach (var client in _clients.Values)
        {
            try
            {
                if (client.WebSocket.State != WebSocketState.Open)
                {
                    deadClients.Add(client.Id);
                    continue;
                }

                await client.WebSocket.SendAsync(messageSegment, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            }
            catch (Exception)
            {
                deadClients.Add(client.Id);
            }
        }

        foreach (var deadClientId in deadClients)
        {
            _clients.TryRemove(deadClientId, out _);
        }
    }

    private sealed record ClientConnection(Guid Id, WebSocket WebSocket);
}
