# Service Bus Debugging Strategy

This document records a structured approach to isolating the local Service Bus emulator problem without bouncing between hypotheses.

## Objective

Determine why the sender cannot successfully publish to the local Service Bus emulator and, once the cause is identified, verify the receiver path end to end.

## Working principle

Each step should answer one question clearly:

- What hypothesis is being tested?
- What evidence would support it?
- What evidence would rule it out?
- What is the next action if it passes or fails?

## Current hypotheses

### 1. The sender is using the wrong connection endpoint

Hypothesis: the sender is connecting to an endpoint that is not the one exposed by the current emulator container.

Test:
- Inspect the runtime environment of the sender process.
- Compare the connection string injected by Aspire with the actual host/port values exposed by the emulator container.

What this rules out:
- If the runtime environment points to a port that is not actually reachable, this is a likely cause.
- If the runtime environment points to a reachable port and the connection still fails, this specific host/port reachability problem is not the cause.

Current result:
- The sender process is configured with ConnectionStrings__servicebus=Endpoint=sb://localhost:40373;... and a direct localhost probe showed that port 40373 is reachable.
- This rules out case 1 as a simple wrong-host/wrong-port reachability issue.

### 2. The emulator container is not accepting Service Bus traffic on the expected port

Hypothesis: the container is up, but the Service Bus endpoint is not reachable on the localhost port expected by the client.

Test:
- Check the running container ports.
- Probe the specific port from the host.

What this rules out:
- If the port is reachable and accepts connections, the issue is not a container exposure problem.
- If the port is closed or refused, the problem is at the emulator/container layer.

Current result:
- A direct probe using the Azure Service Bus client library succeeded in creating a sender and sending a message to the emulator endpoint.
- This rules out case 2 as a broker-level endpoint problem.

### 3. The queue name or namespace configuration is not aligned with the emulator

Hypothesis: the sender or receiver is targeting the wrong queue or using configuration values that do not match the runtime service definition.

Test:
- Compare the queue name used in the sender and receiver with the queue configured in the Aspire app host.
- Confirm both sides are using the same queue name.

What this rules out:
- If both sides agree on the queue name and the send still fails, this is not the cause.
- If one side targets a different queue, this is a direct cause to fix.

Current result:
- The queue name is consistent across the AppHost, sender, receiver, and local settings files: default-queue.
- This rules out case 3 as a queue-name mismatch.

### 4. The receiver trigger binding is not loading correctly

Hypothesis: the Functions host is starting, but the Service Bus trigger binding is not resolving the connection correctly.

Test:
- Inspect the receiver host startup logs.
- Confirm that the trigger function loads successfully and that the Service Bus connection setting is resolved.

What this rules out:
- If the trigger loads successfully and the sender publishes, then the receiver binding is not the blocker.
- If the host fails to load the trigger or reports connection resolution errors, this becomes the active issue.

Current result:
- The AppHost now provisions an Azure Storage emulator for the Functions host and injects both AzureWebJobsServiceBus and ConnectionStrings__servicebus into the receiver process so the Service Bus trigger has the connection it expects.
- This addresses case 4 as the active startup blocker and leaves the receiver path ready for end-to-end verification.

### 5. The issue is in the application code path rather than the infrastructure

Hypothesis: the failure is caused by code-level handling or a bad assumption in the sender or receiver.

Test:
- Reproduce the issue with the simplest possible message.
- Check whether the failure appears before the message is handed off to the Service Bus client.

What this rules out:
- If the client can connect and send but the receiver still fails, the bug is in the application workflow.
- If the client cannot even create the connection, the infrastructure path is the real cause.

## Current plan

1. Verify the exact connection string and target port used by the sender process. (Completed)
2. Verify that the emulator container is exposing that port on localhost. (Completed)
3. Verify that the queue name is consistent across sender, receiver, and app host. (Completed)
4. Ensure the receiver process receives the same Service Bus connection string through AzureWebJobsServiceBus and inspect the trigger binding/runtime behavior.

## Decision rule

Do not move to the next hypothesis until the current one has either been confirmed or ruled out by fresh evidence.
