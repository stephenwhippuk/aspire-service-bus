# Requirements (v1)

## Problem Statement

The Microsoft Service Bus Emulator used with .NET Aspire is not currently compatible with the open source Service Bus Explorer workflow used by the team. This makes local message creation and injection into the emulator cumbersome.

## Goal

Provide a POC Aspire module that offers a convenient UI for creating and publishing Service Bus messages to a target listening service through the emulator while the AppHost is running.

## In Scope (v1)

- Aspire AppHost project that orchestrates all required services.
- Microsoft Service Bus Emulator via Aspire integration.
- A receiver/consumer service that reads messages from the emulator and writes received payload plus metadata to console logs.
- A sender module with a simple UI that captures:
  - standard message headers (editable):
    - timestamp
    - entity-name
    - target-application
  - additional custom message headers (key/value pairs)
  - message body as a JSON document
- A basic web-based sender frontend that runs as its own service and is opened via the standard Aspire service URL link.

## Out of Scope (v1)

- Production hardening and security review.
- Cloud deployment messaging UX. In cloud environments, Azure Service Bus and standard tooling are used instead of the local emulator-focused module.
- Multi-tenant scenarios.
- Topic/subscription workflows and related UX. These are planned for a later, more advanced POC.
- Complex Service Bus features such as sessions, scheduling, transactions, duplicate detection tuning, or dead-letter tooling UX.
- Rich message history/audit UI.

## Functional Requirements

1. The system starts from AppHost and provisions the Service Bus Emulator dependency.
2. The sender UI can submit a message to the configured Service Bus queue (`default-queue`) in the emulator.
3. The receiver service can consume submitted messages.
4. The sender UI provides editable standard header values (`timestamp`, `entity-name`, `target-application`) and allows users to add additional custom headers.
5. The sender UI validates that the body is valid JSON before submit.
6. The receiver deserializes the JSON body into an object for processing.
7. The receiver logs at least:
   - timestamp
   - message identifier
   - headers/properties
   - message body
8. The end-to-end flow works repeatedly without restarting AppHost for each message.

## Non-Functional Requirements

- Local developer setup should be simple and repeatable.
- Errors should be visible in sender UI and receiver logs.
- Startup sequence should avoid race conditions between sender/receiver and emulator readiness by declaring explicit Aspire resource dependency and wait ordering.
- Logging output for this POC should be structured JSON, written to console and optionally to a local log file.
- Full OpenTelemetry integration is not required for this POC and is deferred to a real project implementation.

## Acceptance Criteria (Draft)

1. Given AppHost is running, when a user submits a valid header/body message in the sender UI, then the receiver logs the same body and headers.
2. Given invalid sender input, when submit is attempted, then the UI shows a clear validation error.
3. Given emulator is unavailable, when submit is attempted, then a clear send failure is shown.
4. Given multiple sequential sends, when messages are submitted, then each is received and logged.
5. Given AppHost is running, the sender frontend is reachable through its normal Aspire-exposed service URL.
6. Given sender defaults are shown, when a user edits any standard header (`timestamp`, `entity-name`, `target-application`) and submits, then the receiver logs reflect the edited values.
7. Given additional custom headers are added in the sender UI, when submit is successful, then those custom headers are present in the received message metadata.
8. Given a non-JSON body is provided, when submit is attempted, then the UI blocks send and shows a JSON validation error.
9. Given a message is received, logs are emitted as structured JSON to console (and optionally local file output if enabled).

## Open Decisions To Clarify

1. None currently.
