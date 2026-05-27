# Global Authority Operating Model

Date: 2026-05-23
Status: PENDING VERIFICATION

Evidence class: STATIC_DOC / STATIC_SOURCE. This is a production operating model, not Unity import,
runtime, profiler, GC, compile, or player-build proof.

Evidence boundary:

- Dated local compile attempts, loop notes, and status logs are process evidence and stay outside this authority file.
- Static greps are triage only. They can find suspect routes, but they cannot prove runtime health.
- Runtime proof remains pending until Unity import, Play Mode, profiler, GC, player-build, and platform artifacts exist.

Parents:

- `GLOBAL_AUTHORITY_BOUNDARIES.md`

- `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`

- `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`

- `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`

- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`

- `DISPATCH_PIPELINE.md`

- `SYSTEMS_CONTRACTS.md`

- `QUALITY_GATES.md`

## Senior Verdict

The correct AAA setup is not "use GlobalRegistry, EventBus, SignalBus, and

DataVault everywhere." That creates a distributed singleton monolith.

The correct setup is a small runtime government:

1. `GlobalRegistry` owns cold identity and dependency injection.

2. `SystemDispatcher` owns time, phase order, and completion windows.

3. `SignalBus<T>` owns first-party broadcast state changes.

4. `GlobalDataVault` owns cross-domain native state and persistent snapshots.

5. `HectonEventBus` owns mod/API/cold managed isolation.

6. Black-box telemetry owns postmortem truth.

7. H-Phi owns static pressure detection only.

The core law is:

```text

one fact -> one owner -> one route -> one proof artifact

```

If a system cannot name the owner, route, phase, failure mode, and proof artifact,

it is not ready for a global route.

## Future Doctrine

These rules are mandatory for new work and for cleanup of existing global

surfaces:

- Accessors named `Get*`, `TryGet*`, `Resolve*`, or `Read*` are read-only. They

  must not publish, sync scene state, allocate or grow buffers, complete jobs,

  mutate global authority, or search the scene.

- Runtime context owners publish once in their own dispatcher phase. Consumers

  read cached owner interfaces, immutable frame snapshots, or generation-checked

  DataVault handles.

- `GlobalRegistry` is cold identity and dependency injection only. Hot gameplay,
  physics, render, UI, AI, audio, and logistics paths use cached dependencies.
- Registry hot-swap callbacks are cache invalidation only. They may refresh
  cached owner interfaces on service replacement; they must not carry gameplay
  traffic, poll every frame, or hide scene searches.
- `SignalBus<T>` is the first-party hot broadcast path. `GlobalSignals` direct
  queues are retained bridge lanes. `HectonEventBus` is mod/API/cold isolation.
- `GlobalDataVault` is cross-domain native ownership, not a global dictionary.

  Allocate, grow, and resolve ownership in cold setup or owned swap windows.

- `GlobalDataVault.TryGetLatestCreated()` is bootstrap/editor/diagnostic/crash

  only unless a core fallback is explicitly documented. Domain runtime fallback

  to "latest vault" is rejected.

- Burst/Jobs are accepted only for amortized, data-local batches with

  dispatcher-owned completion windows. Same-frame schedule/readback loops and

  hidden `.Complete()` calls are rejected without profiler proof.

- Data Monolith readiness requires the active

  `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` payload and

  import/bake/boot validation. Source presence is not payload readiness.

- `GlobalQualityWeight` is continuous. It may change fidelity, cadence,

  capacity, and optional telemetry; it must not change gameplay truth ownership,

  DTO layout, save identity, or authority route.

For implementation order, use `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`. For

merge/review disposition, use `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`.

## Runtime Government

| Instrument | Government Role | It Must Not Become |

|---|---|---|

| `GlobalRegistry` | cold service directory, bootstrap ownership, DI source | live state bus, gameplay query loop, concrete class warehouse |

| `SystemDispatcher` | phase owner, tick budget owner, completion-window owner | random work scheduler, hidden `.Complete()` sink |

| `SignalBus<T>` | typed first-party fan-out, dirty-state snapshots | request/response layer, catch-all enum bus |

| `GlobalSignals` direct queues | legacy bridge lanes during migration | default new traffic path |

| `HectonEventBus` | mod/API/cold event isolation with watchdogs | hot gameplay/event spine |

| `GlobalDataVault` | cross-domain native ownership, generation handles, relocation | mutable global heap, H-Phi padding |

| `NativeMemorySentinel` | allocation registry and leak evidence | optional debug ornament |

| Black-box rings | last-frame state proof and crash reconstruction | unbounded log spam |

| H-Phi | static architecture pressure metric | product readiness score |

## Lifecycle

Use this order for every non-trivial runtime system:

1. **Declare Ownership**

   - domain owner

   - public interfaces

   - signal lanes produced/consumed

   - DataVault buffers requested

   - native allocations owned

   - telemetry ring id

2. **Register Cold**

   - expose owned interfaces through bootstrap/registry only

   - create signal lanes and DataVault buffer requests

   - no reads from other domains

3. **Inject Dependencies**

   - cache external interfaces and snapshots in fields

   - resolve Vault handles once

   - subscribe to ready/changed/shutdown signals

   - fail fast if required dependencies are absent

4. **Execute By Phase**

   - `PRE_SIMULATION`: input, command intake, previous-frame snapshots

   - `SIMULATION`: gameplay truth, jobs, physics/AI/world updates

   - `POST_SIMULATION`: swaps, publishes, stable read models

   - `VISUAL_SYNC`: presentation, audio/visual/haptic overkill consumers

5. **Publish Changes**

   - broadcast state changes through typed `SignalBus<T>`

   - coalesce/drop/fail-fast deterministically on overflow

   - never use broadcast for one private caller

6. **Persist Or Share Data**

   - local scratch stays local

   - cross-domain/job/scene/save/replay/crash state uses DataVault handles

   - every buffer has `BufferID`, `SystemID`, generation, disposal, stale-handle

     behavior, and black-box fault fields

7. **Prove Runtime**

   - static scan is not enough

   - compile is not enough

   - Unity Console, Play Mode, profiler, GC, Memory Profiler, player build, and

     stress telemetry are required for readiness claims

## Decision Table

| Need | Correct Route | Rejected Route |

|---|---|---|

| One owner, one immediate query | cached owner interface | new signal lane |

| One owner, many first-party listeners | `SignalBus<T>` | registry polling |

| Mod-facing event or extension API | `HectonEventBus` | direct gameplay bus |

| Cross-domain persistent native data | `GlobalDataVault` handle | raw `NativeArray<T>` sharing |

| Local single-owner temporary data | owner-local native collection | fake Vault buffer |

| Runtime config changes | typed dirty signal + cached snapshot | per-frame registry lookup |

| Bootstrap service identity | `GlobalRegistry` slot | scene search or singleton `Awake` |

| Telemetry/cold diagnostic event | black-box ring or cold event | hot managed callback |

| Visual-only response | `VISUAL_SYNC` consumer | gameplay truth mutation |

| H-Phi improvement | debt reduction with proof | adding references to score better |

## Global Authority Route Card

Any new global authority route must have this in source rationale, task status, or

review notes. Use `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md` for the full

copy/paste template and instrument-specific minimums.

```text

Route:

Owner:

Domain:

Instrument: GlobalRegistry | SignalBus<T> | GlobalSignals bridge | HectonEventBus | GlobalDataVault

Producer phase:

Consumer phase:

Cadence:

Payload/data shape:

Capacity:

Overflow/failure:

Telemetry/black-box fields:

Shutdown/disposal:

Why owner-local data is insufficient:

Why this does not increase global monolith risk:

Proof required before GREEN:

```

If the card is blank, the route is rejected.

## Instrument Setup

### GlobalRegistry

AAA-grade setup:

- bootstrap registers stable interfaces only

- consumers receive dependencies through injection or explicit cold bind

- hot paths use cached fields

- live changes use typed ready/changed/shutdown signals

- registry additions require deletion, migration, or written exception

Bad setup:

- every domain gets a slot

- helper methods hide registry reads

- settings and state are polled every frame

- registry rebound events become gameplay traffic

### SignalBus<T>

AAA-grade setup:

- lane per real broadcast contract

- unmanaged payload

- explicit owner

- producer/consumer phase declared

- max events per frame declared

- overflow policy declared

- snapshot read surface

- duplicate-name scan

- finite-value sanitization where floats exist

- telemetry counters for pushed, dropped, coalesced, overflowed

Bad setup:

- one catch-all signal

- signal for one caller

- Unity object payloads

- strings or managed arrays

- two lanes owning the same truth

### GlobalDataVault

AAA-grade setup:

- only for cross-domain/job/scene/save/replay/crash/relocation data

- owner remains responsible for writes and fences

- readers get handles or read-only snapshots

- generation and stale-handle behavior are explicit

- scene unload returns registered native memory to baseline

- crash dump can name buffer id, system id, generation, capacity, and owner

Bad setup:

- moving every local buffer into Vault

- using Vault as a mutable global heap

- adding BufferIDs for absent systems

- claiming data sovereignty without lifecycle proof

### HectonEventBus

AAA-grade setup:

- mod API

- cold/meta/progression hooks

- watchdog-protected managed callback boundary

- no first-party hot gameplay traffic

Bad setup:

- used because defining a typed SignalBus lane is work

- used inside Tick/FixedTick/LateUpdate/UI refresh/audio/physics/render upload

- reported as zero-GC signal hygiene

### H-Phi

AAA-grade setup:

- run as static pressure radar

- compare only same formula family

- use deltas to select review targets

- never accept readiness from score alone

Bad setup:

- treating H-Phi growth as product quality

- adding global references to move counters

- hiding direct coupling behind another singleton name

## Quality Scaling

No binary low/ultra switch. Every scalable system consumes continuous

`GlobalQualityWeight`:

| Tier Band | Authority Behavior |

|---|---|

| `0.0 - 0.25` | smallest snapshots, coalesced signals, cheap visual responses, no optional telemetry consumers in hot lanes |

| `0.25 - 0.55` | normal gameplay truth, limited presentation consumers, bounded diagnostics |

| `0.55 - 0.85` | richer VISUAL_SYNC consumers and telemetry, still same gameplay truth route |

| `0.85 - 1.0` | visual overkill consumers allowed only after gameplay cost is flat |

Quality may increase presentation detail. It must not change authority ownership.

## Integration Review

Before merge, reviewers check:

- no new hot registry lookup

- no first-party hot `HectonEventBus`

- no new direct `GlobalSignals.Publish` unless bridge migration

- no signal payload with managed/Unity object fields

- no DataVault buffer without owner/proof fields

- no native collection cross-domain exposure without handle/snapshot contract

- no `.Complete()` outside owned completion window

- no H-Phi claim without evidence class

- black-box telemetry can explain last-frame owner state on failure

## Migration Strategy

Do not rewrite the project around the model. That is how teams die in refactor

loops.

Do this instead:

1. Freeze new global surface.

2. Add route cards for new work only.

3. Classify existing `HectonEventBus` and direct `GlobalSignals` traffic.

4. Migrate hot first-party traffic first.

5. Move only cross-domain native state into DataVault.

6. Convert hot registry polls to injected cached interfaces.

7. Add proof gates after each slice.

8. Rerun H-Phi only after actual surface reduction.

## Acceptance

This operating model is not accepted as healthy until the project has:

- current Unity Console clean import

- Play Mode bootstrap -> world -> shutdown smoke

- profiler capture with registry/signal/vault markers under stress

- GC proof for signal/registry/vault hot paths

- NativeMemorySentinel baseline return after unload

- DataVault stale-handle/generation/relocation tests

- black-box dump produced and read back from one controlled fault

- H-Phi rerun with no anti-gaming violation

Until then, status remains `PENDING VERIFICATION`.
