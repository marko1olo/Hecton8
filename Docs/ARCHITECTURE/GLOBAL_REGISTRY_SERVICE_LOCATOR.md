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
