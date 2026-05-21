# Global Authority Setup Playbook

Date: 2026-05-19
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence class: `STATIC_DOC`. This is an implementation playbook, not runtime
proof.

Parents:

- `GLOBAL_AUTHORITY_BOUNDARIES.md`
- `GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`

## Purpose

This is the practical AAA setup sequence for using `GlobalRegistry`,
`SystemDispatcher`, `SignalBus<T>`, `GlobalSignals`, `HectonEventBus`,
`GlobalDataVault`, telemetry, and H-Phi without turning the project into a
global monolith.

Default stance:

```text
owner-local first
global only when ownership boundary is real
proof before readiness
```

## Hard Future Rules

- A read-looking API must be pure. `Get*`, `TryGet*`, `Resolve*`, and `Read*`
  methods must not publish, sync scene state, allocate or grow buffers, complete
  jobs, mutate global state, or search the scene.
- Publish once from the owner phase, then let consumers read snapshots or cached
  interfaces. Do not let every consumer pull-and-sync the same runtime context.
- Treat `GlobalRegistry` as cold identity only. Resolve dependencies during
  bootstrap, `OnRegister`, `OnDependencyInject`, or owner initialization.
- Use `SignalBus<T>` for first-party hot fan-out. Keep `GlobalSignals` as a
  documented bridge and `HectonEventBus` as mod/API/cold managed isolation.
- Use `GlobalDataVault` only for cross-domain native state. Domain runtime code
  must not use `TryGetLatestCreated()` as a normal fallback authority.
- Schedule Burst/Jobs only when work size amortizes scheduling and memory motion.
  Completion belongs in dispatcher-owned swap/completion windows.
- Do not claim Data Monolith readiness until the active StreamingAssets
  `static_data.h8bin` exists and passes import/bake/boot validation.
- Scale with continuous `GlobalQualityWeight`; never use quality to change
  gameplay truth owner, save identity, DTO layout, or authority route.

## Architecture Planes

| Plane | Owner | Tooling | Rule |
|---|---|---|---|
| Identity plane | bootstrap/core owner | `GlobalRegistry` | cold interface/service identity only |
| Time plane | dispatcher owner | `SystemDispatcher` | phase order and completion windows only |
| Signal plane | domain signal owner | `SignalBus<T>` | first-party broadcast only |
| Bridge plane | migration owner | `GlobalSignals` direct queues | temporary or explicitly owned bridge lanes only |
| Mod/API plane | modding owner | `HectonEventBus` | external/cold managed isolation only |
| Data plane | memory/data owner | `GlobalDataVault` / `IDataVault` | cross-domain native state only |
| Telemetry plane | crash/QA owner | black-box rings, `NativeMemorySentinel` | fault reconstruction and leak proof |
| Proof plane | QA/integrator owner | H-Phi, profiler, GC, Memory Profiler, player build | evidence-class enforcement |

No system may own all planes. If one class owns identity, time, signal, data,
and telemetry for multiple domains, it is becoming a god object.

## New Subsystem Setup

Use this sequence for every serious subsystem.

### Step 1 - Keep It Owner-Local

Start with private fields, owner-local native collections, and direct methods
inside the owning domain.

Allowed:

- private state
- local scratch `NativeArray`/`NativeList` with owner lifecycle
- direct method calls inside the same owner
- cached component references resolved at bootstrap/authoring

Rejected:

- new registry slot because another system might need it later
- signal lane before a real second consumer exists
- DataVault buffer for local scratch
- HectonEventBus event for first-party convenience

### Step 2 - Extract The Owner Interface

If another system needs immediate data or a command, expose a narrow interface.

Rules:

- interface lives in the lowest reasonable contract assembly
- implementation remains in the owner domain
- caller receives the interface through cold injection
- hot path caches the interface field
- live replacement uses ready/changed/shutdown signal

Do not use a signal for one caller.

### Step 3 - Add GlobalRegistry Only For Cold Identity

Add `GlobalRegistry` only when a stable owner interface must be discoverable by
bootstrap, dispatcher, save, telemetry, or other long-lived infrastructure.

Minimum proof:

- route card
- interface name
- bootstrap registration owner
- shutdown owner
- hot-path cache plan
- failure behavior if absent

Rejected:

- per-frame `GlobalRegistry.*` reads
- slot for absent/future system
- slot for mutable state instead of service identity

### Step 4 - Add SignalBus For Fan-Out

Use `SignalBus<T>` when one owner publishes a state change to multiple first-party
listeners or across dispatcher phases.

Minimum proof:

- route card
- payload struct
- owner assembly/domain
- producer phase
- consumer phase
- max events per frame
- overflow policy
- retention policy
- duplicate signal-name scan
- telemetry counters
- unmanaged/layout proof

Rejected:

- one private consumer
- catch-all enum payload
- Unity object payloads
- using `HectonEventBus` to avoid payload design

### Step 5 - Keep GlobalSignals As Bridge Only

Use direct `GlobalSignals` queues only when maintaining or migrating a legacy
lane. New gameplay traffic goes to typed `SignalBus<T>`.

Bridge requirements:

- source lane
- target typed lane
- drain phase
- owner
- migration stop condition
- telemetry for retained bridge traffic

If the bridge has no stop condition, it is not a bridge. It is new debt.

### Step 6 - Add DataVault Only For Shared Native State

Use `GlobalDataVault` when state crosses domains, scenes, job owners, save/replay,
crash telemetry, or relocation/defrag boundaries.

Minimum proof:

- route card
- `BufferID`
- `SystemID`
- owner
- capacity
- generation rule
- stale-handle behavior
- disposal/release path
- reader fencing
- crash/telemetry fields
- scene-unload baseline expectation

Rejected:

- local scratch moved to Vault
- buffer for absent system
- shared raw native collection reference
- no stale-handle test plan

### Step 7 - Use HectonEventBus Only At The Mod/API Boundary

Use `HectonEventBus` when external mod/API isolation, managed callback watchdogs,
or cold meta/progression events are the actual requirement.

Rejected:

- first-party hot gameplay
- Tick/FixedTick/LateUpdate/UI refresh/audio/physics/render upload
- event route used to skip SignalBus payload ownership

### Step 8 - Add Telemetry Before Claiming Safety

Every global route must be visible to fault analysis.

Minimum:

- route id/hash
- owner id/hash
- phase
- cadence
- last payload/data hash
- overflow/drop/coalesce/stale-handle counters
- shutdown/disposal state
- black-box ring field or crash dump field for critical systems

If a failure cannot name route, owner, and phase, the route is not production
ready.

### Step 9 - Add Continuous Quality Behavior

Every scalable route must define `GlobalQualityWeight` behavior.

Rules:

- quality may change presentation consumers
- quality may reduce optional telemetry consumers
- quality may coalesce/drop presentation traffic
- quality must not change gameplay authority owner
- quality must not make high-end path mutate different truth

### Step 10 - Prove It

Required proof depends on route type, but the acceptance ladder is:

1. static scan and route card
2. compile/Unity Console
3. Play Mode smoke
4. profiler/GC capture
5. stress overflow/stale-handle/failure test
6. player build or target hardware proof when player-facing

H-Phi may select review targets. H-Phi does not accept the route.

## Scenario Recipes

### Player State Needed By UI And Audio

Correct:

- owner: player movement/survival system
- route: typed dirty `SignalBus<T>` in `POST_SIMULATION`
- consumers: UI/audio in `VISUAL_SYNC`
- direct rare query: cached owner interface

Rejected:

- UI and audio polling `GlobalRegistry.Player` every frame
- `HectonEventBus` for player hot state

### Save Needs Persistent Native State

Correct:

- owner keeps writes
- cross-domain persistent buffer uses `GlobalDataVault`
- save reads generation-checked snapshot
- scene unload validates release/baseline

Rejected:

- save system holding raw native buffer from another domain
- moving all transient save scratch into Vault

### Mod Wants To Observe Achievement Unlock

Correct:

- first-party progression owns truth
- cold event projected through `HectonEventBus`
- payload is copied/sanitized for mod boundary

Rejected:

- gameplay waiting for mod callback
- hot progression loop routed through managed mod event

### Renderer Wants Rich Visual Response

Correct:

- gameplay truth publishes typed signal or DataVault snapshot
- renderer consumes in `VISUAL_SYNC`
- high `GlobalQualityWeight` may add richer visual consumers

Rejected:

- renderer mutating gameplay state
- renderer polling registry for live gameplay state every frame

## Review Cadence

For each sprint/batch:

1. Reject new global routes without cards.
2. Review cards added since last batch.
3. Classify each as accepted, rejected, blocked, or needs runtime proof.
4. Update `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md` if review queues changed.
5. Run static scans before claiming global-authority improvement.
6. Do not rerun H-Phi for vanity; rerun after real surface movement.

## Static Checks

Use these checks as orientation, not proof:

```powershell
rg -n "GlobalRegistry\\." Assets/_Project/Scripts -g "*.cs"
rg -n "HectonEventBus\\.(Publish|Subscribe)|GlobalSignals\\.Publish|SignalBus<[^>]+>\\.(Push|TryPush)" Assets/_Project/Scripts -g "*.cs"
rg -n "\\bNative(Array|List|HashMap|ParallelHashMap|Queue)<" Assets/_Project/Scripts -g "*.cs"
rg -n "GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE|Route ID:" Docs/Tasks Docs/AgentLogs Docs/ARCHITECTURE -g "*.md"
```

## Final Rule

The best global architecture is mostly invisible during gameplay. If profiling
shows the player is paying for registry lookup, managed event dispatch,
unbounded signal storms, DataVault misuse, or telemetry spam, the setup failed.

Status remains `PENDING VERIFICATION` until runtime artifacts prove otherwise.
