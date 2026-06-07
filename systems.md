# HECTON-8 Runtime Systems Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: runtime architecture, execution phases, ownership, event routes, data access, hot-path discipline, and proof expectations.

## First-20 Route Hook

- First-20 moment: boot -> world load -> swim/orient -> interact -> hazard -> save/load must run through phase-owned systems, not scene search or presentation-side truth.
- Route blocker removed: ambiguous owners, hot registry polling, hidden job completion, and unmanaged signal routes that can corrupt opening-route state.
- Proof class: `STATIC_DOC` until route owner packets, Unity Console, Play Mode/player run, profiler, GC, save/load, and black-box artifacts exist.

## Prime Law

One fact has one owner, one route, and one proof artifact. HECTON-8 rejects systems that work only because they search the scene, poll registries, mutate global state from read accessors, complete jobs at convenient times, or smuggle gameplay truth through presentation code.

Runtime systems exist to preserve predictable state under pressure. Visual overkill is bought after ownership and phase discipline are proven.

## Execution Phases

Every runtime unit of work belongs to exactly one dispatcher phase:

- `PRE_SIMULATION`: input snapshots, command queue drain, dependency validation, load-shed admission.
- `SIMULATION`: gameplay truth mutation, force packet generation, AI state changes, survival/economy/damage mutation.
- `POST_SIMULATION`: job completion windows, buffer swaps, typed signal publication, black-box telemetry writes.
- `VISUAL_SYNC`: renderer/audio/UI/haptic presentation from stable snapshots.

Forbidden:

- private `Update`, `LateUpdate`, `FixedUpdate`, or coroutine schedulers for gameplay truth;
- `Schedule()` followed by hidden same-method `Complete()` as fake parallelism;
- presentation code writing simulation truth;
- simulation systems polling presentation objects;
- new phase names without integrator approval.

## Ownership Record

Every accepted system must document:

- owner assembly;
- dispatcher phase;
- data buffers read;
- data buffers written;
- SignalBus lanes consumed;
- SignalBus lanes published;
- GlobalRegistry dependencies resolved at cold setup;
- MX350/i3 budget target or estimate in microseconds, with measured values only when a profiler artifact exists;
- load-shed behavior for low, middle, high, and ultra quality lanes;
- black-box telemetry fields.

If the ownership record is missing, the system is not accepted.

## Data Access Law

Read accessors named `Get*`, `TryGet*`, `Resolve*`, or `Read*` must be pure. They must not allocate, publish events, complete jobs, mutate state, search the scene, sync GameObjects, load assets, or grow buffers.

Hot-path consumers read immutable snapshots, stable handles, NativeArray-backed pages, or typed signal payloads. `GlobalRegistry` is cold dependency injection and identity routing only. It is not a hot polling surface.

## Signal Routing

Hot broadcast uses `SignalBus<T>` or first-party native queues. Managed event buses are reserved for cold tooling, mod/API isolation, editor diagnostics, or non-hot gameplay boundaries.

Signal payloads must be small, finite, versioned, and owned. Do not ship object references through hot signals unless the referenced object is a stable cold identity.

## Quality Scaling

`GlobalQualityWeight` scales cadence, capacity, snapshot density, visual sync richness, telemetry verbosity, and optional presentation work. It must not change authority, save identity, DTO layout, deterministic state transitions, or route ownership.

Low tier uses fewer optional jobs and cheaper visual sync. Middle keeps full gameplay truth with conservative presentation. High increases snapshot density only after budget proof. Ultra spends saved budget in `VISUAL_SYNC`, not in unbounded simulation.

## Proof Artifacts

Runtime system work must provide:

- owner assembly and domain;
- dispatcher phase;
- read/write buffer list;
- SignalBus lanes consumed and published;
- GlobalRegistry dependencies resolved at cold setup;
- hot-path allocation proof;
- job scheduling and completion window proof where jobs exist;
- profiler marker names and budget;
- `GlobalQualityWeight` scaling behavior;
- black-box fields routed through `telemetry.md`;
- failure mode, fallback, and shutdown/disposal route.

Without this ownership packet, the system remains `PENDING VERIFICATION`.

## Rejection Gates

Reject any runtime system that:

- searches the scene in a hot path;
- allocates per frame;
- uses a read accessor to mutate;
- hides job completion outside owner windows;
- has no black-box ring;
- has no phase assignment;
- publishes gameplay truth from UI, VFX, audio, camera, or animation presentation;
- uses binary quality switches instead of continuous scaling;
- reports success without profiler, GC, or static proof when runtime code changed;
- responds to a repeated compile/runtime/ownership failure by adding wrapper glue, fallback managers, duplicate registries, or another checker instead of fixing the real owner route, replacing the route, or reverting the agent's broken chunk.

## Acceptance Sentence

A system is accepted only when ownership is explicit, phase timing is deterministic, data routes are pure, hot paths are allocation-free, proof exists, and the player-facing result makes HECTON-8 feel more physical, readable, and hostile.
