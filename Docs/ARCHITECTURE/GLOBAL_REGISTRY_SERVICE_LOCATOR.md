# Global Registry Service Locator

Date: 2026-05-12

Owner domain: core/global registry dependency injection

Status: STATIC_SOURCE REVIEWED / RUNTIME PENDING

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not runtime global-authority health, dependency-order correctness, profiler, or player-build proof.

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`

- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

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

Services must register through `GlobalRegistry`. A service slot is cold identity/injection only; the owner remains the domain runtime system that owns the fact, lifecycle, telemetry, and shutdown.

Allowed:

```text

owner bootstrap/OnRegister/Initialize -> GlobalRegistry.RegisterX(this)

consumer bootstrap/dependency injection -> cache injected interface

hot path -> use cached interface/snapshot/handle, never poll GlobalRegistry

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

- `Get*`, `TryGet*`, `Resolve*`, and `Read*` accessors that touch registry-owned

  services are read-only. They must not publish, sync scene state, allocate or

  grow buffers, complete jobs, mutate global authority, or run scene searches.

- Runtime context owners publish once from their own dispatcher phase. Consumers

  read immutable snapshots or cached owner interfaces instead of pulling sync

  work through a getter.

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

- Hiding signal publish, scene sync, DataVault fallback, native growth, or job

  completion inside a method whose name implies a read.

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

A local private cache is not a singleton. Cross-domain global `Instance` is a registry violation unless the class is pure static math/helper without runtime state.

## Math LOD Bridge

`HomeostasisBrain` / the scalability owner publishes the continuous `GlobalQualityWeight` and presentation math-LOD state. `GlobalRegistry` is cold identity/injection only and must not own, mutate, or hot-publish quality state:

- `_MATH_LOD_LOW`

- `_MATH_LOD_HIGH`

- `_H8MathLodLowBlend`

- 60-frame precision transition

- integer blend scale of 1000

This is presentation/scalability state, not gameplay determinism state.

Consumers cache the scalability owner or read published snapshots; hot loops do not query registry slots for quality state.

## Hashing

`CalculateActiveServiceTypeFnv1a()` exists for active service-set hashing. Use it for diagnostics and replay comparisons, not for gameplay decisions.

## Shutdown

Registry-owned services must shut down in reverse slot order. That protects downstream services from reading already-disposed dependencies.

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING
