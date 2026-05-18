# Owner Deep Dive â€” World, Gameplay, UI

Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


Purpose:
- identify the runtime owners that actually define HECTON-8
- explain why these owners are strengths and risks at the same time

## World Owners

### `HectonMapMagicVegetationBridge`

Static shape:
- file length: historical `~15,739`; R18 static line count `6,270`
- interfaces: `ITickable`, `ISlowTickable`, `IOriginShiftListener`
- native references: `442`
- job barrier calls: `12`
- registry references: `8`

What this means:
- this is not a helper bridge anymore
- it is effectively a world-runtime platform inside the project

Why it is strong:
- massive implementation depth
- real native/jobs discipline exists in volume
- world dressing, vegetation, and environmental presentation are not stubbed

Why it is dangerous:
- file scale alone makes reasoning expensive
- native density plus barrier density increases regression cost
- the name still understates how much authority the file actually holds

Read:
- high-value asset
- high-risk owner

### `WorldProceduralScatterDirector`

Static shape:
- file length: historical `~11,673`; R18 static line count `10,620`
- interfaces: `ITickable`, `ISlowTickable`, `IUpdatable`, `ISceneBootstrapEventListener`, `IWorldGenService`
- registry references: `11`
- partial class split across many files

What this means:
- scatter is not a local feature
- it is one of the projectâ€™s true runtime sovereigns

Why it is strong:
- owns a meaningful service contract
- explicitly participates in scene bootstrap flow
- procedural world is real, not just content authoring

Why it is dangerous:
- one owner spans cadence, bootstrap, worldgen service authority, and placement logic
- partial split helps organization but does not reduce ownership complexity

Read:
- genuine subsystem core
- architecture-compression target

## Gameplay Owners

### `HectonPlayerMovement`

Static shape:
- file length: historical `~9,099`; R18 static line count `11,818`
- interfaces: `IUpdatable`, `IFixedTickable`, `IOriginShiftListener`
- registry references: `15`
- no coroutine residue in the file
- no native container surface inside the owner itself

What this means:
- player movement is central orchestration code, not a narrow locomotion file

Why it is strong:
- player runtime is clearly integrated with the rest of the project
- it uses the dispatcher/registry path rather than `Update()` sprawl

Why it is dangerous:
- high dependency gravity
- a lot of responsibility sits in one owner that is not itself data-oriented
- if player state is wrong, many adjacent systems are wrong

Read:
- important
- too large
- likely one of the most expensive gameplay files to touch safely

### `PlayerInventory`

Static shape:
- file length: historical `~1,925`; R18 static line count `4,949`
- interfaces: `ISaveable`, `ISlowTickable`
- native references: `52`
- save integration via `GlobalRegistry.Save`

What this means:
- inventory is materially engineered
- it is not a toy `List<Item>` implementation

Why it is strong:
- SOA/native orientation is real
- sorting jobs and structured state words indicate serious runtime intent

Why it is dangerous:
- centrality is higher than its file count suggests
- inventory persistence, degradation, quality, and gameplay semantics are tightly packed

Read:
- one of the more mature gameplay systems
- still a dependency hotspot

## UI Owners

### `SuitHUDV4CanvasOverlay`

Static shape:
- file length: historical `~5,401`; R18 static line count `6,394`
- interfaces: `ITickable`, `IUpdatable`, `ISlowTickable`, `IOriginShiftListener`, `IUIService`, `ISceneBootstrapEventListener`
- registry references: `18`
- contains native `LateUpdate` usage in file

What this means:
- HUD is not a passive canvas wrapper
- it is an active runtime service with bootstrap participation

Why it is strong:
- zero-GC HUD discipline exists
- UI knows about runtime readiness and player context explicitly

Why it is dangerous:
- HUD now lives at the boundary of UI, bootstrap, audio, inventory, and player runtime
- that is effective, but it is also expensive coupling

Read:
- one of the projectâ€™s best-executed user-facing systems
- one of the projectâ€™s heaviest UI owners

### `PlayerPDA`

Static shape:
- file length: large, multi-class owner file
- interface: `ITickable`
- registry references: UI dispatcher registration plus player/audio lookups
- direct static `Action` event bus surface:
  - `OnOpened`
  - `OnClosed`
  - `OnTabChanged`
  - `OnLowBatteryShutdown`

Why it matters:
- PDA is real product logic, not mock UI
- but it also exposes a meaningful policy drift from the projectâ€™s stronger queue-backed event-bus story

Why it is strong:
- clear feature identity
- integrated with survival, tabs, audio, and player blocking

Why it is weak:
- direct static action events are a weaker event architecture than the queue-backed systems documented elsewhere
- UI authority is split across PDA, HUD, and scene-authored ownership

Read:
- implemented
- stylistically mixed

## Cross-Domain Reading

The projectâ€™s most important owners all share the same pattern:
- real implementation depth
- high cross-system authority
- growing architectural gravity

That is why the project feels substantial.
That is also why it feels dangerous.

## Brutal Summary

The core world/gameplay/UI owners are not fake.
They are overgrown.

The project is already beyond â€œdoes this system exist?â€
The correct question now is:

who is allowed to own this much at once without becoming a regression machine?
