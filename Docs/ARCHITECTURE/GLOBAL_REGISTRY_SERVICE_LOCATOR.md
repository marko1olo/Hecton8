# Global Registry Service Locator

Date: 2026-05-12
Status: STATIC_SOURCE REVIEWED / RUNTIME PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R31 Current Boundary Note

R31 reread confirmed this file remains static source orientation for the service-locator boundary, not proof that global authority is healthy at runtime. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R31_ARCHITECTURE_CURRENT_BOUNDARY_PROPAGATION_LOCAL.md`; R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `57` RealtimeCSG vendor references; `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owner Source: `Assets/_Project/Scripts/Core/GlobalRegistry.cs`

## What It Is

`GlobalRegistry` is the static service locator and dense bucket registry for first-party runtime systems.

It is allowed because it centralizes startup order, service ownership, event listener buckets, and shutdown order. It is not permission to create per-class singletons.

## Registry Phase

`RegistryPhase` states:

| Phase | Meaning |
|---|---|
| `Uninitialized` | registry is not ready |
| `Registering` | bootstrap is wiring services |
| `Ready` | runtime service lookups are legal |

## Dense Buckets

Registry-owned buckets exist for high-frequency surfaces such as:

- `IUpdatable`
- `IFixedTickable`
- `ISlowTickable`
- `IFrostTickable`
- `IRenderable`
- registry event listeners
- hot-swap listeners

Dense buckets keep iteration predictable and avoid scene search in the frame lane.

## Service Slot Law

Services must register through `GlobalRegistry`. A service slot is the ownership boundary.

Allowed:

```text
owner Awake/Initialize -> GlobalRegistry.RegisterX(this)
consumer -> GlobalRegistry.X
shutdown -> GlobalRegistry.UnregisterX(this) or registry reverse shutdown
```

Forbidden:

```text
public static Foo Instance
FindObjectOfType<Foo>()
DontDestroyOnLoad self-sovereign service
cross-domain direct serialized field as authority
```

## 2026-05-19 Anti-Monolith Boundary

`GlobalRegistry` is allowed only as the cold authority spine. It is not the
project brain.

Rules:

- Runtime systems cache service dependencies during bootstrap, `OnEnable`, or
  explicit dependency injection.
- Hot paths consume cached fields, cached snapshots, DataVault handles, or signal
  snapshots.
- A new registry service slot requires an existing owner, a shutdown path, and a
  reason it cannot remain local to its domain.
- Registry growth must reduce concrete coupling. Adding a concrete leaf-domain
  type to Core is architectural debt unless an interface boundary is impossible.
- Service changes after bootstrap use a typed ready/changed/shutdown signal so
  consumers refresh cached fields without polling registry slots.

Forbidden:

- Treating `GlobalRegistry` as a live settings bus.
- Reading `GlobalRegistry.*` every `Tick`, `FixedTick`, UI refresh, physics solve,
  AI solve, render upload, or logistics pass.
- Adding slots for future/absent services.
- Hiding registry polls inside `Resolve*`, `Refresh*`, `Prepare*`, `Try*`, or
  helper methods called by hot paths.

See `GLOBAL_AUTHORITY_BOUNDARIES.md` for the cross-surface decision table.

## Singleton Terminal Offense

Singletons are a terminal offense when they own cross-domain state because they bypass:

- bootstrap order
- dependency cycle detection
- service rebinding
- ghost-service telemetry
- shutdown ordering
- test reset
- no-domain-leak enforcement

A local private cache is not a singleton. A global `Instance` that other domains call is a registry violation unless the class is a pure static math/helper with no runtime state.

## Math LOD Bridge

`GlobalRegistry` owns global math precision transition state:

- `_MATH_LOD_LOW`
- `_MATH_LOD_HIGH`
- `_H8MathLodLowBlend`
- 60-frame precision transition
- integer blend scale of 1000

This is presentation/scalability state, not gameplay determinism state.

## Hashing

`CalculateActiveServiceTypeFnv1a()` exists for active service-set hashing. Use it for diagnostics and replay comparisons, not for gameplay decisions.

## Shutdown

Registry-owned services must shut down in reverse slot order. That protects downstream services from reading already-disposed dependencies.

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING
