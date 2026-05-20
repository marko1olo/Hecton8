# SHINOBU_156 Abyssal Cavitation Route Card

Status: STATIC_SOURCE ORIENTATION / UNITY IMPORT + PROFILER PROOF PENDING.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner: SHINOBU_156 / Abyssal cavitation and shockwave physics.

## Global Authority Route Card

Evidence class: STATIC_SOURCE route-card completion only. This block does not prove Unity import, Burst compile, Play Mode, profiler, GCMonitor, player build, or physics correctness.

| Field | Value |
|---|---|
| Route ID | `SHINOBU_156_ABYSSAL_CAVITATION` |
| Owner | SHINOBU_156 / Abyssal cavitation runtime |
| Instrument | GlobalDataVault shockwave/candidate/SDF/force buffers `71560..71570`, PhysicsApplySystem managed bridge, shader upload payload, and black-box dump route |
| First-20-minutes route moment | Hazard pressure and survival feedback only when the Copper Wire route needs abyssal shock feedback; otherwise parked |
| Authority surface | GlobalDataVault buffers `71560`-`71570`; managed bridge drains force packets into `PhysicsApplySystem` |
| Producer phase | `SIMULATION` for shockwave expansion and candidate force evaluation |
| Consumer phase | `POST_SIMULATION` for force-packet publication, `VISUAL_SYNC` for shockwave sphere upload |
| Cadence | Fixed simulation cadence for force truth; visual upload only when frame/upload/quality signature changes |
| Capacity | `ShockwaveEventDTO[128]`, candidate/entity snapshots from caller-fed buffers, telemetry ring `300` |
| Overflow/failure | Deterministic bounded buffers drop or coarsen excess non-critical visual/candidate work by `GlobalQualityWeight`; non-finite shockwave math or stale SDF input sets telemetry fault flags and requests `Docs/AgentLogs/Dump_SHINOBU_156.bin` as a generated-on-fault artifact |
| Overflow policy | Deterministic bounded buffers; excess non-critical visual/candidate work is dropped or coarsened by `GlobalQualityWeight`; critical bodies keep bypass priority |
| Failure mode | Non-finite shockwave math or stale SDF input sets telemetry fault flags and requests `Docs/AgentLogs/Dump_SHINOBU_156.bin` as a generated-on-fault artifact |
| Shutdown/disposal | Release/clear Vault buffer ownership from the cavitation owner; do not let physics or visual consumers own teardown |
| Fault dump target | `Docs/AgentLogs/Dump_SHINOBU_156.bin` is planned/generated on fault; no existing artifact is implied unless a timestamped runtime trigger and output are linked |
| Review disposition | `YELLOW / STATIC_SOURCE_ONLY` until route-card review, guarded compile, Unity import, profiler, and player-build artifacts exist |
| Proof required before GREEN | Artifact path, command/tool, timestamp, environment, output tuple for compile/import/runtime/profiler claims |

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not Unity import, physics runtime, profiler, shader visual proof, or player-build proof.

- `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs`
- `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationContracts.cs`
- `Assets/_Project/Scripts/PhysicsApplySystem.cs`
- `Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl`

## Runtime Contract Notes

Authority:

- Shockwave truth lives in `GlobalDataVault` buffer `71560` as `ShockwaveEventDTO[128]`.
- Entity candidates are caller-fed AUP snapshots in buffer `71562`; SHINOBU_156 does not own the spatial hash or Rigidbody registry.
- SDF occlusion is a producer-fed owner-local snapshot: descriptor `71569` plus signed-distance voxels `71570`; no direct `Hecton8.World` runtime reference is required.
- Burst pressure evaluation writes `ShockwaveForcePacketDTO` rows to buffer `71563`.
- The primary managed bridge `AbyssalCavitationRuntime.FlushForcesToPhysics(double3, ...)` drains `ShockwaveForcePacketDTO` rows through `PhysicsApplySystem.DrainCavitationForcePackets`, resolving `TargetEntityHash` to the tracked body and queuing deferred point-force packets. The legacy `Rigidbody[]` overload remains bounded compatibility only; no Burst job mutates Rigidbody state.
- Cavitation visuals are not particles. Buffer `71564` is uploaded to `_H8CavitationShockwaves` and consumed by `Hecton8_UberNoir.hlsl` water refraction. Visual sync records zero-wave frames as real empty-buffer state and skips duplicate same-frame GraphicsBuffer uploads when frame index, upload count, quality weight, and visual intensity are unchanged.
- Fault telemetry requests the 300-frame ring dump at `Docs/AgentLogs/Dump_SHINOBU_156.bin` as a planned/generated-on-fault artifact. No existing artifact is implied unless a timestamped runtime trigger and output are linked.

Vault IDs:

- `71560` ShockwaveEvents
- `71561` ShockwaveCounters
- `71562` EntitySnapshots
- `71563` ForcePackets
- `71564` VisualSpheres
- `71565` TelemetryRing
- `71566` OrdnanceProfiles fixed open-address table
- `71567` CsvScratch
- `71568` Tuning
- `71569` SdfDescriptor
- `71570` SdfVoxels

DTO proof:

- `ShockwaveEventDTO` is explicit 64 bytes: `double3 EpicenterAUP` at 0, `CurrentRadius` 24, `MaxRadius` 28, `PeakPressure` 32, `ExpansionSpeed` 36, `SourceHashID` 40, padding 44-63.
- Hot DTOs expose raw public fields only.
- `ShockwaveCounterBlock` is explicit 64 bytes to avoid false sharing between counter writes.
- `AbyssalCavitationSdfVolumeDTO` is explicit 64 bytes: `OriginAUP` 0, `CellSizeMeters` 24, `Dimensions` 36, `DecodeRangeMeters` 48, `Version` 52, `Flags` 56, padding 60-63.

Scalability:

`GlobalQualityWeight` drives entity stride, critical-entity bypass, visual upload count, and shader slot count continuously. Weak devices keep critical bodies and coarse shader distortion. Strong devices consume more candidate rows and more refraction shells without changing the authoritative shockwave contract.

Ordnance:

`ordnance_specs.csv` hydrates `OrdnanceProfileDTO[32]` as a fixed open-address FNV-1a table in Vault buffer `71566`. This preserves hash-map lookup behavior without introducing private persistent `NativeHashMap` ownership.

Pressure:

Distance math subtracts entity AUP from wave epicenter AUP first, then casts only that local delta to `float3`. Pressure attenuation is literal inverse-square: `PeakPressure * rcp(max(1, distanceSq))`, multiplied by the expanding shell gate and SDF dampening. The shell gate keeps force application tied to the current mathematical sphere radius; `MaxRadius` still expires propagation.

SDF:

The solver samples SDF at the midpoint between wave epicenter and entity AUP. Negative SDF applies continuous pressure dampening. At low `GlobalQualityWeight`, SDF lookup collapses to one nearest signed-distance byte; above the quality curve threshold it blends to trilinear sampling. If no producer has written the owner-local SDF snapshot, a deterministic mock seabed/pillar SDF keeps CI and editor tests isolated.

Forbidden route:

`Physics.OverlapSphere`, `Physics.OverlapSphereNonAlloc`, `Rigidbody.AddExplosionForce`, explosion prefab instantiation, and particle-system fireballs are not part of this route.
