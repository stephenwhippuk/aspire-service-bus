# Local Aspire Service Bus setup

This document captures the current local topology for the Aspire-based sender/receiver sample and explains how the pieces interact when the app is started from the AppHost.

## Components

- AppHost: orchestrates the local Aspire application, starts the Service Bus emulator, and injects runtime configuration into the sender and receiver.
- Sender: provides the web UI and the HTTP send endpoint. It records message history to a local NDJSON file and exposes the explorer payload used by the composer UI.
- Receiver: runs as a lightweight web-service worker that listens to the queue, logs each message payload, and treats non-store entity messages as failures so they can be retried and dead-lettered.
- Service Bus emulator: runs locally in Docker and exposes AMQP on a predictable host port so the sender and receiver can connect without depending on cloud credentials.
- Azurite: provides the storage emulator for the receiver and any Azure Storage dependencies the local runtime expects.

## Startup flow

1. The AppHost starts the Service Bus emulator and Azurite through Aspire.
2. The AppHost resolves the local Service Bus endpoint and injects a connection string into both the sender and receiver.
3. The sender starts on its HTTP endpoint and exposes `/send` plus the explorer payload at `/explorer/entities`.
4. The receiver starts its background worker and subscribes to the configured queue name.
5. When the sender posts a message, the sender writes a history entry and attempts to send the message via Service Bus. The receiver consumes it when the message is accepted by the emulator.

## Message handling behavior

The current receiver behavior is intentionally explicit:

- Messages with `entity-name = store` are treated as valid and processed normally.
- Messages with a different entity name are treated as non-store messages and are allowed to fail so they can be retried and placed in the dead-letter queue.

That behavior mirrors the original demo intent and gives the sender/receiver pair a deterministic way to exercise the service-bus dead-letter flow without needing a separate consumer implementation.

## Explorer behavior

The sender explorer surface intentionally shows one primary queue entry and collapses the dead-letter flow into a compact pair:

- The badge renders as `(queued, dead-lettered)`.
- The selected queue remains the primary queue used for sends.
- The dead-letter queue is still tracked internally for diagnostics and future context-menu work.

## Local verification steps

From the repository root, run:

```bash
dotnet run --project src/AspireServiceBus.AppHost/AspireServiceBus.AppHost.csproj
```

Then verify the following:

1. The Aspire dashboard starts and shows the sender and receiver.
2. The sender UI is reachable from the dashboard endpoint.
3. A POST to `/send` succeeds and creates a history entry.
4. The receiver logs the message payload and the queue processing activity.
5. The explorer endpoint returns a single primary queue entry with the `(queued, dead-lettered)` badge state.

## Notes for contributors

- The AppHost now uses the explicit host port for the local emulator so the connection string is deterministic.
- Runtime-generated files such as local history and emulator state should be kept out of the commit set; they should be ignored or cleaned before creating a PR.
