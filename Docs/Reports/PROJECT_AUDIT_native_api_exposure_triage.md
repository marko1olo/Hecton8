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
