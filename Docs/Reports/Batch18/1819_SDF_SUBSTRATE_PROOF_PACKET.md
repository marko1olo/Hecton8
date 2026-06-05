# 1819 SDF Substrate Proof Packet

Agent ID: 1819
Mode: STATIC_SOURCE / STATIC_DATA only.
Scope: GPR, Foundation, Drone SDF substrate routes.

## Verdict

Current source contains real SDF lease/descriptor routes for GPR, foundation pylon snapping, and drone navigation/repulsion. That is STATIC_SOURCE_VERIFIED.

Current artifacts do not prove a live first-20 route publishes and consumes real substrate SDF. Real substrate consumption remains PENDING UNITY SLOT.

No Unity Editor, scene, prefab, runtime, profiler, Frame Debugger, Memory Profiler, player build, or capture proof was produced by this task.

## Evidence Boundary

- Static source can prove code routes, guards, fail-closed behavior, and required artifacts.
- Static source cannot prove that `VoxelSdfTexture3D` is populated in the active scene, that GPR/Foundation/Drone hit those paths during play, that output is visible/gameplay-relevant, or that frame/GC budgets pass.
- `Docs/Reports/Batch18/1805_AGENT_OUTPUT_TRIAGE_DASHBOARD.md:110-120` is accepted: lease routes exist, but runtime proof is absent.
- `Docs/Reports/Batch18/1813_STALE_BLOCKER_ERRATA_PACKET.md:87-97` is accepted: static evidence remains `STATIC VERIFIED`; use `PENDING UNITY SLOT` for runtime evidence.

## Route Matrix

CSV packet: `Docs/Reports/Batch18/1819_SDF_SUBSTRATE_ROUTE_MATRIX.csv`.

| Route | Static result | Runtime result |
| --- | --- | --- |
| Shared voxel SDF producer/lease | `HectonVoxelVolume` publishes encoded SDF descriptor/payload; `HectonVoxelEngine` exposes nearest lease. | PENDING UNITY SLOT |
| GPR | `GroundPenetratingRadarRuntime` acquires a nearest SDF lease, stages a bounded snapshot, and passes it into the radar job. | PENDING UNITY SLOT |
| Foundation | `FoundationPylonGpuBatch` requires a valid `WorldStreaming` `VoxelSdfTexture3D` descriptor and fails closed when missing. | PENDING UNITY SLOT |
| Drone | `DroneFleetManager` acquires a SDF lease, creates `DroneSdfGrid`, and fails closed before headless scheduling if unavailable. | PENDING UNITY SLOT |
| DataMonolith/static data | `static_data.h8bin` exists; no inspected SDF route key was found in binary strings or source-data text. | PENDING UNITY SLOT if static data is later made part of SDF ownership |

## Shared SDF Owner Route

Source owner:
- `Assets/_Project/Scripts/HectonVoxelVolume.cs:2097-2162` publishes compact encoded SDF snapshots after validating grid dimensions, density field, origin, cell size, and range.
- `Assets/_Project/Scripts/HectonVoxelVolume.cs:2291-2305` writes `VoxelSdfPayloadDescriptorDTO` with `BufferID.VoxelSdfTexture3D`, `OwnerSystemId = WorldStreaming`, and `FlagValid`.
- `Assets/_Project/Scripts/HectonVoxelVolume.cs:2679-2811` exposes guarded read leases and releases mutation guards.
- `Assets/_Project/Scripts/HectonVoxelEngine.cs:5770-5847` acquires nearest active published SDF payloads, validates finite payload metadata, tracks the read lease, returns `VoxelSonarSdfReadLease.FlagValid`, and releases failed leases.
- `Assets/_Project/Scripts/Core/Contracts/GroundRadarContracts.cs:34-115` defines the descriptor, read lease, read model, and lease model contract.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:1411-1416` exposes `VoxelSonarSdf` as a contract read model from the registered voxel engine.

Static truth: one intended runtime fact exists: `WorldStreaming` owns the encoded SDF payload and consumers borrow it through descriptor/lease guards.

Unproven truth: no current artifact proves this owner publishes a valid live payload in the active first-20 scene or that consumers receive nonzero real substrate data.

## GPR Route

Static source:
- `GroundPenetratingRadarRuntime.cs:895-934` stages SDF when a scan is due and passes `EncodedSdf`, grid dimensions, origin, cell size, and range into `GroundRadarRaymarchJob`.
- `GroundPenetratingRadarRuntime.cs:1152-1230` requires cached SDF read/lease models, calls `TryAcquireNearestSonarSdfReadLease`, validates expected length and snapshot capacity, copies the lease into the pending snapshot, and releases the lease in `finally`.
- `GroundPenetratingRadarRuntime.cs:1388-1408` shows `_fallbackFrameId` is only a frame-id fallback.
- `GroundPenetratingRadarRuntime.cs:1490-1522` resolves `HectonVoxelEngine` through `WorldRuntimeReferenceUtility.TryResolveVoxelEngine` and hotswaps the cached contract service.
- `GroundRadarJobs.cs:100-110` raymarches only when scan flag, writable ping storage, valid SDF, and ore arrays/count are all present.
- `GroundRadarJobs.cs:202-243` validates SDF metadata and decodes encoded SDF bytes into density.
- `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md:66-68` says GPR consumes voxel sonar SDF through `WorldRuntimeReferenceUtility.TryResolveVoxelEngine`, not hot rediscovery through `GlobalRegistry.VoxelSonarSdf`.

Static result: current source no longer supports the old broad claim that GPR uses a fake SDF substrate path. It uses a lease-backed SDF route.

Still pending: prove in Unity that a first-20 GPR scan has valid SDF dimensions/range, a nonzero lease generation/version, ore source data, and player-visible scan output driven by substrate/ore intersection.

## Foundation Route

Static source:
- `FoundationPylonGpuBatch.cs:22-24` defines `FOUNDATION SNAP FAILED: VOXEL SDF SUBSTRATE MISSING` and includes `SnapFailed_NoSubstrate` in the warning mask.
- `FoundationPylonGpuBatch.cs:42-44` includes `VoxelSdfPayloadDescriptor` and `VoxelSdfTexture3D` in the encoded SDF mutation guard mask.
- `FoundationPylonGpuBatch.cs:299-306` clears pending work and publishes no-substrate warning when `TryResolveEncodedVoxelSdf` fails.
- `FoundationPylonGpuBatch.cs:540-604` reads descriptor and SDF bytes from DataVault, rejects invalid flag, wrong buffer, stale generation, non-`WorldStreaming` owner, bad byte count, non-finite origin/cell/range, nonuniform cells, and writes `FoundationPylonFlags.RealVoxelSdf` only after validation.
- `FoundationPylonGpuBatch.cs:663-672` cold-caches generation handles for descriptor and byte buffer.
- `FoundationPylonGpuBatch.cs:846-865` emits `FoundationStructuralWarningSignal` and notification text on no substrate.
- `FoundationSnappingCalculatorJobs.cs:107-112` marks the job result `SnapFailed_NoSubstrate` when the real SDF payload is invalid.
- `FoundationSnappingCalculatorJobs.cs:138-184` samples SDF downward for pylon support and flags `RealVoxelSdf`/`OutOfSdfBounds`.
- `FoundationSnappingCalculatorJobs.cs:260-270` validates `RealVoxelSdf`, finite config, positive range/cell, resolved voxel count, and encoded buffer length.
- `FoundationSnappingCalculatorJobs.cs:333-425` samples the encoded byte SDF with nearest/trilinear blend and decodes byte range to signed distance.
- `FoundationSnappingCalculatorData.cs:1045-1048` uses continuous `GlobalQualityWeight` as SDF interpolation weight.
- `FoundationSnappingCalculatorData.cs:1114-1158` sanitizes invalid real SDF into `SnapFailed_NoSubstrate` instead of fabricating positive defaults.
- `FoundationSnappingCalculatorEditTests.cs:235-272` statically tests missing SDF fail-closed flags.

Static result: foundation is the strongest static proof route. Missing substrate is not accepted; it blocks pylon output and warns.

Still pending: prove in Unity that foundation placement sees valid `WorldStreaming` SDF, does not warn, emits real support geometry, and reacts correctly to substrate slope/holes/edge cases.

## Drone Route

Static source:
- `DroneFleetManager.cs:1703-1740` cold-caches runtime services and hotswaps `VoxelEngineRuntime` into `s_CachedVoxelSdfReadLeaseModel`.
- `DroneFleetManager.cs:3582-3585` fails closed before headless drone scheduling when SDF grid acquisition fails.
- `DroneFleetManager.cs:3711-3714` injects `DroneSdfGrid` and repulsion strength into `DroneCognitionJob`.
- `DroneFleetManager.cs:4226-4300` releases prior headless lease, calls `TryAcquireNearestSonarSdfReadLease`, requires a valid lease and `DroneSdfGrid.TryCreate`, and releases the acquired lease on failure.
- `DroneFleetManager.cs:4349-4360` releases held SDF read leases.
- `DroneFleetManager.cs:7886-7984` uses the same SDF grid route for debug route output and releases leases.
- `DroneFleetNavigationKernel.cs:1017-1077` defines `DroneSdfGrid`, validates encoded SDF length, finite origin/cell/range, and version.
- `DroneFleetNavigationKernel.cs:1101-1122` samples SDF repulsion normals.
- `DroneFleetNavigationKernel.cs:1779-1836` blocks A* neighbors and line clearance against `SdfGrid.IsBlockedForRadius`.
- `DroneCognitionJob.cs:367-370` carries the SDF grid and repulsion strength into the cognition job.
- `DroneCognitionJob.cs:674-679` applies inverse-square repulsion from SDF normal/distance.

Static result: drone source uses real SDF lease data when available and blocks scheduling if the headless SDF grid cannot be acquired.

Still pending: no scoped drone SDF route test or Unity artifact was found proving live drone repulsion, A* blockage, debug route flags, or fail-closed player impact in the current route.

## DataMonolith / Static Data

Observed:
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists, size `3292992`, timestamp `2026-06-04 04:57:59`.
- `rg -a --only-matching "VoxelSdfTexture3D|VoxelSdfPayloadDescriptor|GroundPenetratingRadar|FoundationPylon|DroneSdf|SdfRangeMeters|IVoxelSonarSdf" static_data.h8bin` returned no matches.
- Source-data text hits for `substrate` are narrative rows in `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`; they are not runtime SDF substrate route data.

Conclusion: DataMonolith is present, but this packet found no static source/data proof that DataMonolith owns or feeds the GPR/Foundation/Drone SDF substrate route. Current SDF ownership remains a runtime WorldStreaming/DataVault route unless a later DataMonolith bake/import packet proves otherwise.

## Fail-Closed Gameplay Impact

- GPR: no valid SDF or no ore source means raymarch additions do not execute; this may present as empty/no scan hits, not proven gameplay acceptance.
- Foundation: missing/invalid substrate clears uploaded pylon batch, flags `SnapFailed_NoSubstrate`, pushes `FoundationStructuralWarningSignal`, and emits a HUD warning. This is useful blocker evidence, not acceptance.
- Drone: missing/invalid SDF grid prevents headless scheduling or causes SDF-invalid path checks to block. This avoids fake navigation through substrate, but live behavior still needs Unity proof.

## Save / Load Implications

- SDF bytes and leases must not become save identity. The save truth should be terrain seed/deltas and owned construction/drone state; SDF payloads should regenerate/publish through the owner route after load.
- Foundation save/load proof must show saved module/build state rebinds to current SDF after reload and does not store stale pylon support samples as authority.
- Drone save/load proof must show saved drone tasks/routes do not persist stale `DroneSdfGrid.Version` or lease data, and re-acquire from the current voxel owner.
- GPR scan history/telemetry may persist separately, but new scans must use the current SDF owner state after load.

No save/load runtime artifact was produced.

## Hot Path / Performance Risks

- GPR copies leased SDF bytes into a pending snapshot on scan due. The source caps this through `GroundRadarConstants.SdfSnapshotByteCapacity = 64^3`, but profiler/GC proof is pending.
- Voxel SDF publication can encode up to `PublishedSonarMaxGridDimension = 129` cubed and has an async watchdog path. Source shows guarded publication, not measured cost.
- Foundation locks descriptor/SDF lanes and uploads pylon batches only after SDF validation. Missing SDF clears work and warns; successful path still needs GPU/profiler proof.
- Drone holds a SDF lease across headless job use and releases through explicit release paths. Live contention and frame cost remain unmeasured.
- This task added no code and no hot `GlobalRegistry` polling.

## Quality Scaling Consequences

Low: cadence, ray count, interpolation weight, A* budget, and debug detail may reduce, but SDF truth owner, DTO layout, save identity, and fail-closed behavior must not change.

Middle: limited but non-flat route proof is required: GPR returns substrate/ore pings, foundation supports follow real slope, drones avoid substrate without visible stalls.

High: increase raymarch/A* support and smoothing/interpolation; still consume the same WorldStreaming SDF payload.

Ultra: visual/debug/capture density can increase and support normals can look cleaner, but no alternate fake substrate, no runtime hero-procedural shortcut, and no gameplay truth divergence.

## Unity Verification Packet

Single Unity owner only. Do not run in parallel with first-20 gameplay proof, DataMonolith bake, player build, profiler lane, or another editor verifier.

Required sequence:
1. Boot a clean first-20 route scene where voxel runtime, GPR, foundation placement, and drone manager are active.
2. Capture DataVault/descriptor state for `VoxelSdfPayloadDescriptor` and `VoxelSdfTexture3D`: owner `WorldStreaming`, valid flag, positive generation, byte count equals `x*y*z`, finite origin/cell/range.
3. GPR: trigger one scan due path; log `TryStageNearestSdf == true`, dimensions/range/version, ore count/source, and visible GPR hits. Capture screenshot/video plus blackbox/log artifact.
4. Foundation: attempt pylon placement on real terrain/substrate; capture no `FOUNDATION SNAP FAILED` warning in success case, `RealVoxelSdf`/`HitSdf` flags, support geometry, and one deliberate missing-substrate fail-closed case if safely isolated.
5. Drone: run headless/visible drone route near substrate; capture valid `DroneSdfGrid`, repulsion normal application, A* blocked-cell behavior, no lease leak warning, and route debug output.
6. Save/load: save after valid SDF publication and route activity, reload, re-run GPR/Foundation/Drone checks, and prove re-acquisition from current owner.
7. Profiler/GC: collect CPU/GPU/GC/frame artifacts for each route. Static source estimates are not proof.

Required artifact paths:
- Unity console/log extract.
- GPR scan blackbox/telemetry artifact.
- Foundation warning/surface/counter artifact.
- Drone route/debug artifact.
- Screenshot/video capture for player-visible behavior.
- Profiler/GC/Frame Debugger artifacts if claiming performance or rendering acceptance.
- Save/load before/after state diff artifact.

## Blocked Conditions

- No valid `VoxelSdfPayloadDescriptorDTO` with `OwnerSystemId = WorldStreaming`.
- `VoxelSdfTexture3D` generation zero, byte count mismatch, non-finite metadata, non-positive range/cell, or stale descriptor generation.
- GPR scan has no SDF lease or no ore source, making scan output empty or unrelated.
- Foundation success requires no no-substrate warning and visible support output; warning-only evidence is blocker proof.
- Drone proof requires valid SDF grid and observed behavior; route debug output with no SDF flag is blocker proof.
- Any runtime/profiler/save/load claim without artifact paths is rejected.

## Narrow Fix Candidates

No source or data fix was applied by this task.

If Unity proof fails because substrate is absent, the narrow fix is not consumer-side fallback code. Assign one owner to publish `VoxelSdfTexture3D` from the voxel/WorldStreaming route before GPR/Foundation/Drone consumers run, then prove consumers rebind through existing lease contracts.

If drone proof remains static-only, add a scoped non-Unity edit test for `DroneSdfGrid.TryCreate`, invalid-grid fail closed, and `IsBlockedForRadius` semantics. That would still not replace Unity route proof.

If DataMonolith becomes a substrate owner, create a separate bake/import proof packet. Do not infer it from narrative `substrate` rows.

## Future Unity Owner Prompt

`UNITY_SDF_SUBSTRATE_ROUTE_PROOF`: In a single Unity/editor slot, prove real `WorldStreaming` voxel SDF substrate is present and consumed by GPR, foundation pylon snapping, and drone navigation/repulsion. Capture descriptor/generation/byte-count/range evidence, GPR scan output, foundation support flags/geometry, drone SDF grid repulsion/A* blockage, save/load re-acquisition, and profiler/GC artifacts. Do not add hot `GlobalRegistry` polling, scene shortcuts, runtime fake SDF, or consumer fallbacks. If substrate is missing, stop at blocker proof and assign the owner-publish route.

## Files Inspected

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `construction.md`
- `vehicles.md`
- `tools.md`
- `physics.md`
- `performance.md`
- `architecture.md`
- `drones.md`
- `voxels.md`
- `data.md`
- `persistence.md`
- `systems.md`
- `authoring.md`
- `.agents-skills/VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `.agents-skills/VOX_Voxel_World_Logic_Carving_Persistence.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `.agents-skills/PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `Docs/Reports/Batch18/1805_AGENT_OUTPUT_TRIAGE_DASHBOARD.md`
- `Docs/Reports/Batch18/1813_STALE_BLOCKER_ERRATA_PACKET.md`
- `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`
- `Docs/ARCHITECTURE/FOUNDATION_SNAPPING_CALCULATOR_SHINOBU_252.md`
- `Assets/_Project/Scripts/Core/Contracts/GroundRadarContracts.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `Assets/_Project/Scripts/WorldRuntimeReferenceUtility.cs`
- `Assets/_Project/Scripts/HectonVoxelVolume.cs`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs`
- `Assets/_Project/Scripts/World/GPR/GroundRadarJobs.cs`
- `Assets/_Project/Scripts/Construction/FoundationPylonGpuBatch.cs`
- `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorJobs.cs`
- `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs`
- `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs`
- `Assets/_Project/Tests/Editor/FoundationSnappingCalculatorEditTests.cs`
- `Assets/_Project/Tests/Editor/VegetationAsyncJobFence1408EditTests.cs`
- `Assets/_Project/Tests/Editor/VoxelCompaction1418EditTests.cs`
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`

## Final Proof Label

STATIC PROOF PACKET COMPLETE.

Runtime SDF substrate consumption for GPR/Foundation/Drone: PENDING UNITY SLOT.
