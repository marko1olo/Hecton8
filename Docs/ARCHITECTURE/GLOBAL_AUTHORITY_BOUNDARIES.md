# Global Authority Boundaries

Date: 2026-05-26
Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE

Scope: allowed routes for global authority surfaces. This is a stable contract, not a loop log,
compile diary, or runtime proof artifact.

## Surfaces

| Surface | Allowed use | Rejected use |
|---|---|---|
| `GlobalRegistry` | cold bootstrap identity, service registration, dependency injection, stable owner lookup | hot polling, mutable gameplay state, event bus, scene search replacement |
| `SignalBus<T>` | first-party hot broadcast with unmanaged payloads, bounded capacity, deterministic overflow, telemetry | request/response, one-private-caller events, managed payloads, unbounded queues |
| `GlobalSignals` direct queues | legacy bridge lanes and low-level queue infrastructure during migration | new gameplay traffic, catch-all queue expansion, undocumented lane growth |
| `HectonEventBus` | mod/API/cold managed isolation and watchdog-protected extension events | first-party hot gameplay traffic, Burst/job data flow |
| `GlobalDataVault` / `IDataVault` | cross-domain native ownership, generation-checked handles, persistent shared snapshots, relocation/defrag ownership | global heap, private scratch replacement, unowned persistent allocations |

## Static Source Counters

These counters are static orientation only. They are not runtime proof, allocation proof, or profiler
proof.

| Counter | Value |
|---|---:|
| `SignalBusRegistry.LaneCapacity` | 512 |
| `ClearPostSimulation` hits under `Core/Signals` | 141 |
| `NativeQueue<T>` hits in `Core/Signals/GlobalSignals*.cs` | 35 |
| mod/API public signal-denial count | 160 |

The `160` value is a mod/API boundary fact, not total active signal count.

## Route Rules

1. Pick one route before coding: cold lookup, hot broadcast, persistent shared memory, mod event, telemetry, or debug.
2. Cache registry-resolved interfaces outside hot paths.
3. Publish runtime facts once from the owning phase.
4. Consumers read immutable snapshots, generation-checked handles, or typed signal payloads.
5. Read accessors named `Get*`, `TryGet*`, `Resolve*`, or `Read*` must be pure.
6. Read accessors must not publish, allocate, grow buffers, search scenes, complete jobs, or mutate global state.
7. New signal lanes require owner, producer phase, consumer phase, capacity, overflow policy, retention policy, payload layout, duplicate-name scan, and telemetry route.
8. New DataVault buffers require `BufferID`, `SystemID`, length/capacity, generation handling, release behavior, stale-handle behavior, and dump behavior.
9. `GlobalQualityWeight` is continuous. It may scale fidelity, cadence, capacity, and optional telemetry; it must not change gameplay truth ownership, DTO layout, save identity, or authority route.

## Compile-Wall Guards

Runtime domain assemblies must not reference sibling runtime assemblies for gameplay data flow. Use one of these routes:

- a contract interface in `Hecton8.*.Contracts`;
- a typed unmanaged `SignalBus<T>` payload;
- a generation-checked `GlobalDataVault` handle;
- a cold `GlobalRegistry` lookup cached during boot.

Rejected:

- arrays of interfaces in hot paths;
- registry lookup inside `Update`, `FixedUpdate`, job execution, culling, or solver loops;
- hidden same-frame `JobHandle.Complete()` readbacks;
- read accessors that allocate, publish, search scenes, sync transforms, grow native buffers, or mutate global state.

Static source scans are triage only. A text hit is not proof of hot-path misuse, and a clean grep is not
runtime proof.

## Required Playbook

Use these files before changing global routes:

- `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`

## Non-Claims

This file does not claim:

- current compile health;
- Unity import status;
- Play Mode status;
- runtime lane wiring;
- scene setup correctness;
- overflow behavior under stress;
- job safety;
- GC state;
- profiler cost.

Those claims require fresh proof artifacts outside this contract.
