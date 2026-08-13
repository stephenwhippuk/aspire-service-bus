# Aspire Service Bus POC Docs

This documentation set expands the initial brief in [specification.md](../specification.md).

## Documents

- [requirements.md](requirements.md): v1 scope, goals, non-goals, and acceptance criteria.
- [architecture.md](architecture.md): component model, runtime flow, and technical decisions for the POC.
- [implementation-checklist.md](implementation-checklist.md): executable task list grouped by component and verification flow.
- [poc-0.3-plan.md](poc-0.3-plan.md): proposed delivery plan for the next presentation milestone covering the receiver function app work and the sender UI redesign.
- [developer-startup.md](developer-startup.md): local runbook for launching the AppHost and exercising the sender/receiver flow.
- [local-service-bus-setup.md](local-service-bus-setup.md): end-to-end walkthrough of the local Aspire + Service Bus emulator + sender/receiver topology.
- [verification-scenarios.md](verification-scenarios.md): end-to-end verification steps for Day 3 acceptance scenarios.
- [steering-resource-and-explorer.md](steering-resource-and-explorer.md): steering decisions for making the sender a first-class Aspire resource and introducing a Service Bus explorer panel.
- [service-bus-debugging-strategy.md](service-bus-debugging-strategy.md): structured troubleshooting plan for isolating and eliminating possible causes of the local Service Bus emulator issue.

## Current Status

- Phase: POC 0.3 planning and Day 3 hardening complete
- Audience: local development and experimentation
- Maturity: proof of concept (POC)

## How To Use These Docs

1. Confirm or adjust the requirements in [requirements.md](requirements.md).
2. Validate the design in [architecture.md](architecture.md).
3. Execute delivery tasks from [implementation-checklist.md](implementation-checklist.md).
