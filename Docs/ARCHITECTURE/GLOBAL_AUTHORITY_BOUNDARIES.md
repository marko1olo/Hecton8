# Global Authority Boundaries

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE

This document defines the allowed routes for global authority surfaces.

## Surfaces

| Surface | Allowed use | Rejected use |
|---|---|---|
| `GlobalRegistry` | cold bootstrap identity, service registration, dependency injection, stable owner lookup | hot polling, mutable gameplay state, event bus, scene search replacement |
| `SignalBus<T>` | first-party hot broadcast with unmanaged payloads, bounded capacity, deterministic overflow, telemetry | request/response, one-private-caller events, managed payloads, unbounded queues |
| `GlobalSignals` direct queues | legacy bridge lanes and low-level queue infrastructure during migration | new gameplay traffic, catch-all queue expansion, undocumented lane growth |
| `HectonEventBus` | mod/API/cold managed isolation and watchdog-protected extension events | first-party hot gameplay traffic, Burst/job data flow |
| `GlobalDataVault` / `IDataVault` | cross-domain native ownership, generation-checked handles, persistent shared snapshots, relocation/defrag ownership | global heap, private scratch replacement, unowned persistent allocations |

## Current Static Source Counters

| Counter | Value |
|---|---:|
| `SignalBusRegistry` capacity | 256 |
| direct `FlushDirectSignalLane<>` invocations in `GlobalSignals.cs` | 136 |
| direct `NativeQueue<T>` fields in `GlobalSignals.cs` | 74 |
| mod/API public signal-denial count | 160 |

The `160` value is not the total active signal count. It is a mod/API boundary fact.

## Route Rules

1. Pick one route before coding: cold lookup, hot broadcast, persistent shared memory, mod event, telemetry, or debug.
2. Cache registry-resolved interfaces outside hot paths.
3. Publish runtime facts once from the owning phase.
4. Consumers read immutable snapshots, generation-checked handles, or typed signal payloads.
5. Read accessors named `Get*`, `TryGet*`, `Resolve*`, or `Read*` must be pure.
6. Read accessors must not publish, allocate, grow buffers, search scenes, complete jobs, or mutate global state.
7. New signal lanes require owner, producer phase, consumer phase, capacity, overflow policy, retention policy, payload layout, duplicate-name scan, and telemetry route.
8. New DataVault buffers require `BufferID`, `SystemID`, length/capacity, generation handling, release behavior, stale-handle behavior, and dump behavior.

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

Static source scan on 2026-05-21 found six interface-array declarations and sixty files containing both `GlobalRegistry` and frame-loop method names. Those are triage hits, not proof of hot-path misuse; each owner must review method scope before claiming compliance.

## Required Playbook

Use these files before changing global routes:

- `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`

## Non-Claims

This document does not prove runtime lane wiring, scene setup, overflow behavior, job safety, GC state, or profiler cost.
