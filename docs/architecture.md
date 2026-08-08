# Architecture (v1)

## Overview

The POC uses Aspire AppHost to orchestrate:

- Service Bus Emulator
- Receiver service (consumer)
- Sender module (UI + send operation)

The sender module publishes messages to the emulator. The receiver service consumes from the same configured entity and writes output to console.

This module is intended for local development only. In cloud environments, the emulator is replaced with Azure Service Bus and standard tooling (for example, open source Service Bus Explorer) is used directly.

## Component Model

1. AppHost
- Boots and configures all dependent resources.
- Provides local developer entrypoint.

2. Service Bus Emulator
- Local message broker endpoint for development/testing.

3. Sender Module
- Basic standalone web frontend for header and body input.
- Pre-populates editable standard headers: `timestamp`, `entity-name`, `target-application`.
- Supports adding arbitrary custom headers as extra key/value pairs.
- Accepts body as JSON and validates before publish.
- Creates outbound Service Bus message.
- Sends to configured queue.
- Reports success/failure to user.

4. Receiver Service
- Subscribes to configured entity.
- Reads incoming messages.
- Deserializes JSON body into an object for downstream processing.
- Logs message metadata and payload.

## Runtime Flow

1. Developer starts AppHost.
2. AppHost starts Service Bus Emulator.
3. AppHost starts receiver service and sender module.
4. User opens sender frontend using its Aspire-exposed service URL.
5. User enters headers and body, then submits.
6. Sender module sends message to emulator.
7. Receiver service consumes message and logs content.

## User Journey (Local Developer)

1. Start AppHost and wait until all services report healthy/running.
2. In Aspire, locate the sender frontend service and open its URL.
3. In the sender page, enter one or more headers and a message body.
  - Edit standard headers (`timestamp`, `entity-name`, `target-application`) as needed.
  - Add any additional custom headers needed for the scenario.
  - Provide a valid JSON body.
4. Submit the message.
5. Confirm the sender shows a success response (or a clear validation/send error).
6. Open receiver service logs and verify the message body and headers were consumed.

Expected result: a developer can inject test messages through the sender URL without external tooling and immediately validate downstream processing in receiver logs.

## Configuration (Draft)

- Connection settings provided through Aspire resource wiring.
- Shared queue name used by sender and receiver (`default-queue`).
- Optional defaults for sender UI (example headers and body template).
- Startup dependencies are declared in Aspire so sender and receiver both reference the Service Bus resource and wait for it to become available before attempting connections.

## Error Handling (Draft)

- Sender module:
  - input validation errors
  - connection/send failures
- Receiver service:
  - receive/deserialize errors
  - transient retry behavior based on SDK defaults (to confirm)

## Observability (Draft)

- Receiver emits structured JSON logs including message metadata and payload.
- Logs are written to console and can optionally also be written to a local log file.
- Sender module exposes immediate send feedback.
- Optional correlation field can be added later if needed.
- OpenTelemetry is expected in real projects but is intentionally out of scope for this POC.

## Key Risks

1. Local-only scope may not validate cloud-specific behavior end-to-end; cloud workflows rely on Azure Service Bus plus standard tooling rather than this emulator-focused module.
2. Startup ordering issues if sender/receiver start before emulator readiness. Mitigation: use standard Aspire dependency wiring (resource reference plus wait-for dependency).
3. Sender web frontend discoverability may be confusing if multiple local service URLs are exposed.

## Clarifications Needed

1. Preferred SDK and message property mapping conventions.
2. Whether receiver acknowledgement behavior needs to be configurable in v1.
