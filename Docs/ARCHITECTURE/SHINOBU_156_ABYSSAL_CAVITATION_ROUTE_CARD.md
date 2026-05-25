# SHINOBU_156 Abyssal Cavitation Route Card

Status: HISTORICAL STATIC_SOURCE ORIENTATION / SUPERSEDED FOR LIVE SHOCKWAVE NAN ROUTE BY SHINOBU_248 / UNITY IMPORT + PROFILER PROOF PENDING.

## 2026-05-21 SHINOBU_248 Supersession Note

- `SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD` is retained as historical route context for the original cavitation buffer range `71560..71570`.
- The live authority proof for the shockwave NaN guard, exact-overlap deterministic direction fallback, 32-byte force transport row `71571`, fault dump hardening, and shader cavitation link is `Docs/ARCHITECTURE/SHINOBU_248_SHOCKWAVE_NAN_ROUTE_CARD.md`.
- Integrators must treat SHINOBU_248 as current live route delta owner.
- Do not treat SHINOBU_156 and SHINOBU_248 as concurrent owners of authority surface `71560..71571`.

## R48 Exact Route Field Normalization

Route ID: SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD

Owner: SHINOBU_156 / Abyssal cavitation and shockwave physics.

Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.

Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.

Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.

Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.

Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.

Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.

Review disposition: YELLOW / STATIC_SOURCE_ONLY.

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

| Overflow/failure | Bounded buffers drop/coarsen excess non-critical work by `GlobalQualityWeight`; non-finite shockwave math or stale SDF input sets telemetry flags and requests generated-on-fault dump. |

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

- Primary managed bridge: `AbyssalCavitationRuntime.FlushForcesToPhysics(double3, ...)`.
- It drains `ShockwaveForcePacketDTO` rows through `PhysicsApplySystem.DrainCavitationForcePackets`.
- It resolves `TargetEntityHash` and queues deferred point-force packets.
- Legacy `Rigidbody[]` overload is bounded compatibility only.

- Cavitation visuals are not particles.
- Buffer `71564` uploads to `_H8CavitationShockwaves` and is consumed by `Hecton8_UberNoir.hlsl` water refraction.
- Visual sync records zero-wave frames as real empty-buffer state.
- Duplicate same-frame GraphicsBuffer uploads are skipped when frame index, upload count, quality weight, and visual intensity are unchanged.

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

`GlobalQualityWeight` continuously drives entity stride, critical-entity bypass, visual upload count, and shader slot count.

Weak devices keep critical bodies/coarse distortion. Strong devices consume more candidate rows/refraction shells without changing authoritative shockwave contract.

Ordnance:

`ordnance_specs.csv` hydrates `OrdnanceProfileDTO[32]` as a fixed open-address FNV-1a table in Vault buffer `71566`. This preserves hash-map lookup behavior without introducing private persistent `NativeHashMap` ownership.

Pressure:

Pressure distance path:

- Subtract entity AUP from wave epicenter AUP.
- Cast only the local delta to `float3`.
- Attenuation: `PeakPressure * rcp(max(1, distanceSq))`.
- Multiply by expanding shell gate and SDF dampening.
- Shell gate ties force application to the current mathematical sphere radius.
- `MaxRadius` expires propagation.

SDF:

- The solver samples SDF at the midpoint between wave epicenter and entity AUP.
- Negative SDF applies continuous pressure dampening.
- At low `GlobalQualityWeight`, SDF lookup collapses to one nearest signed-distance byte; above the quality curve threshold it blends to trilinear sampling.
- If no producer has written the owner-local SDF snapshot, a deterministic mock seabed/pillar SDF keeps CI and editor tests isolated.

Forbidden route:

`Physics.OverlapSphere`, `Physics.OverlapSphereNonAlloc`, `Rigidbody.AddExplosionForce`, explosion prefab instantiation, and particle-system fireballs are not part of this route.
