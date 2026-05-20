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

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not runtime global-authority health, dependency-order correctness, profiler, or player-build proof.

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) (R45 prior R43/R44 residue/proof-artifact/source-counter correction) keeps this file as static source orientation for the service-locator boundary, not proof that global authority is healthy at runtime. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`; R45 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

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


