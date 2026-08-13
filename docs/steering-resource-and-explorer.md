# Steering: Sender as an Aspire Resource and Service Bus Explorer Panel

## Summary

The POC direction is now to evolve the sender from a local demo UI into a first-class Aspire resource that can be packaged and consumed as a NuGet component, then wired into AppHost with minimal configuration. The long-term goal is for the sender experience to behave like a Service Bus Explorer for local development: it should expose the registered Service Bus entities and provide enough observability to inspect queues, topics, subscriptions, and message activity without relying on separate tooling.

## Steering Decisions

### 1. Sender becomes a proper Aspire resource

The sender project should be treated as an application/resource in the Aspire model rather than as a bespoke local web app embedded only in this repository.

Implementation implications:

- The sender should be packaged as a reusable NuGet component.
- AppHost should be able to add the sender resource declaratively and wire its dependencies automatically.
- The sender should be able to discover and bind to the Service Bus resource configured in AppHost.
- The resource should support a predictable startup and dependency ordering so it can work reliably with the emulator and receiver.

### 2. The sender UI should function as a Service Bus Explorer experience

The sender UI should evolve into an observability surface for the virtual Service Bus, not just a message submission form.

Expected behavior:

- A left-side explorer panel should list Service Bus entities such as queues and topics.
- The explorer should surface the entities exposed by the registered Service Bus resource.
- The UI should allow the user to inspect entity details and understand what is available in the local environment.
- The experience should be usable as a lightweight local alternative to traditional Service Bus Explorer tooling.

### 3. Multi-service-bus support is the next milestone

The first implementation should be designed so that multiple Service Bus namespaces/resources can be represented in the UI in the next milestone.

Design expectations:

- The explorer model should not assume a single Service Bus instance.
- The resource abstraction should support multiple registered Service Bus endpoints in a future iteration.
- The UI should be ready for a tenant/namespace-based grouping model rather than a single hard-coded connection.

## Target Outcome

By the next milestone, the app should be able to:

1. Add the sender as a first-class Aspire resource from AppHost.
2. Connect the sender to a Service Bus resource without custom hand-rolled wiring.
3. Open a UI that shows an explorer-style panel on the left for Service Bus entities.
4. Provide basic observability for the virtual Service Bus in the local development environment.

## Scope for the Next Implementation Phase

### In scope

- Aspire resource packaging and AppHost integration for the sender.
- A left-hand explorer panel in the sender UI.
- Entity discovery for the currently configured Service Bus resource.
- Basic queue/topic visibility and initial selection flow.
- Clear separation between message submission and entity observability.

### Out of scope for the next phase

- Full multi-namespace management.
- Advanced topic/subscription management workflows.
- Production-grade security and operational hardening.
- Full message browsing and replay capabilities beyond the initial explorer experience.

## Implementation Planning Notes

### Architecture direction

- Introduce a small abstraction for Service Bus entity discovery so the UI can render a consistent explorer model.
- Keep the UI layered so the explorer panel and the message send/history surface can evolve independently.
- Prefer a reusable resource contract that can later support multiple Service Bus instances.

### Delivery phases

1. Resource packaging and AppHost wiring
   - Package the sender as a NuGet-based Aspire component.
   - Add AppHost integration points for attaching the sender to a Service Bus resource.

2. Explorer panel foundation
   - Add a left-side explorer pane in the sender UI.
   - Render discovered queues and topics from the Service Bus resource.

3. Observability enhancements
   - Show entity metadata and simple status information.
   - Prepare the UI model for supporting multiple Service Bus registrations in the following milestone.

## Open Questions

- What exact Aspire resource contract should the sender implement for AppHost integration?
- Should entity discovery be driven by the emulator, a local discovery endpoint, or a simple configuration model initially?
- What level of entity detail is required in the first explorer iteration (queue/topic names, status, counts, subscriptions)?
- How much of the UI should be shared between the explorer and the message history workflow?
