# Developer Startup Guide

## Prerequisites

- .NET 8 SDK
- Docker Desktop or another container runtime supported by Aspire for the Service Bus emulator

## Start the full POC

From the repository root, run:

```bash
dotnet run --project src/AspireServiceBus.AppHost/AspireServiceBus.AppHost.csproj
```

Aspire will start the Service Bus emulator, the receiver, and the sender. The dashboard URL is printed in the console, and the sender web frontend is exposed through the Aspire service endpoint.

## Optional local logging

If you want structured log file output in addition to console logs, set either of the following before starting the app:

```bash
export SERVICEBUS_LOG_FILE=/tmp/aspire-service-bus.log
```

or add a configuration value in the relevant app settings:

```json
{
  "Logging": {
    "LocalFilePath": "/tmp/aspire-service-bus.log"
  }
}
```

## Verify the flow

1. Open the sender URL from the Aspire dashboard.
2. Submit a valid JSON body and standard headers.
3. Confirm the receiver logs show the message id, headers, and body in structured JSON.
4. Try invalid JSON and confirm the sender blocks the submit before any message is sent.
