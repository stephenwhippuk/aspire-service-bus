# POC 0.3 Delivery Plan

## Objective

POC 0.3 is the presentation-ready milestone for the squad. The work is split into two parallel streams:

1. Modernise the receiver so it becomes an Azure Durable Function app that listens to Service Bus.
2. Refresh the sender experience so it feels more polished and demo-friendly.

## Workstream 1 — Receiver becomes an Azure Durable Function

### Goal

Replace the current hosted receiver service with an Azure Functions app that processes Service Bus messages through Durable Functions.

### Proposed architecture

- Create a new Azure Functions project for the receiver.
- Use an isolated worker model for compatibility with current .NET and local development tooling.
- Add a Service Bus trigger function that starts a Durable Functions orchestration when a message arrives.
- Add an orchestrator and an activity function to handle message processing and status updates.
- Keep the existing queue contract unchanged so the sender does not need a breaking change in this milestone.

### Planned implementation steps

1. Create a new Azure Functions receiver project and wire it into the solution.
2. Configure local settings for Service Bus connection details and Azure Functions runtime settings.
3. Implement a Service Bus-triggered orchestration entrypoint.
4. Add a durable orchestration and activity pair for processing the incoming message.
5. Update AppHost wiring so the function app starts alongside the other resources.
6. Verify the end-to-end flow from sender to function execution.

### Expected outcome

- The receiver is no longer a simple hosted worker.
- Messages are processed through a durable workflow that can be expanded later.
- The solution is more aligned with the Azure-native pattern expected in the real project.

### Risks and mitigations

- Local Azure Functions startup can be more involved than the current hosted service approach.
- The team should confirm the local runtime tooling and storage configuration early.
- The initial slice should keep the processing logic simple so the architecture can be demonstrated quickly.
- A lightweight storage approach is preferred so the durable function remains easy to run locally without introducing unnecessary operational complexity.

### Lightweight storage recommendation

For the placeholder durable function, the best fit is to use Azurite as the local storage emulator.

Recommended option:
- Use Azurite as a local emulator for Blob, Queue, and Table storage.
- Run it in a lightweight Docker container so it is easy to start on a Windows demo machine and simple to reset between runs.
- Wire it into the local developer experience so the durable function can run without needing a live Azure Storage account.

Why this is the best fit:
- It is the most widely supported local option for Azure Functions and Durable Functions.
- It matches the storage needs of the Functions runtime without introducing cloud costs or extra setup complexity.
- It is easy to reset between runs by clearing the local Azurite workspace data.
- It is a better fit for a POC than an in-memory or file-based approach, which would not behave like the real Durable Functions runtime.

Alternatives to avoid for this milestone:
- A full Azure Storage account for local development is workable but adds unnecessary setup and cost for a demo-focused POC.
- The older Azure Storage Emulator is now superseded by Azurite and is not the preferred path.
- Pure in-memory persistence is too far from the real runtime behavior and would make the demo less credible.

## Workstream 2 — Sender UI refresh for demo readiness

### Goal

Make the sender experience feel like a polished internal tool for squad review.

### Planned layout changes

- Move message creation and editing into an off-canvas panel.
- Add a prominent New Message button that opens the compose experience.
- Replace the current single-page form with a more structured dashboard layout.
- Introduce tabs for the following status views:
  - Pending
  - Processing
  - Success
  - Failed
  - Received
- The UI should show real data wherever it exists, and otherwise present a clear “Coming soon” placeholder.
- The placeholder states should be styled to fit the same black-and-green theme so they feel intentional rather than unfinished.
- For this milestone, only the Success and Failed views should be functional; the other tabs should remain placeholder states for the demo.

### Planned visual theme

- Adopt Bootstrap 5 for layout and components.
- Use a black-and-green palette to match the existing direction in the real projects.
- Add Font Awesome icons for buttons and status indicators.
- Use outline-style buttons for a lighter, more deliberate presentation feel.

### Planned implementation steps

1. Rework the sender page into a Bootstrap 5 shell with a header, content area, and off-canvas compose drawer.
2. Introduce a state-based tab navigation model for the message views.
3. Add the new message button and move validation/send actions into the drawer experience.
4. Apply the black-and-green styling system and outline button variants.
5. Add Font Awesome iconography to key actions and status states.
6. Preserve the current send and validation behaviour while improving the presentation layer.

### Expected outcome

- The sender page looks more intentional and presentation-ready.
- The message workflow is easier to explain during a squad demo.
- The UI is ready for future stateful behaviour without needing a full redesign.

## Suggested delivery order

1. Receiver architecture spike and function project scaffolding.
2. Sender UI shell and theming.
3. Connect the new UI states to the existing sender history flow.
4. Demo polish and walkthrough preparation.

## Open questions

- Should Azurite be run in Docker or via the Node-based CLI for the local developer experience?
- Should the placeholder tabs keep a consistent visual treatment so the demo feels deliberate even when content is not yet implemented?
