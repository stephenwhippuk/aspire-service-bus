# Day 3 Verification Scenarios

The following scenarios capture the Day 3 acceptance checks for the POC.

## Scenario 1: valid message publishes and is received

1. Start the AppHost.
2. Open the sender web UI from the Aspire dashboard.
3. Leave the standard headers at their defaults and submit a valid JSON body.
4. Confirm the receiver logs contain the same payload and headers.

## Scenario 2: edited standard headers are retained

1. Edit the `timestamp`, `entity-name`, and `target-application` values in the sender UI.
2. Submit the message.
3. Confirm the receiver logs reflect the edited values in the captured headers.

## Scenario 3: custom headers are preserved

1. Add one or more custom header key/value rows.
2. Submit the message.
3. Confirm those header keys are present in the receiver log metadata.

## Scenario 4: invalid JSON is blocked

1. Enter malformed JSON in the body field.
2. Try to submit.
3. Confirm the sender prevents the submit and shows a validation message.

## Scenario 5: emulator-down path is clear

1. Stop the emulator or temporarily interrupt the Service Bus connection.
2. Submit a message from the sender UI.
3. Confirm the sender shows a clear failure message and the error is logged locally if logging is enabled.

## Scenario 6: sequential sends work without restart

1. Submit several messages one after another.
2. Confirm each message is received and logged by the receiver without restarting the services.

## Scenario 7: sender is reachable from Aspire

1. Use the Aspire service URL exposed by the dashboard.
2. Confirm the sender page loads successfully.
