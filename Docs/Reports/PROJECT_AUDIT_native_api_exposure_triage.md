# PROJECT_AUDIT Native API Exposure Triage

Date: 2026-05-21
Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, compile, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or device proof was executed.

## Source

- Tool: `Tools/PolishMandateStaticAudit.py`
- JSON artifact: `Docs/Reports/PROJECT_AUDIT_polish_native_api_exposure.json`
- Markdown artifact: `Docs/Reports/PROJECT_AUDIT_polish_native_api_exposure.md`
- Command: `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.json --report-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.md`

## Raw Count Preservation

The public/internal/protected mutable native API warning class is:

- `nativeCollectionPublicMutableApiExposure`: 268 matches / 97 files

Additive exposure-kind buckets:

- `nativeApiExposureMutableReturn`: 79
- `nativeApiExposureOutRefMutable`: 189
- `nativeApiExposureAmbiguousMutable`: 0
- Sum: 268

Additive build-surface buckets:

- `nativeApiExposureBuildPlayerRuntime`: 254
- `nativeApiExposureBuildEditorOnly`: 5
- `nativeApiExposureBuildQaDevProof`: 9
- Sum: 268

Additive primary-risk buckets:

- `nativeApiRiskCoreVaultOrAllocatorSurface`: 21
- `nativeApiRiskEditorOrProofSurface`: 14
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 61
- `nativeApiRiskRuntimeOutRefMutableView`: 114
- `nativeApiRiskRuntimeReturnMutableView`: 58
- `nativeApiRiskRuntimeAmbiguousMutableView`: 0
- Sum: 268

This is not a debt reduction. It separates allocator/Vault APIs, editor/proof surfaces, and runtime mutable view exports so fixes can happen without breaking neighboring agents.

## Interpretation

The raw count is real enough to matter: most findings are player-runtime surfaces, not editor-only tooling. The two dangerous shapes are:

- Direct mutable native returns/properties, such as internal `NativeArray<T>` graph arrays and runtime BRG/GPU handoff buffers.
- `out/ref NativeArray<T>` APIs that hand mutable views to callers, even when method names say `ForEditor`, `Debug`, or `Snapshot`.

Core allocator and Vault APIs are counted intentionally. They are not automatically wrong, but they are ownership choke points and should be reviewed under a different standard than domain runtime APIs.

Top runtime mutable return/property files:

| File | Count | Static meaning |
|---|---:|---|
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 27 | Exposes active vegetation matrix/metadata/type buffers for direct GPU/consumer handoff. |
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 4 | Opens legacy/native queue writer surfaces. |
| `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 4 | Exposes mutable simulation pools/views. |
| `Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs` | 3 | Exposes mutable fauna simulation buffers. |

Top runtime `out/ref NativeArray<T>` files:

| File | Count | Static meaning |
|---|---:|---|
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 21 | Native read-buffer acquisition and cache sampling paths still surface mutable native views. |
| `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs` | 8 | Economy/ledger vault or owner-state views are exposed as mutable arrays. |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 6 | Navigation grid native state exits through mutable views. |
| `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs` | 5 | Buoyancy state is exposed to callers as mutable native buffers. |
| `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs` | 4 | Tide/seismic buffers are exposed through mutable out views. |

## Safe Next Actions

1. Do not mass-change signatures to `NativeArray<T>.ReadOnly`; many call sites pass these buffers into jobs, and compile fallout would be broad.
2. For each domain file, add a read-only accessor first while keeping the legacy mutable wrapper until all consumers move.
3. Mark true writer APIs explicitly: allocator/Vault write locks can return mutable views, but read/snapshot/debug APIs should return `NativeArray<T>.ReadOnly` or a domain snapshot DTO.
4. Runtime methods named `ForEditor`, `Debug`, `Snapshot`, or `Readback` need an actual compile/runtime boundary. A name is not an authority boundary.
5. BRG/GPU upload handoffs can keep native buffers only if the owner route documents lifetime, generation id, read fence, and mutation window.

## Current Worst Architectural Smell

The codebase has many methods that look like read accessors but return mutable native views. That violates the global read-accessor doctrine: a read route must not hand out a write-capable surface unless the name and ownership contract prove that the caller is the writer.

`HabitatGraphManager` graph SoA accessors are currently `NativeArray<T>.ReadOnly`, so they are no longer part of the direct mutable return/property top list. The diagnostic/editor-named bucket now separates 61 runtime-compiled mutable views with names or payloads such as `ForEditor`, `Debug`, `Readback`, `Snapshot`, or `Telemetry`; these are still player-runtime signatures unless they have an actual compile/runtime guard. The next real engineering step is per-domain read-only migration: start with one `HectonMapMagicVegetationBridge` buffer family, add read-only adapters, migrate consumers, then retire the mutable view only after compile/integration proof.

## 2026-05-22 Private Nested Filter And Ecosystem Save Snapshot Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Sub-agent inspection classified the current top mutable native API rows:

- `Shinobu19EconomyLedger` resolver APIs are owner/acquire/write routes and stay mutable.
- `FaunaSimulationMemory` buffer properties are real writer surfaces used by `FaunaDirector`; split read-only aliases later, do not narrow writer paths now.
- `EcosystemDirector.VaultNativeArray<T>` is a private nested owner helper; it is not an external public API despite `public` helper members.
- `EcosystemDirector.GetSaveSnapshotArray()` is a safe read-only route because `SaveManager` only serializes the snapshot.

Tooling delta:

- `PolishMandateStaticAudit.py` now tracks private containing types and moves those internal helper signatures to `nativeApiExposurePrivateNestedSuppressed`.
- Regression test: `test_suppresses_public_native_api_inside_private_nested_type`.
- Test command: `python Tools\test_polish_mandate_static_audit.py`, 12 tests OK.

Runtime source delta:

- `EcosystemDirector.GetSaveSnapshotArray()` returns `NativeArray<EcosystemSectorSaveRecord>.ReadOnly`.
- `SaveManager.StageSnapshotHeader()` and `ExecuteVerifiedSavePipeline()` carry the read-only ecosystem snapshot.
- `SaveBinaryStorage.TryWriteSaveFile()` and the indexed writer carry the read-only ecosystem snapshot.
- `WriteEcosystemSection()` copies each read-only ecosystem record by value into the raw payload after a section-length guard.

Updated static counts:

- After private nested filter artifact `Docs/Reports/PROJECT_AUDIT_polish_after_private_nested_api_filter.json`: `nativeCollectionPublicMutableApiExposure=155`, `nativeApiExposureBuildPlayerRuntime=142`, `nativeApiExposurePrivateNestedSuppressed=9`.
- After ecosystem save snapshot artifact `Docs/Reports/PROJECT_AUDIT_polish_after_ecosystem_save_readonly.json`: `nativeCollectionPublicMutableApiExposure=154`, `nativeApiExposureBuildPlayerRuntime=141`, `nativeApiExposureMutableReturn=35`, `nativeApiRiskRuntimeDiagnosticNamedMutableView=30`.

Rejected in this pass:

- Inventory ledger resolver narrowing, because current routes open/acquire mutable Vault buffers for owner writes and editor hydration.
- Fauna simulation buffer narrowing, because `FaunaDirector` writes those buffers.
- Persistent-world save snapshot narrowing, because it is a separate save-owner route and was not part of the proven ecosystem-only call path.
- Any managed array copy or save DTO/format change.

## 2026-05-22 Contextual IK Target Frame Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `ContextualPhysicalIkRuntime.CurrentTargetFrames` returned a mutable active front buffer.
- Observed consumer `ContextualPhysicalIkRig` only cached it and assigned it into `ContextualPhysicalIkApplyJob.TargetFrames`, which is a read-only animation job input.

Patch:

- `CurrentTargetFrames` now returns `NativeArray<ContextualPhysicalIkTargetFrame>.ReadOnly`.
- `ContextualPhysicalIkRig._currentTargetFrames`, `AssignEntitySlot`, and `OnTargetBufferSwapped` use the read-only view.
- `ContextualPhysicalIkApplyJob.TargetFrames` uses the read-only view.
- Runtime `_frontTargetFrames` / `_backTargetFrames` stay mutable inside the owner for scheduled writes and swaps.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_contextual_ik_targetframes_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 153, down from 154.
- `nativeApiExposureBuildPlayerRuntime`: 140, down from 141.
- `nativeApiExposureMutableReturn`: 34, down from 35.
- `nativeApiRiskRuntimeReturnMutableView`: 19, down from 20.

Rejected:

- Copying target frames for each rig.
- Changing target-frame DTO layout.
- Changing ground-response or front/back owner-write jobs.

## 2026-05-22 Biomimetic POI Existing-Buffer Resolver Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `ShinobuPoiVault.TryResolveExistingPlacementBuffers` had no observed first-party call sites and reads already existing POI placement buffers.
- Mutable POI allocation/write routes are separate: `AcquirePoiTransformBuffer`, `AcquireRouteBuffer`, and `AcquireTelemetryRing`.

Patch:

- Resolver outputs for `PoiTransformDTO`, `NarrativeBeaconRuleDTO`, and `PoiPlacementTelemetryEntry` are now `NativeArray<T>.ReadOnly`.
- The helper opens mutable locals internally only to return read-only aliases.
- The `Acquire*` functions and private Vault open/acquire helpers remain mutable.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_biomimetic_poi_existing_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 152, down from 153.
- `nativeApiExposureBuildPlayerRuntime`: 139, down from 140.
- `nativeApiExposureOutRefMutable`: 118, down from 119.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 29, down from 30.

Rejected:

- Changing placement jobs or HZB/indirect draw paths.
- Changing POI DTO layout or telemetry dump format.
- Narrowing writer/acquire routes without call-site proof.

## 2026-05-22 Flora Age Public Property Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `HectonIndirectVegetationRenderer.FloraAges01` was a public mutable native return for renderer-owned flora growth/harvest state.
- Focused search found no first-party direct mutation consumers; explicit writer routes are `TrySetFloraAge01` and `TryCopyFloraAges01`.

Patch:

- `FloraAges01` now returns `NativeArray<float>.ReadOnly`.
- The owner buffer, GPU upload path, culling compute binding, setter, and copy route remain unchanged.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_flora_age_readonly_property.json`:

- `nativeCollectionPublicMutableApiExposure`: 151, down from 152.
- `nativeApiExposureBuildPlayerRuntime`: 138, down from 139.
- `nativeApiExposureMutableReturn`: 33, down from 34.
- `nativeApiRiskRuntimeReturnMutableView`: 18, down from 19.

Rejected:

- Cable telemetry private helper narrowing, because it was not an outward mutable API surface.
- Raw external mutation through `FloraAges01`; external writes should go through explicit owner-authorized methods.
- Changing GPU buffer layout, culling shader bindings, or flora age SoA storage.

## 2026-05-22 Prefab Registry Native Map Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `PrefabRegistry.GetNativeMap()` returned `NativeHashMap<int,int>` while its documentation described read-only Burst access.
- Focused search found no first-party call sites; Unity Collections package source confirms `NativeHashMap<TKey,TValue>.ReadOnly` and `AsReadOnly()`.

Patch:

- `GetNativeMap()` now returns `NativeHashMap<int,int>.ReadOnly`.
- The static audit read-only suppression now covers `.ReadOnly` wrappers for native collection types, not only `NativeArray<T>.ReadOnly`.
- Regression coverage was added to `test_detects_public_mutable_native_api_exposure`.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_readonly_native_hashmap_filter.json`:

- `nativeCollectionPublicMutableApiExposure`: 150, down from 151.
- `nativeApiExposureBuildPlayerRuntime`: 137, down from 138.
- `nativeApiExposureMutableReturn`: 32, down from 33.
- `nativeApiRiskRuntimeReturnMutableView`: 17, down from 18.

Rejected:

- Changing registry warmup or disposal ownership.
- Returning the mutable map just because no current caller exists.
- Suppressing mutable queue writer, allocator, or ring-buffer owner APIs.

## 2026-05-22 Chemical Snapshot Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `ChemicalInfluenceGrid.TryGetPublishedSnapshot`, `TryGetActivePublishedSnapshot`, and `TryGetPublishedBreadcrumbs` returned mutable native arrays.
- Observed consumers only sample the grid/breadcrumb data in predator cognition, mesofauna behavior, and flora parasite jobs.

Patch:

- Chemical front/overlay grid snapshots now return `NativeArray<float4>.ReadOnly`.
- Chemical breadcrumb snapshots now return `NativeArray<ChemicalBreadcrumbWaypoint>.ReadOnly`.
- Predator, mesofauna, and flora parasite job fields were updated to read-only native aliases.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_chemical_snapshot_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 147, down from 150.
- `nativeApiExposureBuildPlayerRuntime`: 134, down from 137.
- `nativeApiExposureOutRefMutable`: 115, down from 118.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 27, down from 29.
- `nativeApiRiskRuntimeOutRefMutableView`: 69, down from 70.

Rejected:

- Fixing publish-on-read behavior in this pass; that requires an authority-route migration, not only a signature narrowing.
- Changing chemical emitter queues, grid writer buffers, DTO layout, or sensory math.
- Copying chemical grids into AI/flora-owned buffers.

## 2026-05-22 Thermal Readback Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `IThermodynamicsService.TryGetThermalMapReadback` and `TryGetThermalGridReadback` exposed owner thermal buffers as mutable native arrays.
- The contract text already defined the map/grid as read-only readback data, and focused consumers only sample via read-only unsafe pointers.

Patch:

- Thermodynamics map/grid readbacks now return `NativeArray<float>.ReadOnly`.
- `AbyssalThermalManager` exports `.AsReadOnly()` aliases.
- `ModularEquipmentEngine` and `ShinobuMetabolismRuntime` carry read-only grid views through their thermal sampling handoff.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_thermal_readback_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 145, down from 147.
- `nativeApiExposureBuildPlayerRuntime`: 132, down from 134.
- `nativeApiExposureOutRefMutable`: 113, down from 115.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 25, down from 27.

Rejected:

- Changing thermal diffusion front/back buffers or thermodynamics owner memory.
- Copying 32x32x32 thermal grids into consumer-owned allocations.
- Changing thermal DTO layout, vegetation thermal grids, or heat injection/sampling authority.

## 2026-05-22 Whirlpool Flow Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `HectonFluidEngine.TryGetActiveWhirlpoolFlows` exposed owner whirlpool rows as mutable native memory.
- Observed consumers are player kinematics and submarine auto-level PID jobs; both only sample `HectonAnalyticalFlowField.SampleWhirlpoolVelocity` and already treat the input as read-only.

Patch:

- Active whirlpool flows now return `NativeArray<WhirlpoolFlow>.ReadOnly`.
- `PlayerKinematicsBodyJob`, `SubmarineAutoLevelPidJob`, and the analytical sampler overload now consume read-only whirlpool aliases.
- `_activeWhirlpools`, active counts, and internal fluid owner jobs remain mutable inside `HectonFluidEngine`.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_whirlpool_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 144, down from 145.
- `nativeApiExposureBuildPlayerRuntime`: 131, down from 132.
- `nativeApiExposureOutRefMutable`: 112, down from 113.
- `nativeApiRiskRuntimeOutRefMutableView`: 68, down from 69.

Rejected:

- `TryGetActiveMaelstroms` float4 route because current VFX consumers still feed it into `GraphicsBufferUploadUtility.UploadNativeArray(NativeArray<T>)`.
- Changing GPU upload helper signatures inside a fluid gameplay handoff pass.
- Rewriting internal fluid writer buffers or active whirlpool generation jobs.

## 2026-05-22 Cave Signed-Distance Payload Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `HectonCaveVoxelLightingVolume.TryGetPublishedSignedDistanceVoxelPayload` exposed the owner `_sdfVolume` buffer as mutable native memory.
- Focused search found the only current first-party native consumer in `PredatorCognitionDomain`, and that consumer immediately converted the result to a read-only threat voxel view.

Patch:

- Cave signed-distance payload now returns `NativeArray<byte>.ReadOnly`.
- Predator cognition stores the read-only alias directly.
- Cave scan/finalize buffers and GPU texture publication remain owner-mutable inside `HectonCaveVoxelLightingVolume`.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_cave_sdf_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 143, down from 144.
- `nativeApiExposureBuildPlayerRuntime`: 130, down from 131.
- `nativeApiExposureOutRefMutable`: 111, down from 112.
- `nativeApiRiskRuntimeOutRefMutableView`: 67, down from 68.

Rejected:

- Broad sonar SDF payload conversion because audio, player, scanner, UI, and voxel delta paths still carry mutable native signatures.
- Changing cave scan buffers, GPU SDF texture publication, or predator threat voxel math.
- Copying SDF payloads into predator-owned buffers.

## 2026-05-22 Persistent-World Save Snapshot Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `PersistentWorldRegistry.GetSaveSnapshotArray()` returned a mutable `NativeArray<PersistentWorldDeltaRecord>` after `CaptureSaveSnapshot()`.
- Observed consumers in `SaveManager`, `SaveBinaryStorage`, `PlayerExplorationTracker`, and `SaveRecoverySmokeTester` only count, serialize, or read records.

Patch:

- `PersistentWorldRegistry.GetSaveSnapshotArray()` now returns `NativeArray<PersistentWorldDeltaRecord>.ReadOnly`.
- `SaveManager.StageSnapshotHeader()` and `ExecuteVerifiedSavePipeline()` carry the read-only persistent-world snapshot.
- `SaveBinaryStorage.TryWriteSaveFile()`, the indexed writer, indexed sector grouping, table builder, and persistent-world section writer consume read-only records.
- `PlayerExplorationTracker` holds read-only persistent deltas for POI reveal/discovery.
- `SaveRecoverySmokeTester` passes its synthetic mutable temp array as `.AsReadOnly()` into the writer.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_persistent_world_save_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 142, down from 143.
- `nativeApiExposureBuildPlayerRuntime`: 129, down from 130.
- `nativeApiExposureMutableReturn`: 31, down from 32.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 24, down from 25.

Rejected:

- Sector override writer route narrowing, because those APIs consume caller-owned temp arrays for indexed save block writes.
- Restore/load mutation paths, because loaded records are used to rebuild owner state.
- Managed snapshots, DTO layout changes, save identity changes, or Vault handle changes.

## 2026-05-22 Economy Telemetry Read-Only Dump Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `Shinobu19EconomyLedger.TryResolveTelemetry` exposed the `BufferID.ShinobuEconomyTelemetryRing` black-box ring as mutable native memory.
- `DumpTelemetryRing`, `DumpTelemetryRingH8Dump`, `DumpTelemetryRingOrdered`, and `TryDumpTelemetryOnFault` only scan or serialize telemetry rows.
- Focused search found no first-party external callers that require mutable access to this resolver/dump route.

Patch:

- `TryResolveTelemetry` now returns `NativeArray<EconomyTelemetryEntry>.ReadOnly` after opening the existing Vault buffer as a mutable local and publishing only `.AsReadOnly()`.
- Economy dump and fault-dump helpers now accept read-only telemetry rings.
- `RecordTelemetry` and `ShinobuEconomyTelemetryJob.Telemetry` remain mutable because they are the explicit writer path.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_economy_telemetry_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 141, down from 142.
- `nativeApiExposureBuildPlayerRuntime`: 128, down from 129.
- `nativeApiExposureOutRefMutable`: 110, down from 111.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 23, down from 24.

Rejected:

- Telemetry writer route narrowing, because `RecordTelemetry` writes the black-box ring.
- `EconomyTelemetryEntry` layout changes, because the struct is already explicit 64-byte telemetry payload.
- Managed copies, Vault handle changes, or new allocation paths.

## 2026-05-22 IK Black-Box and Async Buoyancy X-Ray Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `LeviathanTerrainIkBlackBox` and `VRPhysicalHandPresenceBlackBox` dump helpers only read black-box telemetry and output rows.
- `AsyncBuoyancyReadbackRuntime.TryOpenEditorViews` is an editor/X-ray read view; mutation is already routed through `ApplyEditorTuning`.

Patch:

- IK black-box dump/fault-dump methods now accept `NativeArray<T>.ReadOnly` telemetry/cursor/output views.
- Async buoyancy X-ray view now returns read-only tuning, telemetry, cursor, and counter aliases.
- `AsyncGpuReadbackXRayWindow.UpdateWaterfall` consumes a read-only telemetry ring.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_async_buoyancy_editor_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 140, down from 141.
- `nativeApiExposureBuildPlayerRuntime`: 127, down from 128.
- `nativeApiExposureOutRefMutable`: 109, down from 110.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 22, down from 23.

Rejected:

- IK `TryResolveBuffers` and telemetry job field narrowing, because those are writer/resolver paths.
- Async GPU readback result-state ownership changes.
- Managed diagnostic mirrors or DTO layout changes.

## 2026-05-22 Analytical Wave Editor View Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `AnalyticalGerstnerWaveRuntime.TryOpenEditorViews` was a public editor view returning mutable tuning, telemetry, cursor, request, and result buffers.
- Focused search found no first-party callers of the analytical runtime editor view. The active analytical wave editor reads/writes the Vault through its own dedicated read/write helpers.

Patch:

- `TryOpenEditorViews` now returns read-only aliases for all five analytical wave buffers.
- The method still validates owner buffer creation and length before publishing aliases.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_analytical_wave_editor_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 139, down from 140.
- `nativeApiExposureBuildPlayerRuntime`: 126, down from 127.
- `nativeApiExposureOutRefMutable`: 108, down from 109.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 21, down from 22.

Rejected:

- Analytical solver job signature changes.
- Gerstner DTO/request/result layout changes.
- Managed editor snapshots or new allocation paths.

## 2026-05-22 Buoyancy SIMD X-Ray Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `BuoyancyDisplacementRuntime.TryOpenSimdEditorViews` was consumed by `BurstVectorizationXRayWindow`.
- The observed caller only reads telemetry and cursor rows; the tolerance output is discarded.
- Scalar fallback tuning remains on `TryOpenSimdTuningEditorView`, which is an explicit editor writer route and was not narrowed.

Patch:

- SIMD editor view outputs now return read-only telemetry, cursor, and tolerance aliases.
- `BurstVectorizationXRayWindow.RefreshTelemetry` consumes read-only telemetry/cursor aliases.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_buoyancy_simd_editor_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 138, down from 139.
- `nativeApiExposureBuildPlayerRuntime`: 125, down from 126.
- `nativeApiExposureOutRefMutable`: 107, down from 108.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 20, down from 21.

Rejected:

- `TryOpenSimdTuningEditorView`, because editor controls mutate scalar fallback tuning through it.
- Sleep telemetry editor view narrowing, because `PhysicsSleepStateXRayWindow` writes tuning and SDF config through that route.
- Managed graph copies or solver DTO layout changes.

## 2026-05-22 Inventory No-Call Resolver Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `Shinobu19EconomyLedger.TryResolveCarryTotals` and `TryResolveHotbarRoutes` had no first-party call sites in focused search.
- Both methods are read-accessor-shaped resolver routes and do not need to export mutation authority.

Patch:

- Carry totals now return `NativeArray<ShinobuCarryTotalsDTO>.ReadOnly`.
- Hotbar routes now return `NativeArray<int>.ReadOnly`.
- Mutable Vault aliases stay inside `OpenOrAcquireEconomyVaultBuffer` and are immediately converted with `.AsReadOnly()`.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_inventory_no_call_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 136, down from 138.
- `nativeApiExposureBuildPlayerRuntime`: 123, down from 125.
- `nativeApiExposureOutRefMutable`: 105, down from 107.
- `nativeApiRiskRuntimeOutRefMutableView`: 65, down from 67.

Rejected:

- `TryResolveVaultLedger`, because it exposes the inventory writer SoA route.
- Recipe/ingredient/physical constants buffers, because editor tuning/import paths mutate them.
- `ExportRleToVaultScratch`, because it writes into caller scratch.
- FutureCommand byte-input narrowing, because the remaining counted findings are queue ref/writer bridge APIs.

## 2026-05-22 Seaglide Editor View Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `SeaglideHydrodynamicsRuntime.TryResolveEditorViews` is consumed by `SeaglideHydrodynamicsXRayWindow`.
- `TryResolveForcePacketEditorView` is consumed by `SeaglideCurrentDebugGizmo`.
- The X-Ray slider path was the only observed mutation and used `GetUnsafePtr()` through the read resolver.

Patch:

- Editor tuning, counters, telemetry, cursor, audio, cavitation, and force packets now publish read-only aliases.
- `SeaglideHydrodynamicsXRayWindow.ApplySliderValues` now calls `TryApplyEditorTuning` with finite scalar slider values instead of mutating a native pointer.
- Runtime owner code still writes the single tuning row through its Vault handle.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_seaglide_editor_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 134, down from 136.
- `nativeApiExposureBuildPlayerRuntime`: 121, down from 123.
- `nativeApiExposureOutRefMutable`: 103, down from 105.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 19, down from 20.
- `nativeApiRiskRuntimeOutRefMutableView`: 64, down from 65.

Rejected:

- Returning mutable tuning just for editor sliders.
- Managed tuning/telemetry graph copies.
- Runtime solver job buffer rewrites or force packet ownership changes.

## 2026-05-22 Animation Tuning Editor View Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `ProceduralBoneBlenderRuntime.TryResolveTuningForEditor` is consumed by `ProceduralRigTunerWindow` for readout and sliders.
- `KineticCharacterAnimatorRuntime.TryResolveTuningForEditor` is consumed by `KineticCharacterAnimationTunerWindow` for readout and sliders.
- CSV/import code needs mutable owner-local access, but it can remain private inside each runtime.

Patch:

- Public tuning editor APIs now return `NativeArray<T>.ReadOnly`.
- Editor windows mutate local DTO copies and submit them through `TryApplyEditorTuning`.
- Runtime CSV/import paths use private `TryResolveTuningMutable` helpers.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_animation_tuning_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 132, down from 134.
- `nativeApiExposureBuildPlayerRuntime`: 119, down from 121.
- `nativeApiExposureOutRefMutable`: 101, down from 103.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 17, down from 19.

Rejected:

- Mutable editor tuning buffers.
- Managed tuning mirrors.
- Solver, GPU matrix upload, or DTO layout rewrites.

## 2026-05-22 Buoyancy Editor View Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `BuoyancyDisplacementRuntime.TryOpenEditorViews` is consumed by `HydrodynamicBuoyancyTunerWindow` for readout and designer tuning.
- `TryOpenSleepTelemetryEditorViews` is consumed by `PhysicsSleepStateXRayWindow`.
- `TryOpenSimdTuningEditorView` is consumed by `BurstVectorizationXRayWindow` for scalar fallback tuning.
- Runtime solver buffers, force packet queues, material CSV lanes, and physics apply lanes remain explicit writer/owner routes.

Patch:

- Public buoyancy editor views now return `NativeArray<T>.ReadOnly`.
- Editor windows mutate local DTO/scalar values and submit them through `TryApplyEditorTuning`, `TryApplySleepTelemetryEditorTuning`, or `TryApplySimdScalarFallbackEditorTuning`.
- Owner runtime resolves mutable Vault buffers internally only inside those apply methods.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_buoyancy_editor_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 128, down from 132.
- `nativeApiExposureBuildPlayerRuntime`: 115, down from 119.
- `nativeApiExposureOutRefMutable`: 97, down from 101.
- `nativeApiRiskRuntimeDiagnosticNamedMutableView`: 14, down from 17.
- `nativeApiRiskRuntimeOutRefMutableView`: 63, down from 64.

Rejected:

- Mutable editor tuning buffers.
- Managed editor mirrors.
- Solver, force packet, material CSV, physics apply, or DTO layout rewrites.

## 2026-05-22 Construction Telemetry Read Accessor Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `ModularBaseConstructionValidator.TryReadTelemetryRing` is a read-accessor-shaped diagnostic method and now publishes only a read-only ring alias.
- `PlayerBuilder` writes telemetry through `EnsureTelemetryRing` and `WriteTelemetry`, which remain explicit writer/acquire methods.

Patch:

- `TryReadTelemetryRing` returns `NativeArray<ConstructionTelemetryEntry>.ReadOnly`.
- The owner helper resolves a mutable local only to publish `.AsReadOnly()`.
- The player builder write path no longer writes through a `TryRead*` method.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_construction_telemetry_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 127, down from 128.
- `nativeApiExposureOutRefMutable`: 96, down from 97.
- `nativeApiExposureBuildQaDevProof`: 7, down from 8.
- `nativeApiRiskEditorOrProofSurface`: 12, down from 13.

Rejected:

- Narrowing `EnsureTelemetryRing`, bounds, or occupancy writer/acquire APIs.
- Changing telemetry DTO layout or dump format.
- Moving construction validation write ownership in this pass.

## 2026-05-22 Seismic Vault Helper Scope Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `HectonSeismicTideDirector.TryOpenVaultBuffer`, `OpenVaultPointer`, handle matching, and the ref-handle acquire overload are implementation helpers with no external file callers.
- `OpenOrAcquireVaultBuffer(vault, bufferId, ...)` and `TryOpenExistingVaultBuffer` remain `internal` because same-file top-level editor/proof classes call them.

Patch:

- Owner-internal helpers are now `private static`.
- Same-file editor/proof entry methods remain accessible without exposing the lower-level helpers.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_seismic_helper_scope.json`:

- `nativeCollectionPublicMutableApiExposure`: 125, down from 127.
- `nativeApiExposureBuildPlayerRuntime`: 113, down from 115.
- `nativeApiExposureOutRefMutable`: 94, down from 96.
- `nativeApiRiskRuntimeOutRefMutableView`: 61, down from 63.

Rejected:

- Changing writer/acquire semantics to read-only aliases.
- Making same-file top-level editor classes unable to call their documented entry methods.
- Seismic DTO layout or Vault ownership changes.

## 2026-05-22 Base Module Catalog Byte Hydration Update

Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

Selected route:

- `BaseModuleCatalogRuntime.TryLoadCatalogBytes` and `TryStartCatalogByteLoad` had no first-party call sites in focused search.
- Both APIs publish file-hydration bytes; the mutable target is only required inside the loader while filling the Vault byte lane.

Patch:

- Byte-load outputs now return `NativeArray<byte>.ReadOnly`.
- Synchronous and task-backed read paths keep a mutable owner-local `targetBytes` for the file read, then expose `.AsReadOnly()`.

Updated static counts from `Docs/Reports/PROJECT_AUDIT_polish_after_base_module_catalog_bytes_readonly.json`:

- `nativeCollectionPublicMutableApiExposure`: 123, down from 125.
- `nativeApiExposureBuildPlayerRuntime`: 111, down from 113.
- `nativeApiExposureOutRefMutable`: 92, down from 94.
- `nativeApiRiskRuntimeOutRefMutableView`: 59, down from 61.

Rejected:

- Hydration job signature changes.
- Catalog binary format or DTO layout changes.
- Managed byte mirrors.
