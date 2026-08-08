# Implementation Checklist (v1)

This checklist translates the approved v1 requirements into executable work items.

## Scope Reminder

- Local development only.
- Service Bus Emulator only.
- Queue-only messaging using `default-queue`.
- Sender is a standalone web frontend opened via Aspire service URL.
- Body is JSON only.
- Logging is structured JSON to console, with optional local file output.

## Recommended Delivery Sequence

Current status:
- [x] Boilerplate solution and projects are scaffolded.
- [x] AppHost, sender, and receiver compile successfully.
- [x] AppHost runtime startup verification completed on Ubuntu 26.04 using upgraded Aspire AppHost toolchain.

Runtime blocker notes:
- Previous DCP TLS issue was observed with Aspire 8.2.2.
- Upgrading to `Aspire.Hosting.AppHost` and `Aspire.AppHost.Sdk` version `13.4.6` resolved startup on this host.

### Day 1: Vertical Slice First

- [x] Implement minimal AppHost wiring (sender service, receiver service, shared Service Bus connection reference).
- [x] Stand up receiver with basic queue consumption from `default-queue`.
- [x] Stand up sender with minimal form (standard headers + JSON body + submit).
- [x] Validate first end-to-end message send/receive using structured JSON console logs.

Exit criteria:
- [x] Sender URL opens from Aspire.
- [x] One valid JSON message is published and consumed.
- [x] Receiver logs include timestamp, message id, headers, and body.

### Day 2: Validation and Usability

- [x] Add custom header add/remove support in sender UI.
- [x] Add sender validation states (required headers, non-empty values, invalid JSON).
- [x] Add clear send failure handling when emulator is unavailable.
- [x] Add receiver deserialization failure handling with structured error logs.
- [x] Optionally enable local file log output in addition to console.

Exit criteria:
- [x] Invalid JSON is blocked pre-send.
- [x] Edited standard headers and custom headers arrive and are logged.
- [x] Emulator-down failure path is clear to the user.

### Day 3: Hardening and Verification

- [x] Run and document all end-to-end verification scenarios.
- [x] Clean up config defaults and developer startup instructions.
- [x] Capture deferred enhancements for advanced POC (topics/subscriptions, OpenTelemetry).

Exit criteria:
- [x] All acceptance criteria are demonstrated.
- [x] Known limitations and next-phase items are recorded.

## Workstream A: AppHost and Resource Wiring

- [x] Create Aspire AppHost solution structure for the POC.
- [x] Add Service Bus Emulator resource to AppHost.
- [x] Configure shared queue name as `default-queue`.
- [x] Add sender web service to AppHost.
- [x] Add receiver service to AppHost.
- [x] Wire sender to Service Bus resource reference.
- [x] Wire receiver to Service Bus resource reference.
- [x] Add wait dependency so sender starts after emulator is available.
- [x] Add wait dependency so receiver starts after emulator is available.
- [x] Confirm sender URL is exposed and clickable from Aspire.

## Workstream B: Sender Web Frontend

- [x] Build minimal sender page with submit workflow.
- [x] Provide editable standard header inputs: `timestamp`, `entity-name`, `target-application`.
- [x] Pre-populate default values for standard headers.
- [x] Allow adding custom header key/value rows.
- [x] Allow deleting custom header rows.
- [x] Add JSON body input field.
- [x] Validate body as JSON before publish.
- [x] Show clear validation errors for invalid JSON.
- [x] Serialize headers and JSON body into outbound message.
- [x] Send outbound message to `default-queue`.
- [x] Show success state after publish.
- [x] Show clear failure state for send errors.

## Workstream C: Receiver Service

- [x] Create receiver consumer bound to `default-queue`.
- [x] Receive messages continuously while service is running.
- [x] Read standard and custom headers from message metadata.
- [x] Deserialize body JSON into an object.
- [x] Handle deserialization failures with structured error logs.
- [x] Emit structured JSON log for each received message.
- [x] Include at least timestamp, message identifier, headers/properties, and body in logs.
- [x] Optional: add local structured log file output.

## Workstream D: Validation and UX Safety

- [x] Add sender-side checks that required standard headers are present.
- [x] Add sender-side checks that required standard header values are non-empty.
- [x] Prevent submit while validation errors exist.
- [x] Add clear user-facing error messages for emulator unavailable scenarios.
- [x] Verify startup race conditions are prevented by wait dependencies.

## Workstream E: End-to-End Verification

- [x] Scenario 1: valid standard headers + valid JSON body publishes and is received.
- [x] Scenario 2: edited standard headers are visible in receiver logs.
- [x] Scenario 3: added custom headers are visible in receiver logs.
- [x] Scenario 4: invalid JSON is blocked before send.
- [x] Scenario 5: emulator unavailable shows send failure clearly.
- [x] Scenario 6: multiple sequential sends are all received without restart.
- [x] Scenario 7: sender frontend reachable via Aspire service URL.

## Deliverables

- [x] AppHost project wiring completed.
- [x] Sender web frontend implemented.
- [x] Receiver service implemented.
- [x] Structured logging implemented.
- [x] Verification scenarios executed and recorded.

## Done Criteria

- [x] All Workstream A-E checklist items are complete.
- [x] All acceptance criteria in requirements are satisfied.
- [x] Remaining gaps and deferred items are documented.
