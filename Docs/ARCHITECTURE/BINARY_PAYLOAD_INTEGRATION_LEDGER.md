# HECTON-8 Binary Payload Integration Ledger

Date: 2026-05-18
Owner lane: H8BIN_GRAVEYARD_AUDITOR
Status: STATIC SOURCE / FILESYSTEM LEDGER, RUNTIME PENDING

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

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not binary load success, content completeness, alignment proof, profiler, or player-build proof.

- `Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs`
- `Assets/_Project/Scripts/Core/Bridge/H8BridgeBinaryLayoutVerifier.cs`
- `Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs`
- `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompilerWindow.cs`
- `Assets/_Project/Scripts/Editor/DataMonolith/Hecton8.DataMonolith.Editor.asmdef`
- `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`
- `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin`

## 2026-05-19 SHINOBU_103 Data Monolith Editor Import Boundary

- Data Monolith editor tooling is now scoped by `Hecton8.DataMonolith.Editor.asmdef`, Editor-only, unsafe-enabled, and references only `Hecton8.Core`, `Unity.Burst`, `Unity.Collections`, and `Unity.Mathematics`.
- Stable `.meta` GUIDs exist for `H8DataMonolithCompiler.cs`, `H8DataMonolithCompilerWindow.cs`, and the DataMonolith editor asmdef. This prevents local Unity GUID minting for the compiler facade.
- Runtime Data Monolith source is still compiled under Core; no `Hecton8.Data.Runtime.asmdef` is claimed. Splitting runtime data requires a planned bootstrap contract/facade because Core bootstrap calls `H8StaticDataArena` and the arena consumes Core Vault/fatal-boot contracts.
- 2026-05-20 SHINOBU_202 pointer-safety pass: `H8StaticDataArena` no longer keeps a persistent static `NativeArray<byte>` arena view or legacy Data Monolith `VaultBufferHandle<T>` fields. Runtime payload buffer `71103`, telemetry ring `71104`, and telemetry cursor `71105` are stored as `VaultGenerationHandle<T>` descriptors, resolved through `GlobalDataVault.TryResolveHandle` per access, and released through `GlobalDataVault.ReleaseBuffer` during shutdown. This is STATIC_SOURCE / PY_TOOL orientation only; it is not compile, Unity import, runtime, profiler, GC, platform, or route-approval proof. This ledger row is not route approval; a route card must still name owner, producer/consumer phase, capacity, overflow/failure, telemetry fields, black-box fields, shutdown/disposal, and proof artifact tuple before these buffers are treated as accepted global authority.
- `ScavengingLootOracle` now treats `H8StaticDataArena` `LootCdf` rows as its default runtime loot-table source. If a player build has a valid monolith but no `LootCdf` rows, the runtime yields no fake loot instead of scheduling the emergency table; editor/manual self-audit can still schedule the deterministic emergency CDF.
- Scavenging editor/manual loot CSV self-audit now reads selected CSV files through `FileStream` into a Temp `NativeArray<byte>` and invokes the native byte parser directly. It must not reintroduce `File.ReadAllBytes` or managed `byte[]` staging for static-data consumer tooling.
- `H8DataMonolithCompilerWindow` now makes the primary `BAKE MONOLITH` command a large bold `260 x 42` button instead of an ordinary toolbar control.
- `H8DataMonolithCompilerWindow` binary inspection now surfaces `H8DataMonolithCompiler.TryValidateOutputBlob` before printing local section diagnostics, so Task 20 uses the same validation contract as the prebuild artifact gate. The inspector calls this path without mutating the compiler's stored `LastError`, preserving cross-reference bake failures for Task 18 facade display.
- Runtime directory validation now shares `H8DataLayoutAudit.GetExpectedRecordSize` with the editor gate and rejects stale/tampered section order, record-size, empty-offset, data-start, and localization mirror mismatches before `Ready`.
- Task 14 cross-reference validation now operates on raw CSV rows and synthetic JSON source rows before blob output. Broken item references in item recipes, recipe outputs/ingredients, loot items, and economy item/recipe fields report file, line or source index, field, packed-token index, authored value, and computed FNV-1a hash instead of only an anonymous owner/hash pair.
- Automated editor bakes now route through one debounced scheduler. Asset import callbacks and filesystem change events call `H8DataMonolithFileSystemWatcher.RequestBake()`, wait 0.75 seconds after the latest source change, skip during Unity compilation, and block overlapping bakes with an interlocked in-progress flag.
- CSV source ingestion now uses a bounded editor worker pool capped at `Environment.ProcessorCount - 1` instead of launching one `Task.Run` per source file.
- Play-mode Data Monolith hot reload now queues same-process bakes directly instead of bouncing through loopback TCP. The socket bridge remains for external packets only, accepts only the canonical `static_data.h8bin` path, caps packet length at 1024 characters, and tears down on play-mode exit, assembly reload, and editor quit.
- Verification status: static source and import-boundary files exist; Unity import/project regeneration, editor menu discovery, prebuild callback invocation, binary bake, profiler, and player-build proof remain pending.

## 2026-05-20 DOC_GLOBAL R44 Current Boundary Note

This ledger remains static binary/documentation orientation, not runtime payload load, memory, or platform proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`; R45 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

## 2026-05-20 SHINOBU_107 Foveated Simulation Vault Alias Boundary

- `FoveatedSimulationManager` no longer creates owner-local `NativeArray` or `NativeList` persistent allocations. Persistent native storage is requested through `GlobalDataVault` generation handles owned by `SystemID.SystemDispatcher`.
- Owner-local Vault buffer IDs are local numeric casts, not global enum additions: `73220` score positions, `73221` entity AUP/runtime positions, `73222` importance scores, `73223` tick-rate codes, `73224` frustum flags, `73225` simulation tiers, `73226` distance output, `73227` interpolation-from positions, `73228` interpolation-to positions, `73229` interpolation alphas, `73230` pending raycast commands, `73231` pending raycast command indices, `73232` deferred raycast command batch, `73233` deferred raycast hits, and `73234` 300-frame foveated telemetry ring.
- The previous `NativeList<RaycastCommand>` deferred batch is now a fixed Vault-backed `NativeArray<RaycastCommand>` plus a logical command count. This keeps the deferred raycast budget bounded and avoids private native collection ownership.
- Native memory sentinel ownership remains at the Vault allocation site; the foveated manager records only a logical memory budget for the resolved aliases. Duplicate pointer registration of Vault aliases is explicitly avoided.
- Verification status: static source only. `Docs/Reports/SHINOBU_107_StaticScan/SHINOBU_140_Vault_Sovereignty.json` has no `FoveatedSimulationManager.cs` finding after the local scan. Unity import, Burst compile, Play Mode, profiler, GCMonitor, and player-build proof remain pending.

## 2026-05-20 SHINOBU_208 Offline Geology Mesh Manifest Boundary

- Geology Forge now emits a BRG-oriented static payload at `Assets/_Project/BakedGeometry/Geology/geology_mesh_manifest.h8geom` during editor bakes.
- Payload layout is fixed: `GeologyMeshManifestHeader` is 64 bytes and `GeologyMeshManifestRecord` is 128 bytes, validated by `GeologyVertexLayoutValidator` through `UnsafeUtility.SizeOf` and exact field offsets.
- Each record carries sector `double3` AUP, deterministic seed, profile hash, LOD0/1/2 triangle counts, 32B vertex stride, local bounds, three 128-bit Unity mesh GUIDs split into high/low `ulong`, BRG-ready flag, and variation.
- The manifest is static render data only. It is not rollback state, not a new Vault route, and not a runtime owner. Runtime BRG/indirect consumers must import it through their own owner lane before claiming Play Mode proof.
- Generated prefab/LODGroup/GameObject output has been removed from SHINOBU_208's bake lane; generated meshes remain immutable `.asset` files with AO in vertex red.
- Verification status: static source/docs only. No Unity import, manifest bake, BRG runtime ingestion, profiler, player-build, or asset GUID proof is claimed yet.

## 2026-05-20 SHINOBU_213 Offline LOD and Collider Manifest Boundary

- Offline LOD and Collider Forge now emits `Assets/_Project/BakedGeometry/Optimized/offline_lod_manifest.h8lod` during editor batch report generation.
- Payload layout is fixed: `OfflineLodManifestHeader` is 64 bytes and `OfflineLodManifestRecord` is 128 bytes. Both use explicit 4-byte-aligned fields, explicit reserve lanes, and editor validation through `UnsafeUtility.SizeOf`.
- The writer emits every field with explicit little-endian 4-byte serialization. Float lanes are serialized through `math.asuint`; this checkout uses a local `ReverseBytes(uint)` fallback because the installed `Unity.Mathematics` surface has no `math.reversebytes` API.
- Each record carries source/output hashes, LOD1/LOD2 mesh hashes, original and generated triangle counts, primitive/convex collider counts, continuous quality/depth/ratio/tolerance fields, decimation window, warning flags, and state hash. It contains no Unity object reference, string, pointer, managed array, rollback state, or gameplay authority.
- Generated mesh assets use a 32-byte interleaved vertex layout, primitive-first collider authoring, and bounded 8..32 support hull fallback. Invalid hull topology, failed hull asset binding, corrupt index/range/vertex streams, and mock asset reload failures fail closed instead of creating unsafe runtime payload state.
- The manifest is immutable editor output only. It is not a `GlobalDataVault` buffer, not netcode rollback state, and not a runtime owner. Runtime BRG/LOD consumers must import it through their own owner lane before claiming Play Mode, Burst, profiler, GC, or player-build proof.
- Verification status: static source/docs only. Pre-endian local Roslyn probe previously passed under `Temp/SHINOBU_213_CompileProbe`, but the explicit-endian, bounded-hull, fail-closed asset-binding, hull-safety, index-stream, and mock-reload edits still require a post-endian safety-index probe when CPU drops below the build gate. Unity import, manifest bake, generated asset inspection, profiler/GCMonitor, and player-build proof remain pending.

## 2026-05-19 SHINOBU_160 Asynchronous Telemetry Export Vault Lane

- Added SHINOBU_160 owner-local Vault buffer IDs `71860..71876` for analytics event ring, POST_SIMULATION staging, routine ingress ring, critical ingress ring, 64-byte ingress cursor/control row, counters, 300-frame telemetry ring, telemetry cursor, tuning, CSV scratch, compressed scratch, heatmap debug readback, double handoff buffers, worker accumulation, raw batch scratch, and worker-flushed black-box dump snapshot.
- Primary DTO: `AnalyticEventDTO` is explicit 32 bytes with `EventHashID=0`, `TimestampSeconds=4`, and full `double3 EventAUP=8`. No float world coordinate or JSON payload is part of the runtime event truth.
- Runtime boundary: producers push unmanaged DTOs through the owner-local analytics facade or existing contract `SignalBus` snapshots into Vault-owned routine or critical ingress rings; the facade is owner-thread gated, applies continuous backlog/quality culling before ring write, and records hot counters through atomics that flush in `DispatcherPhase.PostSimulation`. `AsynchronousTelemetryExporter` bridges `EntityDeathSignal`, `ItemAcquiredSignal`, `SurvivalVitalsChangedSignal`, `FrameTimeSignal`, and KCC velocity snapshots without concrete sibling-domain references. It drains critical telemetry first, then routine telemetry, with `drainBudget = min(stagingCapacity, round(lerp(10,1000,GlobalQualityWeight)))`; routine drain pressure uses deterministic quality/backlog/AUP-bit decimation instead of all-or-nothing threshold dropping. Accepted rows mirror into Vault, then a fixed batch hands off to `H8_Analytics_IO` through Vault-owned locked handoff buffers. The public `NativeQueue.ParallelWriter` route and exporter-owned persistent `NativeQueue` ingress were removed; ingress storage is now `71874`/`71875` plus `71876` cursor, and live fixed-ring saturation increments lane overflow counters in that cursor instead of double-counting through generic hot-drop deltas. The background thread uses cached locked handle pointers for worker-owned buffers instead of entering Vault metadata resolution. HTTP scheme validation, RLE compression, HTTP POST, failed-response disposal, disk fallback, backlog replay, and black-box file writes execute only on the background thread. No `UnityWebRequest` route or private managed worker-array state is introduced.
- 2026-05-20 active polish: runtime frame identity now uses `DispatcherTimingDTO.FrameId` with an owner-local zero-frame fallback; mock analytics uses `Unity.Mathematics.Random` seeded by `SystemHash ^ SectorHash ^ SimulationFrame`; mock density scales continuously from 20 to 500 events/sec by `GlobalQualityWeight`, collapses under backlog pressure, and adds generated mock writes from the ingress cursor delta into the same owner-local enqueued/backlog counters used by live producers; routine pressure culling hashes event type, timestamp, backlog, and full AUP double bits to avoid same-second cohort drops in both the hot facade and Burst drain; fresh KCC velocity snapshots update the player AUP anchor during `POST_SIMULATION` while route heatmap emission remains timer/flag gated; Vault-owned routine/critical ingress rings remove routine-backlog scanning for critical telemetry without persistent `NativeQueue`; hot fixed-ring overflow returns an `IngressWriteOverflow` result, writes the cursor overflow field, and avoids a second generic hot-drop increment; facade-rejected non-finite AUP increments a hot non-finite delta that flushes into Vault counters; telemetry backlog fields use ingress pending + handoff + volatile-published worker accumulation; worker flags mutate through CAS helpers instead of volatile read/modify/write; `AnalyticsExporterTelemetryEntry` offset 60 now records `VaultBytes`; deferred `OnDestroy` cleanup runs only after the worker has actually stopped; editor telemetry refresh has null guards; `AnalyticsLayout` validates primary DTO offsets without runtime reflection and is called during cold runtime `OnEnable`; disk replay deletes corrupt/partial/replayed `.h8log` files only after the read stream is closed and fault-counts replay exceptions or short reads inside the worker path; fallback publication uses unique sequenced `.tmp` files with `FileMode.CreateNew` and no final `.h8log` deletion; failed HTTP responses are disposed after status-code capture on `H8_Analytics_IO`; non-HTTP endpoint schemes are rejected before `WebRequest.Create`; and the runtime exporter source is statically clean for `Time.frameCount`, `UnityEngine.Random`, `new AnalyticEventDTO`, fixed `EventCount = 500`, `NativeQueue<AnalyticEventDTO>`, and `typeof(...).GetField(...)` layout guards.
- First 20 Minutes route impact: proof/testability only. This lane exports death/resource/route/hazard/perf observations for the Copper Wire route without making analytics a gameplay dependency.
- Scalability boundary: `GlobalQualityWeight` continuously maps routine retention and drain work from `10` to `1000` events per drain; routine backlog culling is deterministic stochastic decimation seeded by event hash/timestamp/backlog/AUP bits; hashes with the high bit set are critical, route through the critical lane, and survive routine backlog culling.
- Route card: `Docs/ARCHITECTURE/ASYNCHRONOUS_TELEMETRY_EXPORTER_SHINOBU_160.md`. Blackbox dump path: `Docs/AgentLogs/Dump_SHINOBU_160.bin`.
- Verification status: STATIC_SOURCE / STATIC_DOC orientation only unless each cited scan or test names an artifact path, command/tool, timestamp, environment, and output. The 2026-05-20 bounded-drain, lifecycle-hardening, KCC/mock-load, reflectionless-layout, disk-replay, partial-read replay, fallback-publication, AUP-gated culling, and hot-overflow cursor rows remain source-scan summaries, not current runtime/editor cleanliness. Archived Unity batchmode logs at `Docs/Archive/Batch010/AgentLogs/Unity_SHINOBU_160_compile.log` and `Docs/Archive/Batch010/AgentLogs/Unity_SHINOBU_160_compile_after_hotpath.log` are historical compile/import attempts with unrelated dependency-wall context; they are not current clean compile, Unity import, Play Mode, profiler/GC, live network fault stress, or player-build proof.

## 2026-05-20 SHINOBU_223 Jacobi Power Grid Vault Lane

- Added SHINOBU_223 owner-local Vault buffer IDs `70850..70864` for power nodes, flat edges, node AUPs, CSR offsets/destinations/conductance/flow, potential front/back buffers, demand rates, battery milli-remainders, 300-frame telemetry ring, telemetry cursor, power profiles, and CSV scratch bytes. These IDs are local numeric casts in `PowerGridBufferIds`; they are not central `H8Memory.BufferID` enum additions.
- Primary DTOs: `PowerNodeDTO` is explicit 32 bytes with `NodeHash=0`, `Potential=4`, `MaxCapacity=8`, `CurrentStorage=12`, `Flags=16`, `InternalResistance=20`, and padding `24..31`; `PowerGridEdgeDTO` and `PowerProfileDTO` are explicit 32-byte records; `PowerTelemetryEntry` is explicit 64 bytes for the black-box ring; `PowerGridCounter64` is an explicit 64-byte cursor/control row to avoid false sharing and carry the monotonic forensic frame counter.
- Runtime boundary: `PowerGridManager` requests the Jacobi power Vault lanes during cold boot and DataVault hot-swap recovery. `PowerGridVaultRuntime.EnsureCoreBuffers` persists only `VaultGenerationHandle<T>` descriptors and validates each lane through `GlobalDataVault.TryResolveHandle`; `ValidateCoreBuffers` prevents repeated same-vault descriptor reacquisition, failed `EnsureCoreBuffers` validation releases partially acquired descriptors before returning false, and `ReleaseCoreBuffers` releases lanes through `IDataVault.ReleaseBuffer` on shutdown/DataVault hot-swap. No pointer-bearing `VaultBufferHandle<T>` is part of the new power contract. `LogisticsNetworkGraph` no longer owns the SHINOBU blackbox as a private `NativeArray<PowerTelemetryEntry>`; writes and dumps read manager-owned Vault lanes `70861` and `70862` transiently through `TryGetGenerationHandle` plus `TryResolveHandle`, without graph-owned `GetGenerationHandle` acquisition. Burst jobs receive phase-local `NativeArray<T>` views or raw node pointers and never query `GlobalRegistry` inside solver loops.
- 2026-05-20 hardening addendum: `BuildCsrPowerGraphJob` now applies the same adjacency-capacity cutoff in its write pass as in its prefix-count pass, preventing truncated CSR buffers from overwriting accepted slots. `GenerateMockPowerNetworkJob` refuses to generate nodes if the `NodeAup` lane is absent or shorter than the node lane. Voltage and battery jobs clamp edge reads to the minimum destination/conductance lane length, while current-flow output remains separately write-guarded. Battery and demand jobs sanitize tick delta, carried milli-remainder, request energy, and existing demand before arithmetic, preventing NaN payload propagation into power truth.
- Equipment drain boundary: `ApplyEquipmentPowerDrainJob` consumes `PowerEquipmentLoadRequest`, a 16-byte power-local DTO. Tool-domain `EquipmentGridLoadRequest` rows must be adapted at the signal/Vault boundary; the power Burst contract does not import `Hecton8.Tools`.
- Brownout boundary: base power brownout is a shader scalar route. `PowerGridManager` publishes one global vector through an instance-owned monotonic frame counter, `GlobalShaderDispatcher` sanitizes the resolved shader Vault row before dispatching `_HectonPowerBrownoutParams`, and `Hecton8_UberNoir.hlsl` applies supply dimming and flicker on GPU. The legacy SubmarineOS light/material cache mutation path is removed. The already-touched CBuffer telemetry route also uses `_dispatchTelemetryFrame` instead of `Time.frameCount`.
- Verification status: static source only plus guarded CLI compile attribution. Scoped scan summaries are recorded as clean text only for the SHINOBU_223 Jacobi contract/manager/blackbox files on `VaultBufferHandle`, `GetBufferHandle`, `BufferID.ShinobuPower*`, `.Resolve(`, `.ptr`, private `NativeArray<PowerTelemetryEntry>`, `new PowerTelemetryEntry`, `new PowerNodeDTO`, `new PowerGridEdgeDTO`, explicit `System.Reflection`, `Time.frameCount`, `UnityEngine.Random`, and direct runtime `using Hecton8.Tools|World|AI|Physics|Gameplay|Vehicles|Habitat|Construction|Rendering`; `HectonSubmarineOS` static scan summary is recorded as clean text only for brownout `GetComponentsInChildren`, per-light intensity mutation, and shared-material emission mutation; artifact tuple required before proof reuse. Editor-only `PowerGridLayoutAudit.ValidateAllPowerLayouts` now checks exact offsets for all new power DTO/control/request rows through `UnsafeUtility.GetFieldOffset`, including telemetry alias lanes and the 64-byte cursor. The editor-only Base Power Tuner now exposes Base Wire Conductance, Sump Pump Draw, and Jacobi Smoothing controls; Sump Pump Draw writes the existing drainage tuning DTO rather than widening the power/logistics ABI. Brownout dispatch scan summary is recorded as clean text only for `private void PublishBrownoutSignal`, no stale `static void PublishBrownoutSignal`, finite publisher clamps, `SanitizePowerBrownoutVector` before `_HectonPowerBrownoutParams`, `_dispatchTelemetryFrame`, and no `Time.frameCount` in `PowerGridManager`, `GlobalShaderDispatcher`, or `HectonSubmarineOS`. Existing WFC contract integration in `ShinobuLogisticsRouter`/`WfcOutpostPowerBootRuntime` is legacy scope and not expanded here. Full compile remains blocked by unrelated active dependency walls recorded in `Docs/Tasks/Status_SHINOBU_223.md`; build attempt 4 exposed a generated `Hecton8.Core.csproj` omission for existing Core memory sources, so the local project now includes `GlobalDataVault.cs` and `H8Memory.cs` before `PowerGridJacobiContracts.cs`. Build attempt 5 ran at CPU 33% with no active `dotnet/csc`; the `VaultGenerationHandle<>` error class disappeared, and the build now stops at 62 external missing-symbol errors across WFC/logistics grid, audio, atmosphere, fauna, binary world paging, fluid, Construction socket/docking, content VRAM, scene-transition, culling, runtime-watchdog, and vegetation bridge owners. Build servers were shut down after the attempt.

## 2026-05-19 SHINOBU_145 Physiology Metabolism Vault Lane

- Added SHINOBU_145 owner-local Vault buffer IDs `70265..70275` for metabolism state rows, entity AUPs, exertion speed-squared, species rule rows, row-to-rule indices, 300-frame telemetry ring, live tuning, toxin samples, CSV scratch bytes, staged physiology signals, and staged combat damage signals. These IDs remain local numeric casts and are not added to the global `BufferID` enum.
- Primary DTO: `MetabolicStateDTO` is explicit 32 bytes with `Calories=0`, `Hydration=4`, `CoreTemperature=8`, `Toxicity=12`, `EntityHashID=16`, `Flags=20`, padding `24..31`. Rule, tuning, telemetry, and shader-global DTOs are explicit 64-byte records.
- Runtime boundary: `ShinobuMetabolismRuntime` schedules Burst `MetabolicIntegrationJob` only from `SlowTick` and reclaims the fence from `LateFrameTick` through Core `DispatcherJobFence`; there are no metabolism-owned `Update`, `FixedUpdate`, `LateUpdate`, or direct `JobHandle.Complete()` call sites. Cold boot runs `InitInactiveMetabolismJob` over every resolved capacity row before optional 5000-row mock hydration, so `UninitializedMemory` capacity slack cannot become live metabolism. Starvation/dehydration/hypothermia and toxin damage are staged into Vault buffers `70274` and `70275` by the completed job, then published from `LateFrameTick` through existing `SignalBus<PhysiologyStateSignal>` and `SignalBus<CombatDamageSignal>` via `TryPush`; no Burst job holds `SignalBus<T>.ParallelWriter` past the dispatcher flush boundary. The runtime no longer feature-configures signal lanes; Core `GlobalSignals` remains lane authority.
- Thermal/AUP boundary: thermal grids are queried only through `IThermodynamicsService.TryGetThermalGridReadback`; metabolism subtracts thermal-grid root AUP from entity AUP before local float conversion. Chemical toxin readback samples SHINOBU_138's published Vault buffers `71152`, `71161`, `71162`, and `71163` through explicit 64-byte mirror DTOs, subtracting chemical `GridOriginAup` from entity AUP before local float conversion. Overlay buffer `71153` is sampled only when it can be locked and resolved. No `Hecton8.Thermodynamics` asmdef reference, concrete `AbyssalThermalManager` route, or direct `ChemicalInfluenceGrid` reference is added.
- Scalability boundary: `GlobalQualityWeight` continuously drives cadence via `math.lerp(0.5f, 3.0f, 1.0f - q)` and thermal interpolation weight. Low quality uses nearest thermal lookup; higher quality blends toward trilinear without dropping authoritative entities.
- Human tuning source: project-root `biological_metabolism_profiles.csv` is parsed cold from bytes/`ReadOnlySpan<byte>` into Vault-backed species rules using FNV-1a lowercase hashes and no managed tokenization.
- Dear Lie boundary: freezing presentation exports a scalar fallback plus a 64-byte shader constant buffer; the earlier debug-vector global was removed. No particles, per-status prefabs, or post-process volumes are part of the metabolism route.
- Route card: archived at `Docs/Archive/Batch010/Tasks/Route_SHINOBU_145_Metabolism.md`; no active route-card copy exists. Blackbox dump path: `Docs/AgentLogs/Dump_METABOLISM_SURGEON.bin`.
- Verification status: static source scan summaries are recorded for new SHINOBU_145 files on Unity message loops, managed collections/LINQ, DTO properties, `Pack=`, private persistent NativeArray ownership, direct thermodynamics/chemical-grid concrete types, deterministic Burst flags, `[NoAlias]` pointer fields, uninitialized Vault requests, inactive-slot skip, chemical readback mirrors, optional overlay fallback, dispatcher-fence routing, staged post-completion signal publication, hot-path value-type `new` removal, and absence of stray `Hecton8.World` imports. Stable `.meta` files exist for the new C# assets. Guarded compile was not launched because CPU telemetry exceeded the 50% build gate; Unity import, Burst compile, Play Mode, profiler/GC, shader visual proof, and player-build proof remain pending.

## 2026-05-19 SHINOBU_113 Hydrodynamic KCC Vault Lane

- Added/owns SHINOBU_113 hydrodynamic KCC Vault IDs `70712..70719`, `70743..70749`, and `70751..70752` for states, input packets, proposed velocities, deferred capsule commands, raw hits, previous AUP, visual outputs, telemetry ring/cursor, tuning, fluid profile rows/buckets, rollback bytes, 64-byte fault flags, wake packets, debug outputs, and resolved hit DTOs. No KCC CSV scratch buffer is requested.
- Primary DTO: `KinematicStateDTO` is explicit 64 bytes with `AUP_Position=0`, `Velocity=24`, `AngularVelocity=36`, `Mass=48`, `DragCoefficient=52`, padding `56..63`. Input, tuning, telemetry, wake, collision-hit, debug, fluid-profile, and fault DTOs are explicit 64-byte records; the fault DTO is cache-line padded to prevent false sharing.
- Runtime boundary: KCC owns movement-vector integration and deferred capsule sweep resolution only. Device input remains Core-owned and must enter through `HydrodynamicKccInputDTO` plus `TryRegisterExternalInputWriter(JobHandle)`. Wake output leaves through `SignalBus<WakeGeneratedSignal>`; rollback uses a byte-copy fence and `TryRunRollbackResimulation(...)` without a direct netcode assembly dependency.
- Route card: `Docs/ARCHITECTURE/SHINOBU_113_HYDRODYNAMIC_KCC_ROUTE_CARD.md`. Blackbox dump paths: `Docs/AgentLogs/Dump_SHINOBU_113.bin` and XML-task alias `Docs/AgentLogs/Dump_KINEMATICS_SURGEON.bin`.
- Verification status: static source wiring and documentation are present; guarded compile, Unity import, Burst Inspector, profiler, GCMonitor, Play Mode rollback, and player-build proof remain pending.

## 2026-05-19 SHINOBU_141 SOA Inventory Routing Vault Lane

- Added SHINOBU_141 Vault buffer IDs `73120..73132` for authoritative SOA inventory slots, active slot count, query results, false-sharing-padded query counters, 300-frame telemetry ring, telemetry cursor, tuning, UI double buffers, stack limits, container range claims, container range count, and single-owner container sync result.
- Collision repair: an earlier candidate range `71340..71352` was rejected after static source audit because `AbyssalShadowBufferIds` already owns `(BufferID)71340..71350` in graphics culling. Focused source grep confirms no other source file claims `(BufferID)73120..73132`.
- Primary DTOs: `InventorySlotDTO` is explicit 32 bytes with `ItemHashID=0`, `Quantity=4`, `ContainerAUPHash=8`, `ConditionFlags=16`, `ReservedLock=20`, `_pad0=24`. `InventoryContainerRangeDTO` is explicit 32 bytes with `ContainerHash=0`, `ContainerAUPHash=8`, `SlotStart=16`, `SlotCapacity=20`, `ActiveSlotCount=24`, `StateFlags=28`; `StateFlags` carries `Active`, `SyncFailed`, `CapacityExceeded`, and `Mutating` bits. `InventoryAtomicCounter64` is explicit 64 bytes to block false sharing.
- Runtime boundary: scene-facing `BaseLogisticsNetwork`/`StorageCrate` object scans remain compatibility until their owner supplies stable container hash, AUP, and reservation authority. SHINOBU_141 owns only the data-only bridge and flat SOA query/transaction jobs.
- Compile-wall boundary: runtime source now lives under `Assets/_Project/Scripts/Inventory/Routing/Hecton8.Inventory.Routing.Runtime.asmdef`. That asmdef references Core/Core.Contracts/Core.Memory and Unity packages only; no scene-facing storage, construction, power, logistics, AI, physics, world, rendering, or other sibling runtime asmdef reference is introduced.
- First 20 Minutes moment: resource -> craft/repair/build -> save/load. Proof pending: Unity import/Console, Play Mode Copper Wire route, fabricator query stress, 0B GC hot-path capture, profiler frame sample, save directory diff, and reload same-state verification.
- Verification status: static source scan summaries are recorded for owner-local hot-path forbidden patterns and BufferID collision check. Unity import, Burst compile, profiler, GCMonitor, save/load, and player-build proof remain pending.

## 2026-05-19 SHINOBU_131 Custom SH L2 Probe Grid Payload Lane

- Added/rewired SHINOBU_131 owner-local Vault buffer IDs `0x630800..0x630806` and `0x630808..0x63080C` for front/back custom probe grids, probe light sources, SDF/occlusion cells, tuning, 300-frame telemetry ring, telemetry scratch, mock power, fault flags, CSV scratch bytes, ambient profile rows, and ambient profile count. ID `0x630807` is intentionally unused by the final direct-GraphicsBuffer route; the obsolete half-texture scratch write was removed.
- Primary DTO: `CustomLightProbeDTO` is explicit 128 bytes. Header offsets: `SpatialHash64=0`, `PackedGridCoord=8`, `Flags=12`; SH lanes: `Lane0=16`, `Lane1=32`, `Lane2=48`, `Lane3=64`, `Lane4=80`, `Lane5=96`, `Lane6=112`; last coefficient `B8=120`, tail spare `Spare0=124`.
- Layout note: the XML's literal `double3 + 27 floats in 128 bytes` is impossible (`24 + 108 = 132` before flags). The accepted static route stores the root AUP once in `InteriorGITuningDTO.RootAup` and stores per-probe location as spatial hash/packed grid coordinate.
- Runtime boundary: Unity `LightProbeGroup`, `LightProbes.GetInterpolatedProbe`, `SphericalHarmonicsL2`, `RenderSettings.ambientProbe`, and `m_LightProbeUsage: 1` are statically absent under `Assets/_Project` after this pass. Custom SH data is uploaded through boot-prewarmed double-buffered `GraphicsBuffer.LockBufferForWrite`; the mapped copy is a Burst `UnsafeUtility.MemCpy` job and `_H8CustomLightProbeGrid` is published only after the upload handle is complete and a later frame is reached. The upload scheduler does not start while a simulation handle is active and incomplete, preventing front-buffer read/write races. No half-texture staging path remains.
- Shader boundary: `Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl` declares the matching 128-byte `StructuredBuffer` DTO and quality-scaled SH evaluation helper. Direct project shader ambient now resolves through `_H8CustomLightProbeGrid` instead of Unity `SampleSH`/`SampleSHPixel`; the CPU upload sends runtime-world root separately from the AUP residue/root hash.
- Solver chain: boot initialization schedules `InteriorGIClearStateJob` and optional `GenerateMockProbeGridJob` without a cold `Complete()` fence; runtime simulation schedules `InteriorGIMockPowerJob -> InteriorGIPropagationJob iterations -> UpdateProbeOcclusionJob -> InteriorGITelemetryScanJob`. Occlusion consumes the owner-local SDF/occlusion cell buffer directly and does not introduce a duplicate float SDF payload. Resolution-change clearing is a scheduled `InteriorGIProbeGridClearJob`, not a Tick-path boot-clear fence, and the Vault tuning row is refreshed before the clear is scheduled so the next GPU publication uses current resolution/count constants.
- Human tuning source: `Docs/ambient_lighting_profiles.csv` is parsed cold through a `ReadOnlySpan<byte>` tokenizer backed by Vault scratch into `AmbientLightingProfileDTO` rows; `AbyssalLightingTunerWindow` exposes mock grid generation, CSV reloads, layout validation, a fixed-buffer `SolverCompleteMs` telemetry graph, and Unity probe scan/disable editor controls.
- Compile-wall boundary: `Hecton8.Lighting.asmdef` references Core/Core.Contracts/Core.Memory and Unity packages only. Lighting source static scan has zero direct sibling-domain `using Hecton8.World|Gameplay|Environment|AI|Physics|Audio|Ecosystem|Vehicles|Habitat|Combat`.
- Verification status: static source scans and `git diff --check` pass for SHINOBU_131-owned source/docs. `dotnet build` was not launched per explicit user instruction. Unity import, Burst compile, Play Mode, profiler timing, shader visual proof, and Frame Debugger confirmation remain pending.

## 2026-05-19 SHINOBU_151 Dynamic Point Light Culling Vault Lane

- Added SHINOBU_151 owner-local Vault buffer IDs `71440..71458` for dynamic light source records, cull states, source-count manifest, settings, double-buffered GPU payloads, 300-frame telemetry, radix-sort key/index streams, CSV scratch, profile rules, mock SDF samples, dynamic probe-bounce lights, runtime counters, localized frustum planes, and self-audit data.
- Primary DTO: `LightCullStateDTO` is explicit 32 bytes with `LightHash=0`, `DistanceSq=4`, `BaseIntensity=8`, `ComputedIntensity=12`, `Flags=16`, and explicit pad bytes `20..31`. Source, source-manifest, GPU payload, telemetry, runtime-counter, settings, and profile-rule DTOs are explicit 96/64/64/64/64/128/32-byte records with no `Pack=1`.
- Runtime boundary: dynamic lights are presentation-only. The route evaluates raw Vault records in Burst, sorts importance keys, writes top-N `DynamicPointLightGpuDTO` records to a double-buffered `GraphicsBuffer.LockBufferForWrite` upload, and never toggles or instantiates Unity `Light` objects. Frustum planes are extracted manually from the camera VP matrix without `GeometryUtility` or managed `Plane[]`. Rollback/Merkle state does not own or hash the cull/payload buffers.
- Scalability boundary: `GlobalQualityWeight` and thermal pressure continuously drive culling cadence, active light count `8..64`, distance fade, and near-field overkill gain. Active light budget uses `math.step` as a zero-quality numeric gate, a cubic smooth polynomial, and `math.lerp`; no low/high binary branch is introduced.
- Optional tuning source, currently absent in the checkout: `Docs/Data/light_culling_profiles.csv`. When present, it is parsed cold into Vault-backed `DynamicPointLightProfileRuleDTO` rows through byte-level FNV-1a parsing; missing CSV fails closed to deterministic defaults.
- Route card: `Docs/ARCHITECTURE/SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md`. Blackbox dump path: `Docs/AgentLogs/Dump_LIGHT_DIRECTOR.bin`.
- Latest polish: mock SDF radial wall generation is sqrt-free, source validity is committed through Vault buffer `71458` after source/state writes, uncommitted source/SDF buffers fail closed with count `0`, the 300-frame telemetry ring is cold-cleared for valid blackbox pre-roll, structured GPU payload buffers are prewarmed during native storage setup, probe bounce is published as an owner-local Vault stream instead of directly completing a probe-grid job from the culler, hot DTO lanes use `NativeArrayUnsafeUtility` plus `UnsafeUtility.AsRef` inside the Burst job file, editor/debug readback count now resolves from SourceManifest `71458`, and stable Unity `.meta` files were added for new C# assets.
- Legacy-light archaeology: static scan found no `LightDistanceCull`/light-distance-cull script, but did find gameplay-owned Unity `Light` toggles in player/tool/flare/gravity-trap paths plus `13` authored Light YAML components. Those remain cross-domain migration debt; SHINOBU_151's owner route for those emitters is Source DTO + SourceManifest `71458`, not direct deletion from gameplay owners.
- Verification status: static scan summaries are recorded for owned forbidden patterns, Burst directives, NoAlias fields, explicit DTO layout, uninitialized Vault requests, manual frustum extraction, no direct probe injection, and compile-wall asmdef boundary. Guarded compile, Unity import, Play Mode, profiler timing, GCMonitor, shader visual proof, and Frame Debugger confirmation remain pending.

## 2026-05-19 SHINOBU_150 Babel Subtitle Payload Lane

- SHINOBU_150 treats Babel text authority as hash-indexed UTF-8 byte slabs plus caller-owned `Span<char>` decode. `LocRegistry.ReloadBinaryOrMock(...)` is the Babel reload route; managed `LocalizationManager` string tables no longer hydrate the registry.
- Runtime `Dictionary<string,string>` localization injection is disabled. `LocalizationManager` no longer owns runtime language tables or a JSON parser; legacy JSON parsing remains Editor-only for key/font validation tools.
- Runtime/editor static source paths include `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin`, but this ledger does not promote that asset to runtime-proven load until Unity boot, MMF map, GC, and profiler evidence exist.
- UI text staging uses Vault buffer `(BufferID)70540` for a `char[500 * 512]` Babel UTF-16 arena when the Vault is available. The no-vault fallback is the prewarmed TMP bridge slot, not a private persistent `NativeArray<char>`.
- SHINOBU subtitle state uses owner-local Vault IDs `(BufferID)15070550` for `SubtitleCueDTO[64]` and `(BufferID)15070551` for `LocalizationTelemetryEntry[300]`; both IDs remain domain-local casts and are not added to core enum authority.
- Registry DTO/signals are explicit ARM64-safe layouts: 16-byte localization/subtitle/mock signals, 24-byte `BabelFormatArgs`, 32-byte `BabelDictionaryStage`, and 64-byte `BabelTelemetryEntry`. `LocRegistry` missing-key suppression is a fixed 256-bit bloom mask, not a managed `HashSet`.
- `SubtitleManager` legacy string request queue is now a fixed 8-slot ring. The SHINOBU runtime subtitle path has no `System.Collections.Generic` dependency.
- Legacy `ResolveRaw`/`TryGetRawBuffer` calls use a fixed 16-slot `char[4096]` decode ring, removing the former thread-static grow-on-first-use decode allocation and same-thread double-lookup alias hazard. Hot subtitle decode remains caller-owned `Span<char>`.
- `LocNumericBuffer` numeric localization formatting uses a fixed 16-slot prewarmed `char[4096]` ring for `char[]` compatibility calls. The former thread-static staging buffer, capacity growth watchdog, and `new char[capacity]` overflow route are removed.
- `LocalizationManager` PDA corrosion, madness override, and localized corruption seed buckets now use DSP/audio-frame counters instead of Unity frame time; active windows use wrap-safe `uint` audio-frame comparison.
- Long-lore fallback decode is capped at 4096 glyphs for static audit/debug paths. Megabyte lore must page through encyclopedia/caller-owned spans rather than expanding common subtitle leases.

## 2026-05-19 SHINOBU_135 Dynamic Music Synth Payload Lane

- Static `.wav` music-stem transport is no longer the owned runtime route. `HectonMusicDirector` and `AdaptiveStemAudioMixer` publish scalar context through the 64-byte `DynamicMusicScalarSignal` contract; `Hecton8.Audio.Synthesis` consumes it inside `DynamicMusicGranularSynthesizer`. The only Unity `AudioSource` used by the new route is a one-frame procedural driver clip for `OnAudioFilterRead`.
- Added SHINOBU_135 owner-local Vault buffer IDs `71700..71711` for synth voices, scalar snapshot, tuning, double output buffers, biquad state, 300-frame DSP telemetry, telemetry cursor, CSV scratch, preset rules, grain bank, and shared audio-thread state.
- Primary DTO: `SynthVoiceDTO` is explicit 64 bytes with hot offsets `CurrentPhase=0`, `PhaseIncrement=4`, `EnvelopeState=8`, `SoundHash=12`, `TargetPitch=16`, `TargetVolume=20`, and explicit padding through offset `60`.
- Human tuning source: `Docs/Audio/synth_presets.csv` is parsed cold from bytes into Vault tuning/preset rows. Missing CSV leaves deterministic emergency mock tuning and a generated grain bank active.
- Scalability boundary: `GlobalQualityWeight` continuously drives active voice count and grain-bank interpolation admission. Below q=0.3 the DSP grain sampler resolves the second tap to the base index and zeroes interpolation weight through `math.step`/polynomial math; high/ultra restores smooth fractional grain reads without a separate code path.
- Runtime file-system guard: repeated CSV timestamp polling is editor/development only; shipping player builds do not poll the filesystem from slow tick.
- Signal ingress: scalar context uses `DynamicMusicScalarSignal`; it is now configured as a central direct `GlobalSignals` lane with 64-byte size validation and finite payload guard coverage. Procedural stingers also consume existing `CombatDamageSignal`, `HullDeformedSignal`, and `WaterlineBreachSignal` lanes. No SHINOBU_135-local breach signal was added.
- Compile-wall boundary: synth runtime moved under `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic` and editor facade under `Assets/_Project/Scripts/Audio/Synthesis/Editor`. Legacy Core audio code does not reference the synth type; it routes through `Hecton8.Core.Contracts.Signals`.
- Runtime boundary: music is presentation-only and must not enter rollback Merkle state. DSP samples are generated by Burst jobs into double-buffered Vault output arrays; `OnAudioFilterRead` only copies the ready buffer and zeros underruns.
- Adjacent audio synthesis hygiene: `DepthStressGranularSynthesisKernel.cs` in the same `Hecton8.Audio.Synthesis` asmdef now uses exact mandated Burst flags on all five Burst jobs, `[NoAlias]` on NativeArray job fields, and direct public-field assignment instead of Burst job struct object initializers.
- Vault alias refresh: `DynamicMusicGranularSynthesizer` resolves its runtime `NativeArray` views and raw output pointers through generation-checked `VaultBufferHandle<T>` records before buffer reuse. During an active Vault compaction fence it preserves already-created aliases and does not call the fenced `ResolveBuffer` path.
- Verification status: static source check and diff summaries are recorded as local text only for edited SHINOBU_135 files after compile-wall isolation. Static dependency search found no Core/legacy-audio direct reference to `DynamicMusicGranularSynthesizer`. Unity import, Burst compile, profiler, GC allocation capture, and Play Mode DSP timing proof remain pending until a guarded compile/runtime pass is executed.

## 2026-05-19 SHINOBU_136 Kinetic Character Matrix Payload Lane

- Unity `Animator`, Animation Rigging, and `ContextualPhysicalIkRig` are no longer the owned player animation route. Player presentation scalars feed a Burst/Vault procedural matrix solver that writes `float4x4` bone matrices to Vault and GPU buffers.
- Added SHINOBU_136 owner-local Vault buffer IDs `13671360..13671371` for rigs, frame inputs, parent indices, bind poses, bone outputs, final matrices, IK targets, frame stats, 300-frame telemetry, telemetry cursor, tuning, and CSV scratch.
- Primary DTO: `ProceduralBoneDTO` is explicit 64 bytes with `LocalToWorld` at offset `0`, matching one cache-line matrix stride. `ProceduralIKTargetDTO` is explicit 32 bytes with target position, weight, pole/normal, and flags kept in a separate stream.
- Frame input DTO: `KineticCharacterFrameInputDTO` is explicit 272 bytes after the active-tool identity fence; `ActiveToolHash=248`, `Frame=252`, `Flags=256`, `_pad0=260`, `_pad1=264`, so total size remains 16-byte aligned.
- Human tuning source: `Assets/_Project/Data/character_rig_constraints.csv` is parsed cold through byte/span FNV-1a logic into Vault tuning and rig rows. Missing or invalid source leaves the deterministic emergency mock humanoid rig active.
- Runtime boundary: the kinetic route consumes `BufferID.PlayerKinematicState` for root AUP/velocity, optional `BufferID.VoxelSdfTexture3D` for hand bracing, and submitted presentation/tool scalars from the player bridge. Solver frame identity is runtime-owned to avoid Unity `Time.frameCount` leakage, and the active tool hash is cached by `PlayerToolManager`, submitted by the swim presentation bridge, and carried into Burst state hashing without importing Equipment runtime types.
- Verification status: static scan summaries are recorded for edited SHINOBU_136 source on Unity `Animator` type usage, Animation Rigging, `Physics.Raycast`, DTO properties, `Pack=`, Unity random, LINQ/foreach/string formatting, hot-path native allocation patterns, hot `math.sqrt`, runtime `AddComponent<KineticCharacterAnimatorRuntime>`, null `kineticMatrixRuntime` prefab wiring, and unguarded SDF cell-size division. The editor tuner now uses `UnsafeUtility.GetFieldOffset` for DTO offset proof, Player prefab owns one serialized kinetic matrix runtime component for script GUID `bd250538668144e4888c05624ddbaf9f`, the raw GPU matrix upload helper is constrained to `where T : unmanaged` before `UnsafeUtility.MemCpy`, Task 11 tool identity is no longer a literal-zero bridge, and DataVault hot-swap now clears GPU skinning bindings before buffer reacquire. Compile remains blocked by the AGENTS CPU gate at 100 percent CPU; Unity import, Burst Inspector, profiler, GCMonitor, shader skinning proof, and player-build proof remain pending.

## 2026-05-19 SHINOBU_147 Surface Weather Wave Payload Lane

- Added SHINOBU_147 owner-local Vault buffer IDs `70769..70774` for targeted wave readback query/results, completed query mirrors, 3-slot query ring, Beaufort profile tuning, and surface swell vector export. Existing `70760..70768` remain the ocean wave/weather/atmosphere/reserved-mock/telemetry/lod scratch lane.
- Primary DTO: `WaveParametersDTO` is 64 bytes with explicit float4 offsets `Wave1=0`, `Wave2=16`, `Wave3=32`, `GlobalWindAndStorm=48`; two records carry six Gerstner lanes for shader/compute evaluation.
- Camera-derived phase DTO: `OceanWaveAupPhaseDTO` is 64 bytes with `PhaseBase0=0`, `PhaseBase1=16`, `CameraAupLocalXZ=32`, `Frame=48`, `Flags=52`, `GlobalQualityWeight=56`, `ActiveWaveCount=60`. It is recalculated from AUP and uploaded as shader/compute constants, not stored as persistent Vault truth.
- Secondary DTO: `BeaufortProfileDTO` is 64 bytes with explicit offsets `StateHash=0`, `BaseSteepness=4`, `BaseWavelength=8`, `WindSpeed=12`, `StormIntensity=16`, `FoamThreshold=20`, `FrequencyScale=24`, `Flags=28`, `Reserved0=32`, `Reserved1=48`.
- Runtime boundary: surface visual displacement is GPU-owned; CPU physics-facing consumers receive only delayed targeted `AsyncGPUReadback` samples through `IHectonOceanKinematics`/Vault buffers. No shipped binary payload is claimed.
- Blackbox fault export: wave/readback telemetry dumps to `Docs/AgentLogs/Dump_SHINOBU_147.bin`.
- Readback ownership: the targeted wave sampler uses three slot-owned query/result `GraphicsBuffer` pairs, matching the 3-frame `AsyncGPUReadback` ring; no pending slot shares its result buffer with a newer dispatch.
- Quality fault boundary: C#/HLSL wave evaluation sanitizes `GlobalQualityWeight` so exact `0.0` remains minimum survival and non-finite input fails closed to `0.0`, not Ultra workload.
- Shader consumer: `Hecton_StormOceanSurface.shader` includes `Hecton_OceanSurfaceAtmosphere.hlsl` and calls `H8EvaluateOceanSurface()` in the vertex stage; scene/material binding proof remains pending.
- Runtime hygiene: hot `Tick` uses cached Vault handles only, readback dispatch refuses to cold-create GPU buffers, pending readback disposal is nonblocking, and fault dumps are deferred to late diagnostics.
- Human tuning source: optional `beaufort_scale_profiles.csv` is parsed cold from bytes/`ReadOnlySpan<byte>` into the Vault-backed Beaufort table; missing file leaves mock/tuner defaults active.
- Verification status: static source scans were reported for edited surface domain sync-readback, CPU editor fallback, CPU buoyancy-query contracts, `Pack=1`, and DTO-property bans. No fresh R34 compile artifact tuple is linked here; guarded `Assembly-CSharp.csproj` compile status remains pending until a command, timestamp, environment, and output are attached. Unity import, shader compile, profiler, and GC proof remain pending.

## 2026-05-19 SHINOBU_127 Ballistics Vault Lane

- Added SHINOBU_127 owner-local Vault buffer IDs `71270..71279` for double-buffered ballistic trajectories, AABB primitives, hit results, penetration LUT, telemetry ring, counters, tuning, impact VFX staging, and CSV scratch.
- Primary DTO: `BallisticTrajectoryDTO` is 64 bytes with explicit offsets `0/24/36/40/44/48/52/56/60`, matching the armor-penetration XML contract and one L1 cache-line stride.
- Runtime boundary: hostile flora fire authority now queues mathematical trajectories; physical projectile `Rigidbody`/collision callbacks are retained only as legacy prefab facade compatibility, not damage authority. `HostileFlora` target acquisition is Core registry based; its unused player-layer mask inspector surface was removed.
- Compile-wall boundary: touched fire-path files route through Core registry/contracts only; `HostileFlora` target acquisition uses `GlobalRegistry.Player` instead of `Hecton8.World.WorldRuntimeReferenceUtility`, and firing audio uses Core `IAudioService` rather than an Audio namespace dependency.
- Fire-source authority: hostile flora and the legacy facade fold Unity entity IDs through Core `GlobalSignals.FoldEntityIdToSourceId`; RNG salt is separate from damage provenance. Flora spread uses `Unity.Mathematics.Random` seeded from AUP-derived sector hash, next ballistic simulation frame, and source salt; no local shot counter participates in rollback-critical seed state.
- Human tuning source: `Data/Balance/armor_penetration_matrix.csv` hydrates the 8x8 Vault LUT through a cold span parser; oversized or malformed CSV files fail closed and do not partially mutate live LUT state.
- Compile boundary: owned ballistics runtime no longer imports `Hecton8.World`; AUP conversion uses Core `HectonFloatingOrigin`.
- Latest static polish: primitive reach rejection is sqrt-free before exact rotated slab math; solver Vault lock rollback releases only buffers acquired by the current lock attempt; trajectory buffer helper names now match the write-to-solver-read phase; velocity queueing now passes resolved `float3` directly into a Vault pointer/ref writer; scheduled jobs resolve Vault-backed DTOs through `NativeArrayUnsafeUtility` plus `UnsafeUtility.AsRef<T>` rather than `NativeArray` indexers; queue/slab/mock/CSV arithmetic uses guarded `rsqrt`/`rcp`/reciprocal constants and power-of-two bit shifts; hostile flora cooldown/aim cadence uses the dispatcher 10 Hz slow-tick contract instead of stale `0.5f`; `GlobalQualityWeight` faults now fail closed to `0.0f` through shared runtime sanitizer/smoothing helpers used by signal budgeting, solver smoothing, VFX scale, and telemetry counters; limb-admission floor is clamped below the `smoothstep` upper edge; stale HostileFlora inspector text about spawned projectile authority was purged; owned scans currently show no `UnityEngine.Random`, no `Mathf.`, no `Time.deltaTime`/`Time.frameCount`, no `math.normalize`, no raw slash/sqrt/magnitude arithmetic hits, no owned ballistic buffer indexer matches, and all remaining `math.rsqrt` calls guarded by `math.max`.
- Verification status: prior static source scans and owner-local build text are historical unless their artifact tuple is linked. The latest documented R34 boundary treats this lane as `STATIC_SOURCE` only; current project-file scans still reference absent archive sources in `Assembly-CSharp.csproj` (`Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`). Fresh compile proof requires command, timestamp, environment, and output before reuse.

## 2026-05-19 SHINOBU_143 Tether AUP Vault Lane

- Added SHINOBU_143 Vault buffer IDs `71280..71293` for AUP tether nodes, constraints, endpoints, spline vertices, force packets, telemetry ring/head, cable materials, CSV scratch, bootstrap state, segment tensions, solver stats, pinned endpoint AUPs, and pinned masks.
- Primary DTO: `TetherNodeDTO` is 64 bytes with explicit offsets `0/24/48/52/56`, matching one cache-line node stride.
- Intended tuning source, currently absent/unresolved in the checkout: `cable_materials.csv`. Parser path/fallback must be documented before treating it as a live source; intended cold ingestion reads bytes, hashes names with FNV-1a, and writes SHINOBU cable material rows into fixed Vault-owned open-address slots under `Shinobu143CableMaterials`.
- Runtime boundary: mock bootstrap/parser/scheduler paths are static/source orientation only; `Dump_CABLE_SURGEON.bin` is the explicit SHINOBU_143 fault export path. Compile proof is blocked by unrelated Visor/Somatic/Equipment missing DTO contracts recorded in the archived log `Docs/Archive/Batch010/AgentLogs/LOG_SHINOBU_143.md`; the active `Docs/AgentLogs/LOG_SHINOBU_143.md` copy is absent after Batch010 archival.

## 2026-05-19 SHINOBU_132 Tether And Cable Physics Vault Lane

- Added SHINOBU_132 owner-local Vault buffer IDs `71320..71332` for cable nodes, constraints, spline vertices, segment tensions, physics event mirror, 300-frame telemetry ring/head, pinned endpoint AUPs/masks, tuning, cable materials, bootstrap state, and endpoints. These IDs remain local numeric casts and are not added to the global `BufferID` enum.
- Primary DTO: `CableNodeDTO` is explicit 64 bytes with `CurrentAUP=0`, `PreviousAUP=24`, `InverseMass=48`, `Flags=52`, and byte padding `56..63`.
- Runtime boundary: SHINOBU_132 solver is a Burst/Vault data kernel scheduled from `TetherManager`; it does not own scene Rigidbodies or apply forces directly. Tension leaves through existing `SignalBus<PhysicsEventPayload>` as `PressureImpulse` plus SHINOBU_132 status bit.
- Dear Lie boundary: physical cable truth is 5 mock cables x 50 nodes; visual extraction writes 10..64 Catmull-Rom spline vertices per cable based on continuous `GlobalQualityWeight` and editor tuning, avoiding extra simulated nodes.
- Fluid boundary: `TetherManager` may sample `GlobalRegistry.Fluid.TrySampleModAbyssalFlow` once outside Burst and passes the finite vector as input data. The Burst solver retains deterministic sinusoidal current as fallback/noise and never performs service lookups.
- GPU upload boundary: spline vertices upload through `TryBeginSplineVertexUpload` and `CableSplineUploadTicket132`; finalization uses completed-handle polling before `UnlockBufferAfterWrite`, not an immediate force-complete. Draw arguments use `TetherSplineIndirectArgsDTO` (16 bytes) and a separate Burst job for `DrawProceduralIndirect` arguments.
- Human tuning source: `cable_materials.csv` is parsed cold from bytes/`ReadOnlySpan<byte>` into a Vault-backed fixed open-address material table; the editor facade reads the file into Temp `NativeArray<byte>` via `FileStream.Read(Span<byte>)`, not `File.ReadAllBytes`. Live tuning DTO controls gravity, drag, max iterations, break force, and spline vertex budget without C# recompilation.
- Blackbox fault export: `Docs/AgentLogs/Dump_SHINOBU_132.bin` and task-required alias `Docs/AgentLogs/Dump_CABLE_SURGEON.bin`.
- Stable `.meta` GUIDs exist for `CablePhysicsSolver132.cs`, `CablePhysicsDebugGizmo132.cs`, and `Shinobu132CablePhysicsTunerWindow.cs`.
- Verification status: static source scan summaries are recorded for SHINOBU_132 Core-residue removal, first-party Unity joint removal, cable-domain LineRenderer removal, deterministic Burst flags, per-cable spline indexing, ticketed GPU upload surface, and no managed byte[] CSV staging in the SHINOBU_132 tuner. Guarded compile, Unity import, Burst Inspector, profiler, GCMonitor, and visual draw proof remain pending.
- 2026-05-20 polish: `CableNodeDTO*` Burst job fields now carry explicit `[NoAlias]` proof; `CaveBioRootsGenerator` no longer creates or updates `LineRenderer` children and routes bio-root visuals through `ConnectionSplineBatchRenderer` descriptors instead. Guarded `dotnet build --no-restore` remains blocked by missing generated `Temp/obj/*/project.assets.json`, stale Unity-generated `.csproj` inclusion for untracked SHINOBU files, and unrelated cross-domain compile errors.
- 2026-05-20 second polish: SHINOBU_132 no longer reconfigures `SignalBus<PhysicsEventPayload>` from the scheduling path, fixed-tick mock finalization uses `DispatcherJobFence.TryFinalizeCompleted`, camera AUP is derived from the player movement AUP owner plus local camera offset, legacy tether spline/GPU jobs use deterministic Burst flags, legacy tether packet flushing uses `ForceMode.Acceleration` instead of steady-state `ForceMode.Force`, `TetherManager` no longer stores private `NativeArray` telemetry aliases, and `CaveBioRootsGenerator` routes spline visuals through cached `IConnectionSplineBatchRendererService` instead of static renderer wrappers.
- 2026-05-20 continuation polish: active `CURRENT_BATCH.md` no longer contains the SHINOBU_132 XML block, so the persisted SHINOBU_132 route card/logs plus explicit user assignment remain the narrow authority. `TetherManager` now caches player camera/movement during cold dependency refresh and no longer polls `GlobalRegistry.Player` from fixed-tick AUP derivation. `CablePhysicsDebugGizmo132` resolves the active `GlobalRegistry.DataVault` instead of a latest-created Vault singleton. Legacy `TetherInstance` player reaction applies mass-normalized `ForceMode.Acceleration`, `TetherVisualGpuSplineCopyJob` uses deterministic Burst mode, and origin-shift visual fallback no longer exports a mutable `ref NativeArray<float3>` from `TetherInstance` into `TetherManager`.
- 2026-05-20 legacy scheduling polish: `TetherInstance.RunVerletSolver` no longer uses synchronous `.Run()`/`.Execute()` for integration, constraint, or telemetry work. It schedules integration -> constraint -> telemetry, stores the pending handle, finalizes through `DispatcherJobFence`, and blocks visual buffer reads while a solve is pending. The old unscheduled `TetherVisualGpuSplineCopyJob` was removed because it was only invoked through direct `Execute(i)` calls. Residual debt remains: `TetherInstance` still keeps Vault-resolved private `NativeArray` aliases and needs a larger generation-handle/view rewrite before H-Phi can be claimed for that legacy monolith.

## 2026-05-19 SHINOBU_148 Equipment Thermal/Battery Vault Lane

- Added SHINOBU_148 Vault buffer IDs `71300..71315` for active equipment state, published state, tool AUP samples, grid load requests, telemetry ring/cursor, padded integration counters, CSV scratch, tuning, hardware specs, dump scratch, tool state/stats/type/status/environment mirrors.
- Primary DTO: `ActiveEquipmentDTO` is 32 bytes with explicit offsets `ToolHashID=0`, `CurrentBattery=4`, `ThermalLoad=8`, `StateFlags=12`, `PowerDrawRate=16`, `HeatGenerationRate=20`, padding bytes `24..31`.
- False-sharing guard: `EquipmentIntegrationCounters` is explicit 64 bytes; each parallel worker writes its own cache-line slot, then the owner aggregates after the late-frame fence.
- Runtime boundary: battery drain, active heat generation, water cooling, and ambient thermal-grid exchange are now centralized in a deterministic Burst `IJobParallelFor`; tool scripts only mark active intent and consume published readback.
- Flashlight boundary: `PlayerFlashlight` no longer falls back to `HectonSurvivalSystem` for battery readback; charge is visible only through the bound `IBatteryTool` adapter backed by `ModularEquipmentEngine`.
- Seaglide boundary: `MantaScooter` no longer subtracts local charge or drains inventory condition; it publishes only active intent plus requested draw rate through `IModularEquipmentService.SetToolActive(toolId, active, drainRate)`.
- Tool frame boundary: `HarpoonLauncherTool` no longer uses `LateUpdate()` for tracer presentation; it registers an `ILateFrameTickable` dispatcher lane and keeps tracer drawing outside battery/heat authority.
- Activity intent boundary: base hold tools no longer set sticky external active masks. `PlayerTool` publishes a 0.075s dispatcher-advanced runtime intent after accepted use, while continuous/toggle tools keep explicit `SetToolActive` ownership.
- Cold init boundary: SHINOBU_148/224 equipment Vault spans are requested with `NativeArrayOptions.UninitializedMemory`, now through `GetGenerationHandle<T>` plus `TryResolveHandle` rather than direct `GetBuffer<T>` external views, and cleared by deterministic Burst `ClearActiveEquipmentNativeStateJob`; no private Persistent NativeArray fallback owns thermal/battery truth.
- Hot lookup boundary: `ModularEquipmentEngine` and `PlayerTool` cache registry services through hot-swap listeners; `LaserCutter` and `FlashlightTool` consume protected cached accessors for runtime equipment/submarine/player dependencies instead of polling `GlobalRegistry` in tool use paths. The latest SHINOBU_224 polish extends the `PlayerTool` cache to durability, input, interaction-signal, and player-inventory services, so active-equipment durability readback, overcharge checks, queued tool raycast helper calls, and overcharge inventory removal do not perform live `GlobalRegistry` reads.
- Brownout readback boundary: tool brownout flicker is now exposed through `IModularEquipmentService`, so `PlayerTool` does not cast to the concrete `ModularEquipmentEngine` for hot readback.
- SHINOBU_224 polish made the tuning source live at `Assets/_Project/Data/Tools/tool_hardware_specs.csv`. Cold ingestion reads bytes/`ReadOnlySpan<byte>` and writes unmanaged spec rows into `ShinobuActiveEquipmentHardwareSpecs`; parser keys may be numeric/hex runtime hashes or FNV-1a name hashes, and runtime matching checks `RuntimeToolId` plus cached `RuntimeToolSpecHashId` to bridge the legacy `Animator.StringToHash` tool IDs.
- SHINOBU_224 signal polish removes the equipment-owned overheat/depleted `NativeQueue` buffers. `EquipmentStateIntegrationJob` writes threshold-edge payloads directly into typed `SignalBus<T>.ParallelWriter` lanes, so post-fence work no longer performs an extra queue drain/re-publish pass.
- SHINOBU_224 compile-wall polish removes the direct `Hecton8.Power` telemetry listener/event dependency from `ModularEquipmentEngine`; brownout feedback now uses cached Core `IPowerGridService` scalar reads only.
- Verification status: static source scan summaries are recorded for edited SHINOBU_148/224 surface on `Pack=1`, hot-path `new NativeArray`, `NativeHashMap`, LINQ, foreach, `Time.deltaTime`, direct per-frame battery/heat drains, direct local charge decrements in battery tools, Unity `Update/FixedUpdate/LateUpdate` methods in `PlayerTool` surface files, private equipment overheat/depletion `NativeQueue` allocation, direct `GetBuffer<T>` use in `ModularEquipmentEngine`, direct runtime `using Hecton8.Power|Hecton8.World`, and hot-path `PlayerTool` reads of `GlobalRegistry.ToolDurability/Input/InteractionSignals/PlayerInventoryRuntime`. A guarded `dotnet build Hecton8.Core.csproj --no-restore --nologo /clp:ErrorsOnly /p:UseSharedCompilation=false` was launched at CPU 21.31 percent with no `dotnet/csc`; it failed in 24.75s with 230 cross-domain errors before SHINOBU_224 acceptance could be proven (`Hecton8.Logistics.Grid`, `VaultGenerationHandle<>`, `SoundEmissionSignal`, `H8BinaryWorldPager`, docking/world/audio bridge types, and other non-equipment symbols).

## 2026-05-19 SHINOBU_139 Procedural Coral Rule Payload Lane

- `Assets/StreamingAssets/coral_growth_rules.h8bin` is not present in this checkout. SHINOBU_139 added a cold direct StreamingAssets lookup plus project-tree reconnaissance, then a deterministic integer-opcode emergency rule generator.
- Coral Vault buffer IDs `71390..71409` cover rules, instruction scratch, branches, turtle stack, spatial cells, render matrices, indirect args, sector triggers, capsule collision proxies, sync pulses, telemetry ring/cursor, tuning, CSV scratch, counters, debug segments, GPU sway scalars, self-audit, and CPU HZB tiles.
- Primary DTO: `CoralBranchDTO` is 128 bytes with explicit offsets `LocalMatrix=0`, `PrefabHash=64`, `GenerationDepth=68`, `SectorAUP=72`, `Stiffness=96`, `Radius=100`, `StateFlags=104`, `ParentIndex=108`, `StableId=112`, `SectorHash=116`, tail padding `120/124`.
- Layout proof boundary: editor validation asserts critical offsets for branch, rule scalar, telemetry, counter fault, GPU sway, and self-audit DTO fields, not only total sizes.
- Rule hydration boundary: CSV/H8BIN rules stage through a 16-record stack scratch and commit only when at least one valid rule is parsed; corrupt/empty rule files preserve the previous live grammar.
- Zero-init boundary: first hydration writes only small sentinel records, fallback rules, tuning, and `CoralPaddedCounterDTO.EffectiveQualityWeight`; large uninitialized coral buffers are not blanket-cleared and are consumed only through logical count windows.
- Quality boundary: sector-trigger/tuning quality is resolved once by the generation stage, stored in the 64-byte counter at offset 60, and consumed by constraint, render extraction, bioluminescence pulse staging, and collision proxy staging. Exact `0.0f` quality is valid and does not fall back to tuning defaults.
- Fault boundary: solver and constraint faults are accumulated in `CoralPaddedCounterDTO.FaultFlags` and carried into the final self-audit result before audit-local faults are OR-ed in.
- Rule-scalar boundary: CSV/H8BIN rule `BranchAngleRadians`, `LengthScale`, and `RadiusScale` fields are finite-clamped before commit and consumed per opcode by the integer interpreter; bad content cannot inject unbounded branch length/radius growth.
- NaN boundary: turtle rotation, step, radius, stiffness, local matrix/AUP publication, HZB extraction radius/matrix prechecks, telemetry measurement overwrites, pulse output, proxy output, and audit sector/radius/overlap probes now use finite-first guards before writing Vault windows.
- GPU upload boundary: `UploadFromVault()` is no-grow by default, unlocks mapped matrix/indirect-args buffers through `try/finally`, and writes the live branch window's `SectorHash` into Vault `CoralGpuSwayDTO` alongside shader sway scalars. Current shader globals expose only the float4 sway vectors.
- GPU dispatch hardening: the upload facade clamps uint instance counts against matrix capacity, forces nonzero vertex count, skips zero-instance draws, and finite-checks shader sway vectors before publishing globals.
- GPU prewarm boundary: explicit prewarm clamps capacity to the coral matrix budget and releases partial buffers if cold `GraphicsBuffer` creation fails.
- Runtime boundary: static source only. Unity import, Burst compile, H8BIN load success, CSV hot reload, renderer draw route, profiler, and runtime GC proof remain pending.

## 2026-05-19 SHINOBU_149 Dynamic Decal Profile Lane

- Added SHINOBU_149 dynamic deferred decal Vault buffer IDs `71490..71496` for the decal instance ring, upload scratch, runtime state, 300-frame telemetry, tuning, material profile table, and CSV scratch.
- Primary DTO: `DecalInstanceDTO` is 80 bytes with explicit offsets `LocalToWorld=0`, `MaterialHash=64`, `Opacity01=68`, `LifetimeSeconds=72`, `Flags=76`. Offset 72 carries profile/tuning lifetime so CSV lifetime rows affect decay without expanding the shader ABI.
- Intended tuning source, currently absent in the checkout: `Assets/_Project/Data/Decals/decal_material_profiles.csv` / `decal_material_profiles.csv`. Intended cold ingestion reads bytes into Vault scratch, hashes source names with FNV-1a, and writes atlas/lifetime/radius/depth records into a fixed Vault-owned open-address table. No generated binary payload is claimed.
- Post-audit hardening: high-speed and combat-damage signal lanes now keep independent frame cursors with frame-zero sentinels; request admission is capped at the 1024-entry prewarmed queue budget with saturating dropped-request telemetry; runtime overkill capacity is capped by the render feature buffer budget with a 128-decal low floor; player layout validation uses size-only checks while exact offset reflection stays editor-only; upload stalls patch the current telemetry row and immediately emit the black-box dump; visual sync locks the full Vault mutation envelope before signal ingestion, while tuning writes, CSV profile ingest, fault marking, upload telemetry patching, black-box reads, and editor/debug snapshots use dedicated lock envelopes; mapped GraphicsBuffer upload count is clamped by the real buffer count and Vault upload scratch length; effective quality is smoothed in runtime state so active decal count sheds over frames instead of one-frame truncation; legacy `Assets/Dynamic Decals` object-decal package was deleted after `_Project` reference scans proved no external GUID/user references.
- Runtime boundary: static source only. Unity import, shader compilation, Frame Debugger, profiler, and runtime GC proof remain pending. Narrow `Hecton8.Core.csproj` build was run after CPU dropped below the gate; SHINOBU_149 file inclusion was fixed, and the remaining build errors are unrelated missing DTO/namespace dependencies in other domains.

## 2026-05-19 SHINOBU_134 Abyssal Shadow Culling Vault Lane

- Added SHINOBU_134 owner-local Vault buffer IDs `71340..71350` for shadow instances, cull states, illumination scalars, localized frustum planes, padded counters, 300-frame telemetry, runtime tuning, profile rules, CSV scratch, HZB depth tiles, and indirect draw args.
- Primary DTO: `ShadowCullStateDTO` is 32 bytes with explicit offsets `InstanceHash=0`, `DistanceSq=4`, `CullFlags=8`, `IlluminationScalar=12`, and padding bytes `16..31`, matching the XML assignment's ARM64 layout contract.
- False-sharing guard: `ShadowCullCountersDTO` is explicit 64 bytes and carries HZB/SDF/visible-shadow/profile/hash fields without sharing a cache line with unrelated counters.
- Runtime boundary: shadow culling is presentation-only, AUP-localized, and excluded from rollback authority through cull flags and owner-local Vault buffers. Simulation schedules the Burst handle; VisualSync only uploads completed state and indirect args through double-buffered `GraphicsBuffer.LockBufferForWrite`, with mapped ranges unlocked through guarded `try/finally` blocks.
- Determinism boundary: point-light shadow culling uses an instance-stable deterministic hash with previous-state budget hysteresis; the SHINOBU_134 runtime no longer falls back to Unity `Time.frameCount`, and point-light admission is no longer rerolled every frame.
- Hysteresis boundary: `EvaluateShadowCullingJob` reuses the prior `ShadowCullStateDTO` only when `InstanceHash` matches, previous `DistanceSq` is finite/positive, and the previous row is not faulted; it then applies 3-5 m distance/frustum bands plus scalar darkness/SDF/radius/point-budget bands. This preserves the 32B state ABI and avoids a second history buffer.
- Producer boundary: Lighting/HZB/World owners may fill the existing Vault input buffers through the SHINOBU_134 producer facade and register their producer `JobHandle`; the culling runtime combines that dependency before evaluation and suppresses fallback mock data when external instance/HZB data is marked resident. No direct sibling runtime assembly reference is introduced.
- Allocation boundary: producer/tuner/CSV/snapshot paths resolve Vault buffers only. GPU upload buffers are cold-prewarmed when the runtime enables with a Vault available and are otherwise ensured by simulation/VisualSync publication, not by external producer access.
- Vault lock boundary: culling schedules only after all job buffers are acquired through `TryLockBuffer`; a partial lock failure releases only the acquired subset, records `TelemetryFlagVaultLockFailed`, preserves producer handoff state, and returns the incoming dependency without scheduling contested writes.
- Mock proof boundary: the editor/CI `RunMockCullingOnce()` facade now fails closed when lock-failfast prevents scheduling; it no longer treats an empty `CompletePendingJob()` path as a successful 50k stress pass.
- HZB mock ALU boundary: fallback HZB tile generation uses squared radial dot products instead of `math.length`, keeping the mock occlusion lane sqrt-free.
- Layout proof boundary: DTO layout reflection lives only in the SHINOBU_134 Editor facade; runtime culling source no longer carries `AbyssalShadowLayoutAudit` or `typeof(T).GetField` validation code.
- Shader dither boundary: `Hecton_AbyssalShadowDither.hlsl` gates Bayer clipping on `DitherFadeActive`; admitted non-fading casters keep solid shadows while fade-band casters dissolve through the Dear Lie.
- CSV reload boundary: profile CSV reloads fail closed on zero valid rows and preserve the previous live Vault profile table; successful shorter files clear only stale tail rows after parse proof.
- CSV transaction boundary: profile CSV bytes are validated in a no-commit pass first; malformed non-comment rows or capacity overflow reject the reload before the live Vault profile table is mutated.
- CSV scalar boundary: byte-level float parsing requires full token consumption, so numeric prefixes with trailing garbage are rejected before profile commit.
- Scheduled-reader boundary: frustum-plane mutation, profile CSV reload, and runtime tuner writes refuse changes while `_jobPending` is true, preventing editor/control facades from racing Burst readers or skewing completion telemetry over Vault arrays.
- HZB basis boundary: external HZB readback producers must set the same camera-local right/up/forward basis used to generate the depth tiles; the Burst culler maps candidates with dot products against that basis instead of assuming world-axis `xy/z` screen space.
- Human tuning source: optional `Docs/Tasks/shadow_culling_profiles.csv` hydrates unmanaged profile rules through the Vault CSV scratch buffer with byte-level FNV-1a parsing; missing file leaves default rules active.
- Verification status: static source scan summaries are recorded for owned SHINOBU_134 files on `Pack=1`, DTO properties, LINQ/foreach/new NativeArray, `Renderer.shadowCastingMode`, `math.sqrt`, Unity random, Unity `Time.`, Burst flags, and diff whitespace. Unity import, Burst Inspector, shader compilation, Frame Debugger, profiler, GC proof, and player build remain pending. Full build is intentionally not rerun until technically needed and unrelated project dependency failures are unresolved.

## 2026-05-19 SHINOBU_157 Autopilot SDF Feeler Payload Lane

- Added SHINOBU_157 owner-local Vault buffer IDs `71592..71603` through `SubmarineAutopilotVaultRoute` for autopilot states, avoidance summaries, 32-feeler debug rows, waypoint rows, route cursors, tuning, 300-frame telemetry, telemetry cursor, mock encoded SDF, flow samples, CSV scratch, and handling profile rows. These IDs are intentionally not added to the global `BufferID` enum.
- Primary DTO: `AutopilotStateDTO` is explicit 64 bytes with offsets `TargetAUP=0`, `DesiredVelocity=24`, `TargetSpeed=36`, `SubmarineHashID=40`, `NavFlags=44`, `_pad0=48`, `_pad1=56`.
- Layout guard: editor-only `AutopilotStateDTOLayout.ValidateAll()` checks exact state, avoidance, feeler, waypoint, route, tuning, telemetry, and handling profile DTO size/offset contracts; reflection remains outside player/runtime. `AutopilotTuningDTO` is 128 bytes and uses offset 120 as `ResolvedQualityWeight`, with offset 124 retained as padding.
- Runtime boundary: Burst jobs sample encoded Voxel SDF bytes and abyssal flow samples from Vault and publish only `DesiredVelocity`; kinematic vehicle integration remains owned by the vehicle motor. No NavMesh, A*, `Physics.Raycast`, `Physics.SphereCast`, Transform movement, or Rigidbody mutation is part of this route.
- Route ingress: external owners seed routes through `TryWriteRoute(int, ReadOnlySpan<AutopilotWaypointDTO>, float, uint)`, which writes fixed Vault waypoint slices from resolved active capacity and route ranges with named active flags, without managed lists, path nodes, or a Logistics assembly dependency.
- Quality boundary: `AutopilotTuningDTO.GlobalQualityWeight` is now the authored cap, not a value overwritten by thermal pressure. Scheduler writes `ResolvedQualityWeight = quantize_0.001(min(HomeostasisBrain.GlobalQualityWeight, cap))` and Burst jobs consume that frozen scalar for feelers `5..32`, steps `1..12`, solver cadence `12..1` fixed ticks, nearest/trilinear SDF interpolation, nearest/trilinear flow sampling, and gradient-tap admission. Skipped/pending fixed ticks accumulate sanitized solver delta up to 0.25s so low-frequency cadence sheds SDF work without over-clamping turn/acceleration. Below resolved q=0.3 the solver collapses to nearest-neighbor SDF, nearest-cell flow sampling, and no gradient taps; high/ultra restores dense ray feelers, trilinear flow reads, and gradient-derived repulsion.
- Intended tuning source, currently absent in the checkout: `Assets/_Project/Data/Vehicles/vehicle_handling_profiles.csv` or root `vehicle_handling_profiles.csv`. When present, it is read cold into Vault scratch via `Span<byte>` and parsed as `ReadOnlySpan<byte>` with FNV-1a lowercase hashes into a fixed Vault-owned open-address `AutopilotHandlingProfileDTO` table. This is the aligned NativeArray substitute for a NativeHashMap under the current DataVault contract. The solver consumes the table by resolving `SubmarineHashID` to a row and applying turn-rate, acceleration, speed-scale, and repulsion-scale modifiers.
- Editor facade: `SubmarineAutopilotTunerWindow` writes Vault tuning DTO values including the authored quality cap, displays resolved quality, assigns default/scout/freighter handling profile hashes, injects Scene View target AUPs without physics casts, and can generate a three-point dogleg route with stackallocated waypoint DTOs through `TryWriteRoute`; telemetry readout uses typed integer/float fields instead of formatted status strings on refresh.
- Route card: `Docs/ARCHITECTURE/ROUTE_CARD_SHINOBU_157_AUTOPILOT.md`.
- Verification status: static source scan summaries are recorded for SHINOBU_157-owned files on forbidden NavMesh/Physics cast APIs, DTO properties, global SHINOBU_157 `BufferID` enum references, hot private NativeArray/List/HashMap ownership, LINQ, `foreach`, `Time.deltaTime`, `Time.fixedDeltaTime`, `StringBuilder`, formatted `ToString()`, Burst flags, `[NoAlias]` pointer annotations, and whitespace via `git diff --check`. New runtime/editor source assets now have checked-in `.meta` files. Public write facades fail closed while the route is locked or jobs are pending, runtime lock rollback uses an acquired-bit `_lockMask`, and black-box dump writes both `Dump_SHINOBU_157.bin` and `Dump_NAVIGATION_SURGEON.bin` from the same telemetry span. R37-era generated-project shielding covered the generated `Hecton8.Core.csproj` stale include for absent unrelated `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` through `Directory.Build.targets`, while `Assembly-CSharp.csproj` still includes absent `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`; `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` is present on disk in the current scan, so older both-missing wording is stale. Generated csproj files also do not yet include the new SHINOBU_157 source paths. Unity import, Burst compile, profiler/GC, and Play Mode route proof remain pending.

## 2026-05-19 SHINOBU_158 Buoyancy Displacement Lane

- Added SHINOBU_158 Vault buffer IDs `71620..71627` and `71629..71631` for buoyancy states, force-packet transfer rows, abyssal flow samples, tuning, 300-frame telemetry, telemetry cursor, material-volume table, CSV scratch, debug force readback, false-sharing-padded counters, and body binding rows. ID `71628` remains unallocated by this route.
- Primary DTO: `BuoyancyStateDTO` is explicit 64 bytes with offsets `CurrentAUP=0`, `Velocity=24`, `VolumeCubicMeters=36`, `MassKg=40`, `EntityHashID=44`, `Flags=48`, `_pad0=52`, `_pad1=56`.
- State mutation boundary: solver and mock jobs mutate authoritative `BuoyancyStateDTO` rows through `UnsafeUtility.AsRef<BuoyancyStateDTO>` over raw Vault buffer pointers; no direct `States[index]` setter remains.
- Parallel writer boundary: strided solver work maps `workIndex` to `(workIndex * EvaluationStride) + EvaluationOffset`; solver `States` and `DebugForces` are annotated with `[NativeDisableParallelForRestriction]` and the fixed-stride mapping is injective, so the annotation removes Unity's index-only safety false positive without allowing writer collisions. Mock state seeding uses the same annotation for raw pointer writes.
- Runtime boundary: Burst `EvaluateBuoyancyJob` reads prebaked scalar volume, AUP surface delta, depth-dependent density, continuous `GlobalQualityWeight` drag, abyssal current samples/fallback triangle flow, and sleep flags; the scheduler stamps `SectorAUP` from `HectonFloatingOrigin.CurrentTotalOffsetDouble`, maps scheduled `workIndex` rows through `EvaluationOffset/EvaluationStride`, and fallback current uses `CurrentAUP - SectorAUP` before `float3` conversion. It emits unmanaged `BuoyancyForcePacketDTO` rows into Vault buffer `71621` with an atomic count in `BuoyancyCounterDTO`, then `PhysicsApplySystem` drains that Vault window on the main thread without calling `Rigidbody` from Burst.
- Dependency boundary: `_forcePacketsReadyToDrain` prevents the next fixed scheduling pass from resetting the force-packet window when the solver completed after the previous post-fixed drain slot.
- Lifecycle boundary: `Awake` and `OnEnable` share an idempotent cold boot path, so CSV ingest runs once per acquired Vault; emergency mock generation runs only when the tuning row reports zero active states, preserving real producer-owned Vault rows. If a completed solver cannot resolve the post-fixed packet route, stale drain readiness is cleared instead of deadlocking the next fixed tick.
- Sleep boundary: surface sleep requires snap state plus force equilibrium; seafloor contact sleeps on low velocity without force-equilibrium proof because bottom contact is the support constraint for settled debris.
- Quality boundary: authored `BuoyancyTuningDTO.GlobalQualityWeight` is a designer cap; runtime writes `ResolvedQualityWeight` into the existing 124-byte tuning slot. Below q=0.25 drag stays linear and bypasses relative-speed work; above q=0.25 it blends quadratic drag, and above q=0.3 it permits exact-speed interpolation. Low quality now reduces scheduled work count to roughly `ceil(active/stride)`, not just an in-job early return.
- Intended tuning source, currently absent in the checkout: `Data/Physics/item_volume_specs.csv`. When present, it is parsed cold from bytes/`ReadOnlySpan<byte>` into a fixed Vault-owned open-address `BuoyancyMaterialVolumeDTO` table. This is an aligned NativeArray substitute for a NativeHashMap because the current DataVault contract exposes typed NativeArray handles.
- Route card: `Docs/ARCHITECTURE/SHINOBU_158_BUOYANCY_ROUTE_CARD.md`.
- Verification status: static source scan summaries are recorded for SHINOBU_158-owned files on `Pack=`, hot DTO properties, gameplay `Update/FixedUpdate/LateUpdate`, direct `Rigidbody.AddForce`, runtime `MeshCollider` volume APIs, private NativeArray/List/HashMap allocations, LINQ, and numeric `.ToString()` in the editor readout formatter. Layout validation no longer uses reflection. Latest compile gate was `dotnet/csc=0` with CPU at `100%`; Unity import, Burst compile, profiler/GC, and Play Mode stress proof remain pending.

## 2026-05-19 SHINOBU_156 Abyssal Cavitation Shockwave Lane

- Added SHINOBU_156 owner-local Vault buffer IDs `71560..71570` for active shockwave events, false-sharing-padded counters, entity AUP snapshots, pressure force packets, shader visual spheres, 300-frame telemetry, ordnance profile rows, CSV scratch, live tuning, SDF volume descriptor, and signed-distance voxel bytes.
- Primary DTO: `ShockwaveEventDTO` is explicit 64 bytes with offsets `EpicenterAUP=0`, `CurrentRadius=24`, `MaxRadius=28`, `PeakPressure=32`, `ExpansionSpeed=36`, `SourceHashID=40`, and explicit padding through byte 63.
- SDF DTO: `AbyssalCavitationSdfVolumeDTO` is explicit 64 bytes with offsets `OriginAUP=0`, `CellSizeMeters=24`, `Dimensions=36`, `DecodeRangeMeters=48`, `Version=52`, `Flags=56`, and explicit padding at 60-63.
- Runtime boundary: shockwaves are expanding mathematical spheres. No `Physics.OverlapSphere`, `OverlapSphereNonAlloc`, `Rigidbody.AddExplosionForce`, particle-system fireballs, or explosion prefab instantiation are part of this route.
- Physics handoff boundary: Burst writes owner-local `ShockwaveForcePacketDTO` rows; `PhysicsApplySystem.DrainCavitationForcePackets` resolves `TargetEntityHash` through `GlobalPhysicsStateManager` and queues deferred `ForceMode.Impulse` point-force packets. The legacy caller-owned Rigidbody-slot overload remains compatibility only; SHINOBU_156 does not expose or claim PhysicsApplySystem private queue ownership.
- Pressure law: `EvaluateShockwavePressureJob` uses AUP-local delta math and literal inverse-square attenuation, `PeakPressure * rcp(max(1, distanceSq))`, with the expanding shell gate and SDF dampening as multipliers.
- SDF boundary: midpoint SDF sampling dampens pressure through SHINOBU_156-owned Vault SDF snapshots when a producer writes `71569/71570`; otherwise deterministic mock seabed/pillar SDF is used. The cavitation runtime no longer imports `Hecton8.World`.
- Visual boundary: `CavitationVisualSphereDTO` rows upload to `_H8CavitationShockwaves` and `Hecton8_UberNoir.hlsl` performs the water-refraction Dear Lie. Authoritative pressure truth stays CPU/Vault-side; visible cavitation stays shader-side.
- Black-box boundary: telemetry faults dump the 300-frame `ShockwaveTelemetryEntry` ring to `Docs/AgentLogs/Dump_SHINOBU_156.bin`.
- Human tuning source: `Assets/_Project/Data/Combat/ordnance_specs.csv` hydrates unmanaged ordnance profile rows through a cold byte/`ReadOnlySpan<byte>` parser into a fixed open-address FNV-1a table in Vault buffer `71566`; `Abyssal Ballistics & Explosives Tuner` mutates Vault-backed tuning values.
- Route card: `Docs/ARCHITECTURE/SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD.md`.
- Verification status: static source scan summaries are recorded for SHINOBU_156-owned source on forbidden physics APIs, particle instantiation, DTO properties, `Pack=1`, Unity random, foreach, and hot NativeArray ownership. R37-era generated-project shielding covered the generated `Hecton8.Core.csproj` stale include for absent unrelated `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` through `Directory.Build.targets`, while `Assembly-CSharp.csproj` still includes absent `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`; `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` is present on disk in the current scan, so older both-missing wording is stale. Unity import, shader compile, Frame Debugger, profiler, GC proof, and runtime visual proof remain pending.

## Purpose

This file is the stable architecture ledger for generated HECTON binary payloads found under
`Data`, `Assets/_Project/Data`, and the current archived black-box dump path. It exists because
agent logs and CSV scans are evidence trails, not durable project authority.

This ledger does not authorize deletion by itself. A file is safe to delete or quarantine only
after its owning gameplay/rendering/data system confirms that no build, bake, runtime convention,
Addressables hook, StreamingAssets copy step, or external packager consumes it.

## 2026-05-19 SHINOBU_117 Thermodynamics Source Lane

- Added `ThermalSourceSignal` as a 64-byte typed signal payload for producer-agnostic heat source registration into the abyssal thermodynamics field.
- Layout: `AbsoluteUniversePosition PositionAup` offset `0` size `48`; `float RadiusMeters` offset `48`; `float IntensityCelsiusPerSecond` offset `52`; `uint SourceId` offset `56`; `uint Frame` offset `60`.
- Route: heat producers call the existing `IThermodynamicsService` facade; `AbyssalThermalManager` publishes `ThermalSourceSignal`; `AbyssalThermodynamicsSolver` ingests the frame snapshot into Vault `HeatSourceDTO` slots. No Thermodynamics-to-World assembly reference was added.
- Dispatch: `ThermalSourceSignal` is now a direct registry lane with deterministic mutation order and a stable sort key from `SourceId` or folded AUP/radius/intensity. Capacity is `128`, with low-tier frame cap `32`.
- Damage ownership: thermodynamics runtime no longer emits `CombatDamageSignal` or thermodynamics mock damage. Heat damage must be owned by consumers that sample the scalar field.
- Determinism polish: legacy thermodynamics source accumulation is serial deterministic, updraft extraction is telemetry-scan ordered, and thermal source signal frame metadata no longer depends on Unity `Time.frameCount`.
- Sample/visual/cadence polish: abyssal owner samples now scale from nearest-cell reads to trilinear field sampling through `GlobalQualityWeight`, active resolution has a 3 second hysteresis band, abyssal heat integration uses fixed `SimulationTickDeltaSeconds` with continuous 12-to-1-frame cadence, legacy debug load shedding uses continuous `qualityCeiling`, and shader upload uses double-buffered `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy`.
- Verification status: static layout and zero-GC route only. A narrow `Hecton8.Core.csproj` build was attempted after CPU opened to 19 percent and failed in unrelated Visor/Somatic missing DTO/id dependencies, not in thermodynamics.

## Evidence

- Inventory artifact: `Docs/Archive/Batch008/AgentLogs/H8BIN_GRAVEYARD_AUDITOR_ReferenceScan.csv`
- Current hygiene artifact: `Docs/Archive/Batch008/AgentLogs/BinaryHygiene_H8BIN_GRAVEYARD_AUDITOR_RECHECK2.json`
- Original audit log: `Docs/Archive/Batch008/AgentLogs/LOG_H8BIN_GRAVEYARD_AUDITOR.md`
- Auditor status: `Docs/Archive/Batch008/Tasks/Status_H8BIN_GRAVEYARD_AUDITOR.md`
- Archive movement log: `Docs/Archive/Batch008/AgentLogs/LOG_ARCHIVE_BATCH_008.md`
- Verifier: `Tools/VerifyBinaryHygiene.py`
- Recheck command before Batch008 archive move: `python Tools\VerifyBinaryHygiene.py --report <active AgentLogs output path now archived as the current hygiene artifact above>`

Current recheck result before SHINOBU_50 alignment repair:

- Target product/generated payload set: 47 files.
- Global verifier scope: 65 `.bin` / `.h8bin` files.
- Global verifier status: `BINARY_HYGIENE_FAILED`.
- Misaligned count: 16.
- Product misalignment: `Data/Balance/Baked/Babel_Dictionary.h8bin`, 1295 bytes, remainder 15.
- Other 15 misalignments: Bakery editor/plugin fixtures under `Assets/Editor/x64/Bakery`.

SHINOBU_50 update on 2026-05-18:

- `Data/Balance/Baked/Babel_Dictionary.h8bin` is now 1296 bytes, remainder 0, with header `FileByteLength=1296` and payload CRC `0x199CAC7A`.
- `Data/Balance/Baked/H8StaticData.bin` now stores the same Babel CRC in its static header.
- Archived artifact `Docs/Archive/Batch009/AgentLogs/BinaryHygiene_SHINOBU_50.json` reports global `BINARY_HYGIENE_FAILED`, but no longer because of the balance Babel payload. The active `Docs/AgentLogs/BinaryHygiene_SHINOBU_50.json` path is absent in the R30 filesystem check, so cite the archive path until a new active artifact is produced. Remaining failures are third-party Bakery binaries plus archived dump artifacts.

SHINOBU_207 update on 2026-05-20:

- `Tools/UpgradeStaticBTreePayloads.py --check` upgraded the current small balance payloads from flat lookup-only bytes to `CacheBTreeFlag` payloads. This is a generator/upgrader path, not a manual byte edit.
- `Data/Balance/Baked/Babel_Dictionary.h8bin` is now 1616 bytes with header `FileByteLength=1616`, payload CRC `0xA1084F1D`, flags `0x101`, B-Tree offset `448`, B-Tree bytes `320`, and data offset `768`.
- `Data/Balance/Baked/H8StaticData.bin` is now 1328 bytes with header `FileByteLength=1328`, payload CRC `0x598EF439`, Babel CRC `0xA1084F1D`, flags `0x101`, B-Tree offset `320`, B-Tree bytes `192`, records offset `512`, and every 48-byte payload record starting on a 64-byte boundary.
- `Data/Balance/Baked/*.manifest.json` files now carry `cacheBTree` sections and `*_PENDING_UNITY_PROOF` statuses. Unity import, MMF map, GC, profiler, and scene/bootstrap proof remain absent.

`Assets/_Project/Data/UI/GlitchTable.bytes` is included in this ledger because the user-requested
scope was binary assets, not only the verifier's `.bin` / `.h8bin` extension set.

## Classification Key

| Class | Meaning |
|---|---|
| `ACTIVE_RUNTIME_WIRED` | Current main runtime source resolves or opens the exact payload path. Unity scene/profiler proof is still pending unless stated separately. |
| `ACTIVE_CODEPATH_NOT_SCENE_PROVEN` | A runtime component can load the file, but no prefab/scene/bootstrap reference proves that component is live. |
| `READER_PRESENT_NOT_WIRED` | A C# reader exists for this exact format/path family, but no production instantiation was found. |
| `EDITOR_OR_TEST_ONLY` | Current exact load is editor tooling, tests, or inspector-only code. |
| `SCRIPT_TOOL_ONLY` | Python/data docs/manifests know the file; first-party runtime/editor C# does not currently load it. |
| `STATIC_LEDGER_MIRROR_ONLY` | Binary asset mirrors data embedded directly in code. |
| `ARCHIVE_DUMP_ONLY` | Historical dump evidence, not product content. |
| `THIRD_PARTY_EDITOR_BINARY` | Vendor/editor binary outside HECTON Python payload ownership. |

## Hard Current Findings

- `STATIC_SOURCE` evidence currently finds exact source/prefab/path wiring for three product payloads:
  `Data/Audio/Acoustic_LUT.bin`, `Data/Visuals/Water_Extinction_Matrix.bin`, and
  `Data/Visuals/Biolum_Profiles.bin`.
  Water-extinction wiring is through Unity's `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` hook
  on `LutArrayResolver.EnsureLoadedAndBound`, not through a scene/prefab caller.
- `Data/Visuals/Biolum_Profiles.bin` has static source for a runtime reader path in
  `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs`. SHINOBU_74 added a
  scene-local `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` host fallback on 2026-05-18 and then
  removed the singleton/Awake self-registration guard in favor of an atomic process ownership claim.
  On 2026-05-19 the runtime was moved behind
  `Assets/_Project/Scripts/VFX/Bioluminescence/Hecton8.VFX.Bioluminescence.Runtime.asmdef`, with
  the editor facade isolated under `Hecton8.VFX.Bioluminescence.Editor.asmdef`.
  The code path is statically present and path-wired; Unity import, scene host, runtime file I/O
  success, GC, profiler, and Frame Debugger proof remain pending. The indirect vegetation shader consumes the packed
  `_BiolumGpuColorBuffer` by instance ID and guards reads by the exact published GPU page count.
  The four-state Dear Lie fallback is published as `_GlobalBiolumDearLieGroups` float4x4, selected
  by template/species group modulo four in the indirect vegetation shader, and packed into the
  existing spatial pulse TEXCOORD lane rather than a new interpolator. Its runtime frame counter now advances once per dispatcher Tick rather than
  through blackbox telemetry writes, so fault dumps cannot perturb mock RNG or shader frame clock.
  The CPU oscillator Burst job now uses deterministic float mode for DTO phase/color mutation.
  The active 50,000-instance CPU path uses a smoothed triangle/hash waveform fake instead of
  per-instance trigonometric pulse evaluation, and squared-distance wavefront/falloff math instead
  of per-instance sqrt for presentation-only glow ripples. `GlobalQualityWeight` now also drives
  update cadence from 5Hz low-quality scheduling to per-frame high-quality scheduling. The managed
  `Vector4[16]` global-state bridge and private `byte[16384]` CSV staging array were removed; CSV
  hot reload now reads directly into vault-owned `BiolumCsvScratch`.
  Unity shader import, scene, profiler, and Frame Debugger proof are
  still pending.
- `Data/Balance/Baked/H8StaticData.bin` and `Data/Balance/Baked/Babel_Dictionary.h8bin` are small
  balance-store artifacts. They are not the authoritative StreamingAssets DataMonolith
  `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, which is currently absent.
- 2026-05-19 SHINOBU_103 update: `static_data.h8bin` authority is now represented by
  `H8DataMonolithCompiler`, `H8StaticDataArena`, and the editor-only Data Monolith compiler window.
  The monolith ABI uses a 16-byte checksum header, 64-byte directory, 16-byte section entries,
  explicit-layout ARM64-safe DTOs, unsigned UTF-8 pool offsets, final 16-byte blob padding, and
  runtime XXHash3 validation of bytes `[16..blobLength)`. Runtime payload bytes are now owned by
  `GlobalDataVault` BufferID `71103`; Android/Quest-style non-filesystem StreamingAssets URIs are
  staged into `Application.temporaryCachePath` before the same Vault/checksum reader runs; the arena
  fails closed if the Vault is absent instead of allocating a private persistent byte fallback.
  Designer CSV rows under `Data/Balance` are compiled into fixed sections; runtime boot must consume
  the binary arena, not CSV/JSON text. Generated `Data/Balance/Baked` manifests and schema templates
  are excluded from compiler source discovery. Same-domain SoA reconstruction jobs now use explicit
  Burst flags and no-alias NativeArray fields. The payload still requires a fresh bake/build artifact
  before this ledger may classify the actual file as present.
- `Data/Balance/Baked/Babel_Dictionary.h8bin` alignment and cache-BTree topology are repaired.
  Header/checksum/alignment semantics are owned by `H8DataBaker` plus the current-byte upgrader
  `Tools/UpgradeStaticBTreePayloads.py`; future dictionary changes must go through a generator path.
- `Data/Lore/Encyclopedia.h8bin` is now an `H8LR` raw UTF-8 lore blob with a 64-byte
  cache-conscious B-Tree section inferred from the aligned gap between record table and payload bytes.
  `PdaH8lrLoreStore` is the dedicated reader and rejects flat-only H8LR payloads. Status remains
  pending Unity import/Play Mode/profiler proof; this is static source plus Python-tool evidence only.
- `ContentAssetBinaryRecord` in `Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs`
  intentionally remains `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]` as a cold
  content hash-map file/export record. Current SHINOBU_02 source recheck found validator coverage
  for the 32-byte size and no active `NativeArray<ContentAssetBinaryRecord>` or active raw runtime
  reader/writer path. It is not approved as a hot ARM64 runtime DTO; if runtime storage is needed,
  split it into a packed file record plus an aligned runtime record and update the schema version.
- Most low/toaster/high/ultra variants are legitimate Math LOD payload ideas, but without a tier
  selector they are disk ballast, not scalability.

## Safe Integration Rules

1. Binary readers must load in boot/cold paths or explicit lazy-read paths only. No JSON parsing, file
   probing, string construction, or heap allocation in `Tick`, `LateUpdate`, `FixedUpdate`, Burst jobs,
   shader upload loops, or per-frame UI paths.
2. Runtime systems must acquire payload ownership through existing domain owners: `GlobalDataVault`,
   `GlobalRegistry` interfaces, typed signal lanes, or cold bootstrap injection. Do not wire direct
   cross-domain concrete references.
3. Tiered payload families require hysteresis. Low, middle, high, and ultra selection must not flip
   every frame or during the same visual beat.
4. If a payload is a visual/audio fake, prefer it over live simulation. If the fake saves CPU, spend
   the saved budget on high-tier visual/audio richness, not on unnecessary physical truth.
5. Never patch generated binary bytes by hand when the format has a header, CRC, offsets, or manifest.
   Fix the generator and rebake.

## Active Payloads

| File | Current status | Runtime/code evidence | Action |
|---|---|---|---|
| `Data/Audio/Acoustic_LUT.bin` | `ACTIVE_RUNTIME_WIRED`, runtime proof pending | `SpatialAudioManager.cs` defines `AcousticLutRelativePath`, calls `TryLoadAcousticLutFallbackCold`, reads the file in a cold init path, `GameBootstrapper.cs` resolves/registers `SpatialAudioManager`, and `Assets/_Project/Prefabs/Audio/PFB_SpatialAudioManagerRoot.prefab` contains the component. This is static source/prefab evidence, not Unity scene/import/profiler proof. | Keep. This is a valid acoustic cinematic cheat: sampled Sabine/damping lookup instead of live acoustic solving. |
| `Data/Visuals/Water_Extinction_Matrix.bin` | `ACTIVE_RUNTIME_WIRED` | `LutArrayResolver.EnsureLoadedAndBound` is marked `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`, resolves `Data/Visuals/Water_Extinction_Matrix.bin`, and `GlobalShaderDispatcher` consumes the bound texture. | Keep. This is a valid Beer-Lambert visual LUT fake. Runtime proof still needs Unity/profiler evidence. |
| `Data/Visuals/Biolum_Profiles.bin` | `ACTIVE_RUNTIME_WIRED`, shader/scene/profiler proof pending | `BiolumPulseSyncRuntime` owns a scene-local runtime host fallback, runtime/editor asmdef split, shader buffer publication, and deterministic CPU oscillator path. | Keep. Static boot/shader source wiring exists; verify with Unity shader import, Profiler, and Frame Debugger before claiming measured frame impact. |

## Candidate Payloads With Reader But Missing Wiring

| File | Current status | Mechanic | Logical insertion point | Blocker |
|---|---|---|---|---|
| `Data/Balance/Baked/H8StaticData.bin` | `READER_PRESENT_NOT_WIRED`, `CACHE_BTREE_PRESENT`, `CACHE_LINE_RECORD_PAYLOADS` | Small static balance record table with `StaticDataStore.OpenDefault()`. | Either make it a dev-only section producer for the DataMonolith, or wire it as a temporary Core data service behind a stable interface. | 1328 bytes, B-Tree bytes 192, payload records start at 64-byte boundaries. Current production authority is the absent StreamingAssets DataMonolith, not this small file. |
| `Data/Balance/Baked/Babel_Dictionary.h8bin` | `READER_PRESENT_NOT_WIRED`, `ALIGNED_PRODUCT_FILE`, `CACHE_BTREE_PRESENT` | Small Babel string pool paired with `H8StaticData.bin`. | Keep aligned through `H8DataBaker` / `Tools/UpgradeStaticBTreePayloads.py`, then wire only with the chosen static-data source of truth. | 1616 bytes, 16-byte aligned, payload CRC `0xA1084F1D`, B-Tree bytes 320. |

## Editor/Test Only Payloads

| File | Current status | Mechanic | Logical insertion point | Action |
|---|---|---|---|---|
| `Data/Economy/Crafting_Costs.h8bin` | `EDITOR_OR_TEST_ONLY` | Crafting recipe/ingredient SoA hydration payload. | Runtime crafting/economy DataVault importer if the crafting owner wants binary recipes. | Do not wire from this audit. Current exact consumer is `EconomyRecipeTunerWindow`. |
| `Data/Narrative/First_Hour_Quests.h8qdag.bin` | `EDITOR_OR_TEST_ONLY` | First-hour quest DAG binary. | Quest bootstrap through `QuestDagDataLoading.TryLoadOshinoOrGenerateMock` if the quest owner promotes it. | Current caller found is editor inspector `NarrativeDagInspectorWindow`. |

## Full Product/Generated Inventory

| # | File | Bytes | Class | Responsibility / mechanic | Logical application or action |
|---:|---|---:|---|---|---|
| 1 | `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin` | 1534512 | `SCRIPT_TOOL_ONLY` | Full `H8BD` Babel localization dictionary, hashed text pool for localization/content. | Package or copy through a real localization bootstrap if required; otherwise it is Unity import ballast. Exact asset GUID/path is not runtime-wired. |
| 2 | `Assets/_Project/Data/UI/GlitchTable.bytes` | 64 | `STATIC_LEDGER_MIRROR_ONLY` | HUD glitch glyph substitution table. | Current `GlitchTable.cs` embeds the bytes directly. Keep only if designers need the asset as authoring evidence. |
| 3 | `Data/AI/Navigation_Tuning.h8bin` | 1280 | `SCRIPT_TOOL_ONLY` | AI path/potential-field tuning cache. | Logical owner is AI navigation bootstrap/DataVault import. No main runtime load found. |
| 4 | `Data/Audio/Acoustic_LUT.bin` | 524288 | `ACTIVE_RUNTIME_WIRED` | Acoustic RT60/damping LUT. | Keep and verify in Unity with GC/profiler. |
| 5 | `Data/Balance/Baked/Babel_Dictionary.h8bin` | 1616 | `READER_PRESENT_NOT_WIRED`, `ALIGNED_PRODUCT_FILE`, `CACHE_BTREE_PRESENT` | Small balance string pool. | Cache B-Tree present. Do not wire until source-of-truth decision is made. |
| 6 | `Data/Balance/Baked/H8StaticData.bin` | 1328 | `READER_PRESENT_NOT_WIRED`, `CACHE_BTREE_PRESENT`, `CACHE_LINE_RECORD_PAYLOADS` | Small static balance DTO lookup blob. | Cache B-Tree present; every payload record starts on a 64-byte boundary. Reconcile with DataMonolith. Do not let both contracts become parallel truth. |
| 7 | `Data/Economy/Crafting_Costs.h8bin` | 7424 | `EDITOR_OR_TEST_ONLY` | Crafting recipe/ingredient cost table. | Promote only through economy owner and DataVault importer. |
| 8 | `Data/Economy/Crafting_Costs_Toaster.h8bin` | 2464 | `SCRIPT_TOOL_ONLY` | Reduced low-tier crafting-cost payload. | Needs runtime tier selector before it has value. |
| 9 | `Data/Economy/Ore_Distribution.h8bin` | 1776 | `SCRIPT_TOOL_ONLY` | Deterministic ore distribution / LCG spawn table. | Logical owner is resource spawn. No load found. |
| 10 | `Data/Economy/Submarine_Upgrade_Stat_Map.h8bin` | 176 | `SCRIPT_TOOL_ONLY` | Submarine upgrade stat map/curve. | Logical owner is submarine upgrade/progression. No load found. |
| 11 | `Data/Ecosystem/Organic_Entropy_Regrowth.h8bin` | 195344 | `SCRIPT_TOOL_ONLY` | Organic entropy/regrowth table. | Logical owner is ecosystem regrowth. No load found. |
| 12 | `Data/Environment/Tide_Harmonics.bin` | 9600 | `SCRIPT_TOOL_ONLY` | Base tide harmonic coefficients. | Logical owner is environment tide system. No load found. |
| 13 | `Data/Environment/Tide_Harmonics.index.h8bin` | 96 | `SCRIPT_TOOL_ONLY` | Tide harmonic sidecar/index. | Must be wired together with a tide reader, not independently. |
| 14 | `Data/Environment/Tide_Harmonics_Low.bin` | 2400 | `SCRIPT_TOOL_ONLY` | Low-tier tide approximation. | Needs environment tier selector with hysteresis. |
| 15 | `Data/Environment/Tide_Harmonics_Ultra.bin` | 38400 | `SCRIPT_TOOL_ONLY` | Ultra tide harmonic variant. | Needs environment tier selector and visual overkill policy. |
| 16 | `Data/Habitat/HabitatPressureBudget.h8bin` | 2704 | `SCRIPT_TOOL_ONLY` | Habitat pressure/failsafe budget table. | Logical owner is habitat logistics/pressure. No load found. |
| 17 | `Data/Localization/en_US.bin` | 60928 | `SCRIPT_TOOL_ONLY` | English localization binary. | Logical owner is localization bootstrap. No main load found. |
| 18 | `Data/Localization/en_US_Taxonomy.h8bin` | 27536 | `SCRIPT_TOOL_ONLY` | Taxonomy localization/classification payload. | Logical owner is taxonomy/scanner/localization. No load found. |
| 19 | `Data/Localization/Radio/marauder_radio_interceptions.h8bin` | 7872 | `SCRIPT_TOOL_ONLY` | Marauder radio interception payload. | Logical owner is audio log/radio narrative. No load found. |
| 20 | `Data/Lore/Encyclopedia.h8bin` | 43536 | `READER_PRESENT_PENDING_UNITY_PROOF` | `H8LR` raw UTF-8 lore blob with two records and one 64-byte B-Tree node at offset 64. | Dedicated reader is `PdaH8lrLoreStore`; Python verification passes, but Unity import, MMF map, GC, and profiler proof are still missing. |
| 21 | `Data/Lore/PdaTechnicalLogs.h8bin` | 59120 | `SCRIPT_TOOL_ONLY` | Full `H8PT` PDA technical log table/text/extra visuals. | Logical owner is PDA data-log UI. Needs zero-GC lookup reader before use. |
| 22 | `Data/Lore/PdaTechnicalLogs_Toaster.h8bin` | 19120 | `SCRIPT_TOOL_ONLY` | Compact low-tier PDA technical log payload. | Needs PDA tier selector before use. |
| 23 | `Data/Narrative/First_Hour_Quests.h8qdag.bin` | 496 | `EDITOR_OR_TEST_ONLY` | Quest DAG binary. | Promote only through quest runtime bootstrap. |
| 24 | `Data/Physics/Submarine_RuntimePack.bin` | 1152 | `SCRIPT_TOOL_ONLY` | Submarine hydrodynamics/runtime verification pack. | Logical owner is submarine physics. No load found. |
| 25 | `Data/Precomputed/Atmosphere/atmosphere_density_matrix_rgba16f.bin` | 1024 | `SCRIPT_TOOL_ONLY` | Atmosphere density RGBA16F LUT. | Logical owner is atmosphere rendering. No load found. |
| 26 | `Data/Precomputed/Atmosphere/atmosphere_sky_gradient_rgba16f.bin` | 262144 | `SCRIPT_TOOL_ONLY` | Sky gradient RGBA16F LUT. | Logical owner is atmosphere/sky renderer. No load found. |
| 27 | `Data/Precomputed/caustics_dispersion_offsets.bin` | 1216 | `SCRIPT_TOOL_ONLY` | Caustics dispersion offset table. | Logical owner is caustics shader/upload path. No load found. |
| 28 | `Data/Precomputed/dalton_gas_toxicity.bin` | 128128 | `SCRIPT_TOOL_ONLY` | Dalton gas toxicity base matrix. | Logical owner is atmosphere/toxicity hazard. No load found. |
| 29 | `Data/Precomputed/dalton_gas_toxicity_overkill.bin` | 96112 | `SCRIPT_TOOL_ONLY` | High/overkill toxicity variant. | Needs hazard/atmosphere tier selector. |
| 30 | `Data/Precomputed/dalton_gas_toxicity_toaster.bin` | 4080 | `SCRIPT_TOOL_ONLY` | Low-tier toxicity variant. | Needs hazard/atmosphere tier selector. |
| 31 | `Data/Precomputed/gerstner_wave_weather.bin` | 32000 | `SCRIPT_TOOL_ONLY` | Gerstner wave/weather LUT. | Logical owner is water/weather. No load found. |
| 32 | `Data/Precomputed/Reverb_LUT.bin` | 262400 | `SCRIPT_TOOL_ONLY` | Reverb/acoustic validation LUT. | Runtime already uses `Data/Audio/Acoustic_LUT.bin`; avoid duplicate acoustic truth. |
| 33 | `Data/Precomputed/sabine_reverb_rt60.bin` | 4000 | `SCRIPT_TOOL_ONLY` | Sabine RT60 lookup. | Superseded for runtime by `Acoustic_LUT.bin` unless audio owner says otherwise. |
| 34 | `Data/System/VFX_Budgets.h8bin` | 1344 | `SCRIPT_TOOL_ONLY` | VFX particle/VRAM budget catalog. | Logical owner is VFX budget/scalability bootstrap. No load found. |
| 35 | `Data/System/Visual_Scalability_Matrix.bin` | 2048 | `SCRIPT_TOOL_ONLY` | Visual LOD/scalability matrix. | Should be wired to visual scalability authority before any low/high/ultra payload selection. No load found. |
| 36 | `Data/UX/VR_Comfort_Profiles.h8bin` | 1472 | `SCRIPT_TOOL_ONLY` | VR comfort profile table. | Logical owner is UX/VR comfort runtime. No load found. |
| 37 | `Data/UX/VR_Comfort_Profiles_Toaster.h8bin` | 1120 | `SCRIPT_TOOL_ONLY` | Low-tier VR comfort profile table. | Needs UX tier selector. |
| 38 | `Data/UX/VR_Comfort_RTXOverkill.h8bin` | 560 | `SCRIPT_TOOL_ONLY` | High/overkill VR comfort supplement. | Needs UX tier selector and headset/platform guard. |
| 39 | `Data/Visuals/Biolum_Profiles.bin` | 25936 | `ACTIVE_RUNTIME_WIRED`, shader/scene/profiler proof pending | Bioluminescence profile table. | SHINOBU_74 added the runtime host fallback, purged static-instance/Awake ownership, isolated runtime/editor asmdefs, wired indirect vegetation packed-buffer shader consumption, guarded shader reads by actual published GPU page count, replaced the 16-slot global vector-array bridge with `_GlobalBiolumDearLieGroups` float4x4, packed the Dear Lie sync group into the existing spatial pulse TEXCOORD lane, detached frame counter advancement from blackbox telemetry writes, moved the CPU oscillator Burst job to deterministic float mode, replaced per-instance trigonometric pulse work with a smoothed triangle/hash waveform fake, uses squared-distance math for per-instance pulse wavefront/falloff while damage-signal radius still computes a cold/control-path sqrt from damage magnitude, removed the private CSV `byte[]` staging path, and made `GlobalQualityWeight` drive update cadence from 5Hz to per-frame; verify with Unity shader import, Profiler, and Frame Debugger before claiming measured frame impact. |
| 40 | `Data/Visuals/Refraction_LUT_RGBA16F.bin` | 524288 | `SCRIPT_TOOL_ONLY` | Base refraction LUT. | Logical owner is water/refraction shader path. No load found. |
| 41 | `Data/Visuals/Refraction_LUT_RGBA16F_MINIMAL_128.bin` | 131072 | `SCRIPT_TOOL_ONLY` | Minimal low-tier refraction LUT. | Needs visual scalability selector. |
| 42 | `Data/Visuals/Refraction_LUT_RGBA16F_ULTRA_512.bin` | 2097152 | `SCRIPT_TOOL_ONLY` | Ultra refraction LUT. | Needs visual scalability selector and VRAM budget gate. |
| 43 | `Data/Visuals/Water_Extinction_Matrix.bin` | 393216 | `ACTIVE_RUNTIME_WIRED` | Base water extinction LUT. | Keep and profile. |
| 44 | `Data/Visuals/Water_Extinction_Matrix_Overkill.bin` | 1572864 | `SCRIPT_TOOL_ONLY` | High/overkill water extinction variant. | Current resolver loads only the base file. Needs selector. |
| 45 | `Data/Visuals/Water_Extinction_Matrix_Toaster.bin` | 24576 | `SCRIPT_TOOL_ONLY` | Toaster water extinction variant. | Current resolver uses analytical fallback on low-memory targets, not this file. |
| 46 | `Data/Visuals/Water_Fog_Density_LUT.bin` | 3008 | `SCRIPT_TOOL_ONLY` | Water fog density preview/validation LUT. | No main runtime load found. |
| 47 | `Docs/Archive/Batch007/AgentLogs/Dump_HEADLESS_SCENARIO_RUNNER.bin` | 16 | `ARCHIVE_DUMP_ONLY` | Archived black-box/headless dump. | Keep only as archive evidence; never package as product content. |

## Non-Target Binary Verifier Contamination

The current hygiene verifier also scans 19 Bakery editor/plugin `.bin` files under
`Assets/Editor/x64/Bakery`. They are not HECTON Python-generated payloads. If the hygiene gate is
intended to police product data only, the verifier needs an explicit vendor/editor exclusion. If the
gate is intended to police every `.bin`, Bakery fixture ownership must be handled by a third-party
asset hygiene task, not by data payload owners.

| # | File | Bytes | Alignment | Classification | Action |
|---:|---|---:|---|---|---|
| B1 | `Assets/Editor/x64/Bakery/hwtestdata/alphabuffer.bin` | 2 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B2 | `Assets/Editor/x64/Bakery/hwtestdata/alphaid2.bin` | 0 | aligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B3 | `Assets/Editor/x64/Bakery/hwtestdata/direct0.bin` | 52 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B4 | `Assets/Editor/x64/Bakery/hwtestdata/heightmaps.bin` | 0 | aligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B5 | `Assets/Editor/x64/Bakery/hwtestdata/ib32.bin` | 28 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B6 | `Assets/Editor/x64/Bakery/hwtestdata/lmid.bin` | 4 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B7 | `Assets/Editor/x64/Bakery/hwtestdata/lmlod.bin` | 4 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B8 | `Assets/Editor/x64/Bakery/hwtestdata/lms.bin` | 18 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B9 | `Assets/Editor/x64/Bakery/hwtestdata/settings.bin` | 10 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B10 | `Assets/Editor/x64/Bakery/hwtestdata/vbtrace.bin` | 96 | aligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B11 | `Assets/Editor/x64/Bakery/hwtestdata/vbtraceUV0.bin` | 32 | aligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B12 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part0.bin` | 7 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B13 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part1.bin` | 12597 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B14 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part2.bin` | 628 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B15 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_2017_1_part3.bin` | 88 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B16 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part0.bin` | 7 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B17 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part1.bin` | 12497 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B18 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part2.bin` | 584 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |
| B19 | `Assets/Editor/x64/Bakery/lightingDataChunks/LightingData_5_6_part3.bin` | 84 | misaligned | `THIRD_PARTY_EDITOR_BINARY` | Exempt/quarantine only through third-party asset hygiene. |

Other binary-like assets observed outside the product/generated target set:

- `Assets/_Project/Diagnostics/auto_baseline_test.raw` - diagnostics raw evidence, not a generated HECTON runtime payload in this pass.
- `Assets/MapMagic/Generators/Biomes/Runtime/Sources/*.raw` - MapMagic biome raw source assets, third-party/runtime authoring material.
- `Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor/ConfigData.bytes` - Odin editor plugin config.

## 2026-05-19 SHINOBU_111 Voxel Delta WAL Payload

`Assets/StreamingAssets/voxel_save_schema.h8bin` is not present in this checkout. SHINOBU_111 added
a deterministic emergency schema generator and a Vault-backed voxel delta WAL payload surface instead
of inventing a generated binary by hand. Runtime payload layout is:

- `VoxelDeltaHeaderDTO`, 32 bytes, explicit ARM64-safe layout: sector hash, compressed size,
  uncompressed size, XXHash3-derived checksum, explicit padding.
- Payload bytes: RLE delta stream, optionally LZ4-compressed; `CompressedSize == UncompressedSize`
  means raw RLE bytes.
- WAL route: `IAsyncPersistenceService.TryEnqueueChunkPageWrite(..., H8WorldPagePayloadTypes.VoxelDeltaRle, ...)`; concrete pager remains SavePersistence-owned.
- Human tuning source: `Assets/_Project/Data/World/voxel_save_profiles.csv`, parsed by a byte-level
  zero-GC job into `SaveVoxelDeltaTuning`.

Status: `PENDING COMPILE/RUNTIME PROOF`. No new `.h8bin` is claimed as shipped content until Unity
import, layout manifest, and WAL replay validation run cleanly.

## 2026-05-19 SHINOBU_154 Entity Delta WAL Payload

`Assets/StreamingAssets/entity_save_schema.h8bin` is not present in this checkout. SHINOBU_154 added
a deterministic emergency entity schema and a Vault-backed dynamic-entity delta lane instead of
serializing object graphs, JSON, `ModuleDTO`, `WorldStateDTO`, or fauna MonoBehaviours.

- `EntityDeltaHeaderDTO`: 32 bytes, explicit ARM64-safe layout: `SectorHash=0`,
  `CompressedSize=8`, `UncompressedSize=12`, `XXHash3Checksum=16`, padding bytes `24..31`.
- `EntityDeltaDataRecordDTO`: 80 bytes, explicit layout with integer AUP sector coordinates,
  local `float3` offset, stable hashes, compact vitals, flags, baseline hash, and simulation tick.
- Payload bytes: dehydrated entity delta records only. Dense records are byte-RLE preconditioned and
  then passed through the Burst deterministic LZ4-block encoder already used by the save lane.
- WAL route: `IAsyncPersistenceService.TryEnqueueChunkPageWrite(...)` with payload type
  `H8WorldPagePayloadTypes.EntityDeltaRle`; the pager sector key is mixed with the payload type while
  the header retains the true AUP sector hash.
- Human tuning source: `entity_save_profiles.csv` bytes are parsed cold into Vault tuning/profile DTOs;
  missing CSV leaves deterministic defaults and mock state generation available for CI.
- Emergency fallback schema bytes are written as a canonical little-endian 64B schema header, not as
  raw host-endian `EntityDeltaMockSchemaDTO` memory.
- Vault lane: `SaveEntityDeltaSchemaBytes` through `SaveEntityDeltaWalPayloadBytes` (`70340..70357`) under
  `SystemID.SavePersistence`; no persistent private NativeArray is owned by the compressor.
- Route card: `Docs/Tasks/Route_SHINOBU_154_EntityDeltaCompression.md`, review result `YELLOW`
  until Unity import, Burst, profiler/GC, WAL replay, and unload proof artifacts exist.
- Compile-wall note: current SaveSystem source is still under the existing root
  `Assets/_Project/Scripts/Hecton8.Core.asmdef`, which already contains sibling runtime references.
  SHINOBU_154 did not mutate that asmdef; file-level direct sibling namespace scan for the entity
  delta lane is clean. A true SaveSystem asmdef split remains integrator-owned because existing
  SaveManager, Merkle, voxel, and layout-manifest routes share the root assembly shape.
- Latest polish: `EntityDeltaGizmoProbe.OnDrawGizmos` is the literal editor heatmap hook for unsaved
  entity-delta sectors, and hot extraction/prune record access now uses `UnsafeUtility.AsRef` helpers
  instead of relying on `NativeArray<T>` indexer mutation. Stable Unity `.meta` files are present for
  the new runtime/editor C# assets. `EntityDeltaCompressionRatioAuditJob` now provides the schedulable
  Burst telemetry audit for Task 20 and is chained into `ScheduleCompressionPipeline` after telemetry
  recording; it requires both the 99-percent smaller-sample pass and aggregate 99-percent byte savings
  using integer PPM counters, not a sample-only ratio. The pre-LZ4 entity delta stream now starts
  with a 16-byte `EntityDeltaRleStreamHeaderDTO` so WAL replay can distinguish raw dense fallback from
  `{run,value}` RLE pairs; raw WAL validation and post-decompression validation reject ambiguous or
  malformed RLE streams before hydration. Dense entity records are canonicalized as fixed-offset
  little-endian fields before RLE/LZ4 instead of raw host-endian DTO `MemCpy`; replay hydration accepts
  explicit little-endian or big-endian stream markers and rejects missing/ambiguous endian markers.
  Extraction and replay hydration reject non-finite local AUP offsets before those bytes can become
  WAL or Vault record truth.
  `EntityWalPayloadEnvelopeAuditJob` now runs after WAL pack
  to verify the copied WAL header, packed byte count, checksum, and raw RLE envelope inside the Burst
  dependency chain before enqueue can treat the payload as ready; `TryEnqueueEntityDeltaWalWrite`
  rejects payloads without the audit pass counter. `ScheduleWalPayloadDecodePipeline` adds the
  matching Burst replay path: verify WAL header/checksum, copy or LZ4-decode into RLE bytes, validate
  the RLE stream, expand dense bytes, and hydrate `EntityDeltaDataRecordDTO` rows without managed
  `byte[]` or `MemoryStream`. The RLE stream header is public for layout-manifest/test visibility,
  the cold WAL verifier accepts header-only zero-delta payloads only when size/checksum fields are
  exactly zero, and the public enqueue helper rejects short counter buffers before reading audit
  counters. `TryRequestEntityDeltaWalRead` and `TryCopyCompletedEntityDeltaWalPayload` provide the
  matching typed read facade over `IAsyncPersistenceService`, using the same entity pager-sector hash
  route as writes before bytes enter the Burst decode pipeline. The existing `H8BinaryWorldPager`
  WAL stream now opens with `FileOptions.Asynchronous | WriteThrough | SequentialScan`; queue
  ownership, worker-thread processing, and WAL bytes remain unchanged. `SaveEntityDeltaWalPayloadBytes`
  is a dedicated staging buffer so WAL source bytes never alias the RLE decode destination. Save and
  replay scheduling now run stack-only native byte-range overlap guards before `[NoAlias]` Burst jobs
  are scheduled; overlap or range-list capacity overflow marks the existing counters/header/stats as
  fatal instead of running vectorized jobs over invalid aliases. Scheduling profiler anchors are
  `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode`; worker job
  timings still require Unity/Burst profiler proof. The SHINOBU jobs that suppress
  `NativeDisableParallelForRestriction` now carry source-local three-paragraph safety proofs for their
  index/block/delta-range ownership invariants. The editor-only `EntitySaveTunerWindow` telemetry
  facade now polls Vault telemetry at 4Hz, repaints its histogram only on telemetry cursor/payload
  changes, uses cached UI Toolkit callbacks, and confines its unavoidable managed `Label.text`
  summary string to a fixed-buffer, change-gated editor boundary; the runtime compressor route remains
  free of `ToString`, string concatenation, and managed summary formatting. The cold `RunSelfAudit()`
  layout proof now derives audited field offsets with `UnsafeUtility.AddressOf` pointer deltas instead
  of `Marshal.OffsetOf`, `typeof`, `GetField`, or reflection/string field lookup. The entity
  black-box dump now writes the telemetry dump header and every 64-byte telemetry row as explicit
  little-endian fields instead of raw host-endian DTO memory. WAL pack now marks fatal counters on
  invalid header/source buffers instead of silently returning, and WAL decode resets stale compression
  audit counters when replay reuses the shared counter row. Dense pack, RLE precondition, LZ4,
  WAL decode failure, RLE expand failure, and schedule-failure rows now clear downstream byte/decode/audit
  counters on invalid input so stale Vault aliases cannot preserve old success proof.

Status: `BLOCKED BY DEPENDENCY COMPILE WALL`. Static source/layout hooks are present. Unity batchmode
import on 2026-05-20 is archived at `Docs/Archive/Batch010/AgentLogs/Unity_SHINOBU_154_Compile.log`; the active `Docs/AgentLogs/Unity_SHINOBU_154_Compile.log` copy is absent after Batch010 archival. The script asset
set includes SHINOBU_154 runtime/probe/editor files and no SHINOBU file appears in the compiler-error
list. Project-wide compile exits on unrelated owner domains (`Physics/HabitatFluidIncursionJobs.cs`,
`Narrative/Prologue/AwaitableDropSequenceDirector.cs`, `World/ProceduralWreckage/*`,
`World/ProceduralCoral/*`) plus Burst ILPP in `Hecton8.MockDomain.Runtime`. Burst Inspector,
profiler GC capture, WAL replay, and 99 percent compression-ratio route proof remain pending until
that compile wall is cleared.

## 2026-05-19 SHINOBU_133 Sonar Cartography Vault Payload

SHINOBU_133 added a Vault-owned 1-bit cartography payload surface for sonar fog-of-war truth. No new
runtime `.bin` payload is claimed. Human scanner tuning source is
`Assets/_Project/Data/scanner_hardware_profiles.csv`, parsed into Vault scratch/profile buffers by a
byte-level parser; Unity import, editor-window interaction, and runtime profiler proof remain pending.

Reserved DataVault buffer IDs:

- `71420` `DiscoveryWords`: `ulong` bitmask, `32768 * 9` words, uninitialized then Burst-cleared.
- `71421` `SectorTable`: `CartographySectorDTO[9]`, explicit 32-byte ARM64 layout.
- `71422` `UploadPackedR8`: packed `uint` R8 voxel upload staging for hologram volume sampling.
- `71423` `TelemetryRing`: `CartographyTelemetryEntry[300]`, 64-byte black-box entries.
- `71424` `TelemetryCursor`: single `int` ring cursor.
- `71425` `Tuning`: `CartographyTuningDTO[1]`, 64-byte editor/hot-reload tuning.
- `71426` `ScannerProfiles`: `CartographyScannerProfileDTO[32]`, open-addressed FNV-1a profile table.
- `71427` `CsvScratch`: 8192-byte CSV ingest scratch.
- `71428` `MockPings`: `MapRevealSignal[16]` producer/fallback sonar ping lane.
- `71429` `Counters`: `CartographyCounterDTO[9]`, 64-byte false-sharing-padded discovery output counters with telemetry `PendingSignalCount` at offset 28.
- `71430` `ActiveSectorHashes`: `ulong[9]` resident 3x3 AUP sector hashes.
- `71431` `DebugVoxels`: `CartographyDebugVoxelDTO[512]` editor gizmo staging.
- `71432` `RleRuns`: `CartographyRleRunDTO[4096]` save-compression seam.
- `71433` `SurfaceMaskWords`: `ulong[32768]` SDF-shell mask seam.
- `71434` `RollbackSnapshotWords`: `ulong[32768]` deterministic memcpy rollback seam.
- `71435` `PendingPings`: `MapRevealSignal[16]` dispatcher-staged ping lane consumed by the scheduled job.
- `71436` `PendingSignalCounts`: `int[1]` producer-side pending count, separated from discovery counters to avoid scheduled-job races.

Runtime quality route: `PDAMapTab` and `PlayerExplorationTracker` resolve effective cartography quality
as `min(HomeostasisBrain.GlobalQualityWeight, CartographyTuningDTO.GlobalQualityWeight)`. The hologram
packed-R8 upload cadence, visual decimation, and secondary point-cloud overlay stride now consume that
continuous scalar; no low/high cartography tier switch is part of the owned route.

Execution route: live bitmask mutation now registers owner-local `IDispatcherSystem` adapters for
`PreSimulation`, `Simulation`, and `PostSimulation`. Pre-simulation stages `MockPings` into `PendingPings`
and clears `PendingSignalCounts`; `ApplyCartographyFrameDiscoveryJob` is scheduled through the master
dispatcher and consumes Vault `DiscoveryWords`, `SurfaceMaskWords`, `PendingPings`, and `Counters`;
legacy `SlowTick()` mutation is fallback only when dispatcher registration is unavailable.

Status: `PENDING COMPILE/RUNTIME PROOF`. Static source wiring exists in
`Assets/_Project/Scripts/Cartography/CartographyGridJobs.cs`,
`Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs`, and
`Assets/_Project/Scripts/UI/PDAMapTab.cs`. The hologram shader source is
`Assets/_Project/Art/Shaders/Hecton_HologramMap.shader`, and the static material/buffer binding route
is `_CartographyVoxelR8` -> `PDAMapTab` packed-R8 `GraphicsBuffer<uint>`. Unity import, Frame
Debugger, GCMonitor, and save/replay validation are not yet proven.

## 2026-05-19 SHINOBU_124 Flora Procedural Sway Vault Lane

SHINOBU_124 owns the presentation-only flora sway displacement field. No shipped `.h8bin` payload is
claimed; missing `flora_stiffness_profiles.h8bin` fails closed to deterministic unmanaged fallback rules.

Reserved DataVault buffer IDs:

- `71650` `FloraSwayDisplacementField`: `FloraDisplacementDTO[262144]`, explicit 16-byte nodes.
- `71651` `FloraSwayFieldMeta`: `float4[4]` center/cell/resolution/quality metadata.
- `71652` `FloraSwayFieldBlackBox`: `FloraSwayFieldTelemetryEntry[300]`, explicit 64-byte entries.
- `71653` `FloraStiffnessRules`: `FloraStiffnessRuleDTO[16]`, deterministic fallback/CSV target.
- `71654` `FloraStiffnessCsvScratch`: `byte[16384]`, cold CSV ingest scratch.

Collision repair: earlier SHINOBU_124 notes used `71580..71584`; current SHINOBU_155 source now owns
`71604..71613` for player death reconciliation after avoiding the flora history and the submarine
autopilot `71592..71603` lane. SHINOBU_124 uses `71650..71654`; focused scan found no other active
`BufferID` owner for that flora range.

Runtime boundary: vehicles enter through wake signals and the cached Vault route; individual grass/kelp
bending is a shader sample from `_HectonFloraSwayDisplacementField`, not a PhysX collider, trigger, or
per-blade CPU deformation path. Clear/origin-shift invalidation marks in-flight field uploads for discard
instead of force-completing them outside teardown; discarded uploads are black-boxed with pending ring and
center-shift state before the upload state is cleared.

Status: `PENDING COMPILE/RUNTIME PROOF`. Static source/docs are updated; Unity import, Burst compile,
Frame Debugger, profiler, and GCMonitor proof remain pending.

## 2026-05-19 SHINOBU_155 Player Death Reconciliation Vault Lane

SHINOBU_155 owns the no-scene-reload death reconciliation state. Fatal player health/survival events
emit `PlayerRespawnSignal`; physiology, metabolism, decompression, kinematic AUP, death fade, inventory
penalty command, and telemetry are reconciled through Vault-owned unmanaged buffers.

Reserved DataVault buffer IDs:

- `71604` `RespawnStateBuffer`: `RespawnStateDTO[1]`, explicit 32-byte target AUP/hash/flags state.
- `71605` `MedicalBayRespawnPointsBuffer`: `MedicalBayRespawnPointDTO[8]`, explicit 64-byte mock/real med bay AUP rows.
- `71606` `RespawnFadeBuffer`: `RespawnFadeDTO[1]`, explicit 32-byte Dear Lie shader fade scalar.
- `71607` `RespawnTelemetryRingBuffer`: `RespawnTelemetryEntry[300]`, explicit 64-byte forensic ring.
- `71608` `RespawnTelemetryCursorBuffer`: `RespawnTelemetryCursor64[1]`, explicit 64-byte false-sharing padded cursor.
- `71609` `RespawnTuningBuffer`: `RespawnTuningDTO[1]`, explicit 64-byte designer tuning payload.
- `71610` `RespawnPenaltyRulesBuffer`: `InventoryDeathPenaltyRuleDTO[64]`, explicit 16-byte CSV penalty rows shared through Core contracts.
- `71611` `RespawnPenaltyRuleCountBuffer`: `int[1]`, rule count.
- `71612` `RespawnCsvScratchBuffer`: `byte[32768]`, cold CSV ingest scratch.
- `71613` `RespawnRequestBuffer`: `RespawnRequestDTO[1]`, explicit 64-byte pending request lane.

Runtime route: `PlayerDeathReconciliationBridge` owns fatal-damage signal emission only; `ShinobuRespawnReconciliationRuntime`
owns dispatcher-phase Vault mutation; `HydrodynamicKccRuntime` consumes only request-phase packets with `Requested`
present and `Committed` absent, or committed-phase packets with `Committed` present, then requires nonzero sequence
and no `InvalidDeathAup` before accepting `SuspendCollision`, and
skips capsulecast/collision resolution for one accepted snapshot generation. The KCC accepted-generation latch is written
only after an admissible packet is found, so malformed packets cannot consume the generation. `HectonShaderGlobalDataVaultBridge` slot `19` carries
`_HectonRespawnDearLieParams` and `_HectonDeathFadeIntensity` into the UberNoir shader from the VisualSync route. The player GameObject persists;
no death path scene reload, destroy/instantiate respawn, or coroutine fade is part of the route.

Core signal lane route: `PlayerRespawnSignal` is a direct `GlobalSignals` lane with stable hash `0x5253504E`,
expected capacity `8`, max frame signals `16`, low-tier frame signals `4`, direct pre-simulation flush, post-simulation
snapshot clear, finite payload guard for both `double3` AUP fields, 128-byte layout validation, and `SignalBusAotPreserve`
coverage. Gameplay and Physiology early-boot calls reuse the payload's constants rather than owning separate lane values.
VisualSync reads `RespawnFadeDTO` only after the active fade/reconciliation job fence is already completed; late jobs skip
that VisualSync publish instead of blocking the render phase. The respawn Dear Lie shader route now publishes only while active
or while issuing the final zero-clear, and `H8UberNoirApplyRespawnDearLie` scales blackout/grain/chroma/abyss tint through continuous
`GlobalQualityWeight`-derived `detailWeight` instead of an `_MATH_LOD_LOW` branch inside the respawn mask. SHINOBU passes its cached `_dataVault` into the Core bridge overload instead of using
the bridge's legacy no-argument `GlobalRegistry.DataVault` lookup path. Simulation likewise refuses to stack a second writer over the
same respawn Vault rows while the previous active handle is incomplete, returning a combined dependency instead.
Death-adjacent health/survival signal timestamps use `TimeSliceScheduler.CurrentFrameId` so respawn, vitals, and physiology
metadata share the dispatcher frame domain for rollback/post-mortem correlation.
The Gameplay bridge fails closed on non-finite death AUP before configuring or pushing `PlayerRespawnSignal`; it does not synthesize
`double3.zero` as a plausible origin packet.
Health/survival death producers resolve finite movement/snapshot AUP into `double3` absolute coordinates before the bridge and do not
import `Hecton8.World` in the SHINOBU death route. Survival no longer fabricates a reconciled-death AUP from runtime
`Transform.position`; missing/non-finite AUP falls through to legacy death handling. The existing `HectonHazardManager`
compatibility bridge owns the `double3` absolute-point to World AUP conversion for hazard queries.
`ShinobuRespawnReconciliationRuntime` dispatcher phases use cached `_dataVault` only and gate on already-created handles through
`HasHotVaultState()`; the allocation-capable `EnsureVaultState(...)` and `GlobalRegistry.DataVault`/latest-Vault fallback are
restricted to cold Awake/Start/DataVault hot-swap/editor utility paths.
Cold `EnsureVaultState(...)` runs `ShinobuRespawnLayoutGuards.ValidateRespawnLayouts()` before any respawn Vault handle request;
layout drift fails closed before buffers are allocated.
The cold guard validates `PlayerRespawnSignal` as a two-cache-line explicit payload: size `128`, `DeathAUP=0`,
`RespawnAUP=24`, scalar contract fields through `SuspendCollisionFrames=73`, `Reserved0=74`, and aligned
tail lanes `Reserved1=76`, `Reserved2=80`, `Reserved3=88`, `Reserved4=96`, `Reserved5=104`, `Reserved6=112`,
`Reserved7=120`. Earlier pre-repair route wording is obsolete and is superseded by this executable 128-byte proof.
Hot respawn jobs, Simulation scheduling, VisualSync shader payload publish, and AUP conversion helpers use `default` field
assignment rather than literal `new`/object-initializer value construction. Remaining `new`/`Complete()` sites are documented
cold host/dispatcher adapter creation, cold CSV/dump IO, stack-only span construction, boot mock-medbay generation, or teardown
fences.
Death-adjacent survival scalar sidecar now uses explicit 32-byte `SurvivalPhysiologyScalarResult`, deterministic Burst standard
precision/synchronous compile flags, `[NoAlias] NativeArray` output, default field assignment, and `UninitializedMemory` for its
one-row Vault result; `job.Run()` is intentional for the one-row scalar kernel to avoid scheduler overhead.
The one-row scalar result handle is created only after a cold `UnsafeUtility.SizeOf/GetFieldOffset` guard verifies the same
32-byte layout and offsets, so row drift fails closed before Vault buffer creation.
`ShinobuPhysiologyRuntime` decompression shader scalar payloads are also built through `default` `Vector4` field assignment before
bridge publish.
Successful reconciled deaths skip legacy managed `GlobalTelemetryBus.PublishPlayerDeath`, `SurvivalVitalsChangedSignalFlags.Death`,
legacy last-death-record capture, human-readable `RecordDeathTelemetry`, health `OnHealthChanged`, health `OnDamageTaken`,
vital warning emission, zero-health combat target sync, post-damage trauma HUD/leviathan advisory fan-out, `OnDeath`, and `PlayerDiedEvent` fallback side effects; those remain only for unreconciled failure or non-respawn health changes after
`PlayerRespawnSignal.TryPush` or finite AUP resolution fails. Survival reconciliation clears stale `_hasLastDeathRecord`/`_lastDeathRecord`
so PDA/HUD last-loss consumers cannot surface a successful one-frame rebirth as a legacy loss.
Because Gameplay can only publish the lethal request before med-bay selection, Physiology resolves the target in `PreSimulation`
and transforms the current `PlayerRespawnSignal` snapshot in-place. Same-frame Physics/Fauna consumers therefore see the
resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped one-frame collision-suspend count without a
second queued signal or a direct sibling-domain call. `ResetPlayerPhysiologyJob` consumes that staged `RespawnStateDTO`
target as the primary Simulation truth and scans the med-bay row buffer only as a fail-closed fallback when staged state is
missing, non-finite, or unresolved.

Inventory penalty route: `ResetPlayerPhysiologyJob` emits `InventoryCommandSignal.DropNonEquippedResources` with
`PayloadFlags=VaultPenaltyRules`, `Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and
`Payload3=0x53313535`. `PlayerInventory` resolves the same Vault rule table through cached `IDataVault` and applies
per-item `DropOnDeath` / `RetainIfEquipped`. CSV token hashing now matches inventory item IDs via LocHash-compatible
UTF-8-as-UTF-16 FNV, while numeric `0x...`/decimal authored hashes are also accepted. The XML NativeHashMap wording is
implemented as a fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback payloads.
If the command advertises a Vault rule table and Inventory cannot resolve it from the cached Vault reference, it fails closed
instead of applying broad fallback drops.

Status: `PENDING COMPILE/RUNTIME PROOF - COMPILE BLOCKED BY EXTERNAL CONTRACT BRIDGE ERRORS`. Static source/docs are updated. R37-era generated-project shielding covered the stale generated include for deleted `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` through `Directory.Build.targets`; the follow-up guarded Core compile now advances to semantic errors in external missing contract/source bridge types outside this lane.
Route card: `Docs/Tasks/Route_SHINOBU_155_Respawn.md`. Blackbox dump paths: `Docs/AgentLogs/Dump_SHINOBU_155.bin` and XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.

## 2026-05-19 SHINOBU_122 Biome Transition Shader Payload

SHINOBU_122 owns the mathematical biome-atmosphere blend route. No shipped
`biome_transition_matrix.h8bin` payload is currently claimed; the runtime fails over to CSV bytes and a
deterministic unmanaged mock biome seed.

Reserved DataVault buffer IDs:

- `71220` `BiomeTransitionStates`: `BiomeStateDTO[64]`, explicit 64B rows.
- `71221` `BiomeTransitionCenters`: `BiomeCenterDTO[64]`, explicit 64B rows with center-owned state index.
- `71222` `BiomeTransitionInfluences`: `BiomeInfluenceDTO[1]`.
- `71223` `BiomeTransitionCurrentAtmosphere`: `CurrentAtmosphereDTO[1]`.
- `71224` `BiomeTransitionBlendMask`: `BiomeBlendMaskDTO[1]`.
- `71225` `BiomeTransitionShaderPayload`: `float4[8]`, 128B CBuffer source.
- `71226` `BiomeTransitionAcousticStage`: `BiomeAcousticStageDTO[1]`.
- `71227` `BiomeTransitionTelemetryRing`: `BiomeTransitionTelemetryEntry[300]`.
- `71228` `BiomeTransitionCounters`: `BiomeTransitionCounterDTO[1]`.
- `71229` `BiomeTransitionTuning`: `BiomeTransitionTuningDTO[1]`.
- `71230` `BiomeTransitionCsvScratch`: `byte[65536]`.
- `71231` `BiomeTransitionMockCameraAup`: `AbsoluteUniversePositionBlit128[1]`.

Runtime boundary: Burst jobs write the eight-slot shader payload into Vault after deterministic
distance/weight blending. Visual sync uploads the completed 128B snapshot into a double-buffered
`GraphicsBuffer.Target.Constant` named `H8BiomeTransitionPayload` through `LockBufferForWrite` and
`UnsafeUtility.MemCpy`. `_pendingShaderPayloadUpload` prevents a newly scheduled solver from
overwriting the Vault shader payload before LateFrame visual sync consumes it. Legacy scalar shader
globals remain a compatibility mirror, not the sole route.

Status: `PENDING COMPILE/RUNTIME PROOF`. Static source wiring exists in
`Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` and
`Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs`. Unity import, Frame Debugger
CBuffer binding, profiler, GCMonitor, and generated project compile proof remain pending.

## 2026-05-19 SHINOBU_153 Procedural Geology Vault Lane

SHINOBU_153 owns deterministic JIT resource geology. No shipped ore-coordinate `.h8bin` payload is claimed; unmined resource positions are regenerated from world seed + AUP sector hash. Depleted resource truth is represented by deterministic candidate-slot hash/mask deltas and existing depletion signals, not by stored coordinates.

Reserved DataVault buffer IDs:

- `71530` `ResourceNodes`: `ResourceNodeDTO[maxOreCapacity]`, explicit 128 B rows with matrix, resource hash, yield, AUP, and padding.
- `71531` `OrePositions`: `float3[maxOreCapacity]`, camera-relative local positions for read models.
- `71532` `OreTypes`: `int[maxOreCapacity]`, zero means hole or visual-only matrix.
- `71533` `DepletionMasks`: `ulong[wordCount]`, active-sector live/depleted mask.
- `71534` `ResourceMatrices`: `float4x4[maxOreCapacity]`, direct GPU upload lane.
- `71535` `BiomeHeatmap`: `byte[256]`, coarse dominant-biome fallback.
- `71536` `SpawnCounts`: `int[7]`, generated/render/depletion/visual/overflow/HZB counters.
- `71537` `TelemetryRing`: `GeologyGenerationTelemetryEntry[300]`, 64 B black-box entries.
- `71538` `MockTerrainSdf`: `GeologyTerrainSampleDTO[1024]`, deterministic fallback terrain.
- `71539` `DistributionRules`: `GeologyDistributionRuleDTO[32]`, CSV/default resource distribution rules.
- `71540` `Tuning`: `GeologyTuningDTO[1]`, editor/runtime tuning.
- `71541` `CsvScratch`: `byte[32768]`, Vault-owned cold CSV scratch used by the span parser; no `File.ReadAllBytes` staging.
- `71542` `SelfAudit`: `GeologySelfAuditResultDTO[1]`, layout/determinism audit row.
- `71543` `CandidateSlots`: `int[maxOreCapacity]`, compact render index -> deterministic slot.
- `71544` `DepletionCacheKeys`: `ulong[4096]`, Vault-owned session depletion cache keys.
- `71545` `DepletionCacheMasks`: `ulong[4096]`, Vault-owned session depletion cache masks.
- `71546` `DepletionCacheCount`: `int[1]`, Vault-owned session depletion cache count.
- `71547` `SectorHashGrid`: `long[9]`, 3x3 AUP sector hash handoff around the player.
- `71548` `IndirectArgs`: `GeologyIndirectArgsDTO[1]`, 16-byte `DrawProceduralIndirect` args row written by the generation job and copied to the GPU args buffer.
- `71549` `HzbTiles`: `GeologyHzbTileDTO[4096]`, optional 16-byte CPU HZB readback tiles for matrix culling.
- `71550` `HzbMeta`: `GeologyHzbMetaDTO[1]`, optional 128-byte camera-relative view-projection, dimensions, flags, and bias row for HZB culling.

Runtime boundary: `ProceduralOreSpawner` emits Vault DTOs and matrix buffers under `SystemID.WorldResourceSpawnerRuntime`; it no longer contains proxy `GameObject`, `MeshCollider`, `ICuttable`, direct `Hecton8.Gameplay` coupling, or manager-level persistent `NativeArray<T>` aliases. Persistent runtime state is handle-only through 16-byte `VaultGenerationHandle<T>` descriptors; full mutation/job paths resolve transient Vault views, while per-frame helpers resolve only the exact descriptor they consume. Rendering uploads matrices and the 16-byte procedural args row with `GraphicsBuffer.LockBufferForWrite`; `Hecton_ProceduralOreClusters.shader` expands 36 vertices per instance from `SV_VertexID`, reads `_OreMatrices`, and is submitted through `Graphics.DrawProceduralIndirect`. Per-slot generation seeds `Unity.Mathematics.Random` from world seed + AUP sector hash + slot, then drives the placement stream through the SHINOBU LCG. Grounding now uses a quality-gated bounded gradient refinement: below `GlobalQualityWeight < 0.3` it collapses to nearest terrain height, and high quality executes up to two finite-difference refinement steps. Optional HZB readback buffers `71549/71550` are read only through Vault; active HZB culls visual-only matrices before upload, while authoritative cull requires an explicit flag so gameplay truth is not silently camera-owned. CSV resource tokens are normalized to `WorldOreTypeIds` 1-4 before entering Vault rules; unknown resource tokens are rejected cold. `GeologyTuningDTO` is the cold control row for density, cluster spread, normal tolerance, visual density, and sector size after validation. After cold boot, runtime Vault access uses cached `_dataVault` plus `IGlobalRegistryHotSwapListener`/`IGlobalRegistryHotSwapRefListener` rebind events rather than hot `GlobalRegistry.DataVault` reads. `TelemetryRing` receives bounded frame-level samples with cached first-node hash/position, so the black-box trail is not forced to scan resource lanes each frame. `IWorldResourceSpawnerCommandModel` provides the primitive data-only depletion command route for future interaction consumers; broad metamorphism migration off legacy `ResourceNode` remains owner-contract blocked. Black-box dumps write both `Docs/AgentLogs/Dump_SHINOBU_153.bin` and XML alias `Docs/AgentLogs/Dump_GEOLOGY_ARCHITECT.bin`.

Loop 9 H-Phi note: routine `EnsureNativeState()` now validates cached handle metadata only; full 21-buffer Vault view resolution is limited to immediate mutation/job/readback paths. DTO padding fields are private explicit-offset fields and editor validation reflects them non-publicly.

Loop 10 precision note: MapMagic payload lookup no longer casts absolute `double3` AUP to float `Vector3`; lookup uses runtime coordinates derived by `HectonFloatingOrigin.ToRuntimePosition(double3)`, while `GenerateResourceNodesJob` receives `double2 TerrainOriginAbsoluteXZ` and computes payload UVs in double-local terrain space. Tangent basis generation now rejects non-finite normals/tangents before matrix rows enter `ResourceMatrices`.

Loop 11 matrix-bound note: procedural draw bounds now accumulate active `ResourceMatrices` rows directly, including visual-only Dear Lie crystals, using the same diagonal activity predicate as `Hecton_ProceduralOreClusters.shader`. Blackbox validation checks every uploaded matrix row for finite columns before draw submission, while authoritative `OrePositions` remain the gameplay read-model validation path.

Loop 12 shader-bound note: the CPU procedural draw AABB now uses conservative local extents matching the shader-expanded ore primitive: X `0.34`, Y `0.34`, Z `0.82`. The previous half-basis cube assumption was rejected because `Hecton_ProceduralOreClusters.shader` emits a forward spike to local Z `0.82`.

Loop 13 H-Phi note: `ProceduralOreSpawner` no longer retains a private `MapMagicBridge.QuantizedHeightmapPayload` field. The terrain payload is resolved into a local variable, passed directly into spawn scheduling, and discarded after job data is built; persistent geology state remains Vault-handle-only.

Loop 14 evidence-hygiene note: owned `COLD ALLOC` comments now use the exact AGENTS canonical format with em-dash separators for the double-buffered matrix `GraphicsBuffer`s, indirect args `GraphicsBuffer`, and editor-only tuner `StringBuilder`.

Loop 15 job-fence note: geology generation jobs are now registered with `H8Memory.RegisterActiveJob(SystemID.WorldResourceSpawnerRuntime, _spawnJob)`. Raw `_spawnJob.Complete()` calls were replaced by `DispatcherJobFence.TryFinalizeCompleted` for completed late-frame retirement and `DispatcherJobFence.TryComplete(..., forceComplete: true)` for the remaining forced teardown path, because Vault lock release still requires job completion before unlock.

Loop 16 depletion-render note: after a deterministic ore slot is depleted, geology now compacts active rendered rows in `ResourceNodes`, `OrePositions`, `OreTypes`, `ResourceMatrices`, and `CandidateSlots` before rewriting indirect args. Dead zero-matrix rows are no longer left inside `_renderInstanceCount` for shader-side clipping.

Loop 20 DataVault rebind note: `ProceduralOreSpawner` consumes DataVault service replacement through registry hot-swap callbacks, not tick-time registry polling. A pending replacement waits for any scheduled geology generation job to retire, discards old output, clears presentation without touching the old Vault, releases descriptors, reacquires all `71530..71550` lanes from the replacement Vault, writes the 16-byte `GeologyIndirectArgsDTO` row back to Vault, and zeros the GPU indirect args buffer if the Vault is cleared.

Loop 21 disable/rebind note: disabled cleanup no longer rewrites the Vault `IndirectArgs` row while a generation job is scheduled or while a DataVault rebind is pending. Those cases clear scalar presentation state and zero the GPU indirect args buffer only; normal no-job/no-rebind disable still writes the owner Vault row. `Dispose()` clears any queued DataVault rebind reference after descriptor release.

Status: `PENDING COMPILE/RUNTIME PROOF`. Static forbidden-pattern and H-Phi scan summaries are recorded as clean text only for the SHINOBU_153 geology source after the latest polish pass; Unity import, Burst compile, profiler/GCMonitor, Frame Debugger, and player-build proof remain pending.

## 2026-05-20 SHINOBU_140 Master Dispatcher Suppression Vault Lane

SHINOBU_140 owns the master dispatcher telemetry, mock fallback, job-dependency snapshot, and presentation
suppression route. This is a Core phase-governance lane, not a VFX, Audio, or Networking runtime dependency.

Reserved DataVault buffer IDs:

- `70620` `SystemDispatcherMasterJobHandles`: `JobHandle[85]`, dispatcher-owned simulation job handles.
- `70621` `SystemDispatcherMasterDependencyScratch`: `JobHandle[8]`, dispatcher-owned dependency scratch.
- `70622` `SystemDispatcherMasterJobDependencyTelemetry`: `JobDependencyDTO[85]`, 16-byte job-fence telemetry rows.
- `70623` `SystemDispatcherMasterPipelineTelemetry`: `DispatcherTimingDTO[300]`, explicit 32-byte timing ring.
- `70624` `SystemDispatcherMasterPipelineCursor`: `int[1]`, ring cursor.
- `70625` `SystemDispatcherMasterMockTimeDilationSignals`: `MockTimeDilationSignal[8]`, fallback mock topology lane.
- `70626` `SystemDispatcherMasterPresentationSuppression`: `DispatcherPresentationSuppressionDTO[1]`, rollback/health-pressure presentation suppression fact.

Primary DTOs:

- `DispatcherTimingDTO` is explicit 32 bytes with `PreSimMs=0`, `SimWaitMs=4`,
  `PostSimMs=8`, `VisualSyncMs=12`, `FrameId=16`, and padding `20..31`.
- `DispatcherPresentationSuppressionDTO` is explicit 32 bytes with `FrameId=0`, `Flags=4`,
  `GlobalQualityWeight=8`, `Suppression01=12`, `RollbackFlags=16`, and padding `20..31`.
- `MasterRollbackRuntimeStateProbeDTO` is a Core-local explicit 96-byte mirror for reading netcode-owned
  rollback flags from DataVault buffer `70752` without a direct `Hecton8.Networking` source or assembly edge.

Runtime boundary: `SystemDispatcher` reads rollback state only through DataVault buffer `70752`, skips
`VISUAL_SYNC` when rollback/resimulation/hard-resync flags are active, and overwrites buffer `70626` before
the visual-sync decision. Rollback presentation suppression is therefore an O(1) unmanaged fact containing
`VisualSyncSuppressed`, `RollbackFence`, `HealthPressure`, `AudioSuppression`, and `ParticleSuppression`
bits plus the continuous `GlobalQualityWeight`. Netcode remains owner of restore/resimulation command
generation through `RollbackFixedPipelineJob.ExecuteRollback()` and `HeadlessResimulationCommandJob`; the
dispatcher deliberately does not duplicate that loop because it would double-run side effects.

Verification status: static source snapshot is current only as STATIC_SOURCE orientation and is not proof without artifact path, command/tool, timestamp, environment, and output and intentionally red. `Docs/Reports/SHINOBU_140_STATIC_GATE_SUMMARY.json`
now reports `14` scanner rows, including helper-reachability gates for hot `GlobalRegistry` polling and helper-hidden
mid-frame `JobHandle.Complete()` calls. `Rollback_Fence_Compliance` and `Self_Audit_Proof` remain `0 critical / 0 warning`;
canonical `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` embeds the same SHINOBU_140 red gate and self-audit path. A
no-regression baseline exists at `Docs/Reports/SHINOBU_140_STATIC_GATE_BASELINE.json`; only the two new helper scanner rows
were seeded at first measured debt (`Hot_Helper_Registry_Polling=253/0`, `Hot_Helper_Complete=13/0`). Existing scanner
baselines were not raised. The current gate flags `Static_Gate_Regression=2/0`: `Burst_Job_Directives` is `653` over baseline
`645`, and `Hot_Helper_Registry_Polling` is `256` over baseline `253`. Static architecture debt remains red at `2303`
critical and `182` warnings. `Docs/Reports/SHINOBU_140_STATIC_GATE_REGRESSION_ATTRIBUTION.json` is the owner-routed
attribution artifact for those regressions, while `Docs/Reports/SHINOBU_140_SCANNER_SELF_TESTS.json` records four executable
scanner self-tests, including helper-hidden hot registry/complete fixtures and XML-to-summary count drift. Global compile proof
remains blocked by external project errors outside this lane; no `dotnet build` or rebuild was launched for this documentation
and Python-static-tool loop. Stable self-audit proof is mirrored at `Docs/Reports/SHINOBU_140_SELF_AUDIT.xml` and in
`Docs/Archive/Batch010/AgentLogs/LOG_SHINOBU_140__watch29.md`.

## 2026-05-20 SHINOBU_200 Signal Thread Contention Vault Lane

SHINOBU_200 owns the Core signal MPSC contention mock/stress corridor and its black-box telemetry. This route is
not gameplay damage truth, not audio DSP ownership, and not rollback state.

Reserved DataVault buffer IDs:

- `73043` `SignalThreadFrontBytes`: `byte[(64 * 16384) + 64]`, uninitialized.
- `73044` `SignalThreadBackBytes`: `byte[(64 * 16384) + 64]`, uninitialized.
- `73045` `SignalThreadFrontHeaders`: `SignalThreadLocalHeader64[64]`, explicit 64-byte rows.
- `73046` `SignalThreadBackHeaders`: `SignalThreadLocalHeader64[64]`, explicit 64-byte rows.
- `73047` `SignalThreadCommittedSignals`: `SignalWardenMockDamageSignal[4096]`, explicit 64-byte rows.
- `73048` `SignalThreadCommittedCount`: `int[1]`.
- `73049` `SignalThreadContentionTelemetry`: `SignalThreadContentionTelemetryEntry[300]`, explicit 64-byte rows.
- `73050` `SignalThreadContentionTelemetryCursor`: `int[1]`.
- `73051` `SignalThreadContentionTuning`: `SignalThreadContentionTuning64[1]`, explicit 64-byte row.
- `73052` `SignalThreadCoalescenceBuckets`: `int[8192]`, uninitialized, reset over active range by commit job.
- `73053` `SignalThreadOverflowSignals`: `SignalWardenMockDamageSignal[1024]`, explicit 64-byte rows, uninitialized.
- `73054` `SignalThreadOverflowHeader`: `SignalThreadOverflowHeader64[1]`, explicit 64-byte row.
- `73055` `SignalThreadContentionCsvScratch`: `byte[8192]`, uninitialized, cold CSV parser scratch.

Runtime boundary: `GenerateSignalThreadContentionMockJob` writes directly to worker-local byte slices through
`[NativeSetThreadIndex]` and raw pointer copies. The slow overflow fallback uses Vault buffers `73053`/`73054`
only after slice capacity exhaustion. The overflow fallback is sequence-tagged: `SignalThreadOverflowHeader64`
stores monotonic `long` write/read cursors and `SignalWardenMockDamageSignal.OverflowSequence` publishes a slot
only after the payload copy. `SignalThreadLocalCommitJob` walks slices in deterministic worker order, clamps each
worker read to the header's recorded active stride, drains only published overflow rows, uses the supplied sector
origin for fallback AUP hashes, and uses Vault-owned hash buckets
for same-AUP-cell Dear Lie coalescence before publishing a contiguous committed snapshot.
`SignalThreadLocalAupHash.ComputeCellHash(...)` rejects non-finite AUPs, non-finite sector origins, and overflowed
sector-relative float casts by returning sentinel hash `1u` instead of allowing NaN/Infinity into bucket math.
`SignalThreadLocalScratchpad` stores only `VaultGenerationHandle<T>` descriptors for SHINOBU-owned buffers `73043..73055`
and resolves phase-local `NativeArray<T>` views immediately before scheduling, mutation, telemetry readback, CSV parsing,
or editor snapshot reads. Snapshot consumers now use `TryGetCommittedSignalsReadOnly(...)` to receive a
`NativeArray<SignalWardenMockDamageSignal>.ReadOnly` view; the writable snapshot accessor is retained only as a legacy
owner-local surface. It does not retain private static `NativeArray<T>` aliases for this Vault lane; same-vault
generation resolve failures clear the initialized flag and reacquire fresh generation handles on the cold path. Resolve
validation fails explicitly on the first missing or undersized Vault buffer.
`SignalThreadContentionLayoutGuard` verifies the six SHINOBU-owned 64-byte row layouts with `UnsafeUtility.SizeOf` and
`UnsafeUtility.GetFieldOffset` during editor/development cold bootstrap. `SignalThreadContentionHeatmapGizmo` visualizes
committed AUP-cell density in editor Scene View only. `Assets/StreamingAssets/signal_corridor_capacities.csv` is present
with platform/min-stride/max-stride/max-output rows and is parsed through Vault scratch `73055` with `ReadOnlySpan<byte>`;
the loader rejects empty or oversized files, fails on short reads, lowercases platform labels before deterministic FNV-1a
hashing, prefers exact detected platform rows, and uses `pc` as the only fallback row.
`SignalThreadContentionTunerWindow` renders a UI Toolkit waterfall graph directly from the read-only telemetry ring through
`Painter2D`; per-refresh `Label.text` string concatenation was removed from the SHINOBU contention file.
Adjacent Core signal buffers `73038..73042` were migrated off legacy pointer-bearing `VaultBufferHandle<T>` storage:
`SignalTelemetryRingBuffer` and `SignalTuningTable` now persist `VaultGenerationHandle<T>` descriptors and resolve
phase-local `NativeArray<T>` views through `IDataVault.TryResolveHandle(...)`; `SignalTuningTable` no longer stores static
NativeArray aliases for profiles, counts, or CSV scratch.
Core signal frame dispatch no longer virtual-dispatches fallback lanes from `ISignalLane[]`: `FlushPreSimulation()` and
`ClearPostSimulationSnapshots()` use generated generic direct calls for Core-known lanes. Non-generated sibling-owned
typed lanes register cached closed-generic flush/clear delegates into `SignalLaneDispatch[]`, preserving compile-wall
isolation without starving their snapshots. The legacy interface registry remains for cold identity/disposal surfaces,
and telemetry copies use cached closed-generic delegates instead of per-lane interface calls.

Status: `STATIC SOURCE UPDATED - COMPILE BLOCKED BY CPU GUARD`. Route card:
`Docs/ARCHITECTURE/SHINOBU_200_SIGNAL_THREAD_CONTENTION_ROUTE_CARD.md`. Blackbox dump path:
`Docs/AgentLogs/Dump_SHINOBU_200.bin`. Static scan summaries are recorded as clean text only for owned forbidden patterns; Unity import, Burst compile,
profiler, GCMonitor, and runtime microsecond proof remain pending.

## 2026-05-20 SHINOBU_201 SIMD Vectorization Vault Lane

SHINOBU_201 owns the SIMD benchmark, SoA hydrodynamics workspace, vectorized spatial/culling kernels, and
Burst vectorization editor facade. This is a Physics/Core optimization lane. It does not own gameplay truth,
predator cognition, graphics culling ownership, or rollback authority.

Reserved DataVault buffer IDs:

- `71632` `ShinobuSimdLocalPositions`: `SimdFloat3Padded[250000]`, explicit 16 B rows.
- `71633` `ShinobuSimdVelocities`: `SimdFloat3Padded[250000]`, explicit 16 B rows.
- `71634` `ShinobuSimdDragCoefficients`: `float[250000]`, dense scalar lane.
- `71635` `ShinobuSimdOutputForces`: `SimdFloat3Padded[250000]`, explicit 16 B rows.
- `71636` `ShinobuSimdTelemetryRing`: `SimdTelemetryEntry[300]`, explicit 64 B black-box rows.
- `71637` `ShinobuSimdTelemetryCursor`: `int[1]`, clear-memory cursor.
- `71638` `ShinobuSimdMathTolerances`: `SimdMathToleranceDTO[64]`, explicit 16 B cold tuning rows.
- `71639` `ShinobuSimdVisibleIndexMask`: `int[250000]`, transient culling mask.
- `71640` `ShinobuSimdVisibleIndices`: `int[250000]`, transient compacted visible indices.
- `71641` `ShinobuSimdVisibleCount`: `int[1]`, clear-memory count.
- `71642` `ShinobuSimdHydrodynamicTuning`: `SimdHydrodynamicTuningDTO[1]`, explicit 64 B control row.

Primary DTOs:

- `SimdFloat3Padded` is explicit 16 bytes with `float3 Value=0` and pad at `12`.
- `SimdMathToleranceDTO` is explicit 16 bytes with `FormulaHash=0`, `PolynomialDegree=4`,
  `MaxError=8`, and `Flags=12`.
- `SimdTelemetryEntry` is explicit 64 bytes with frame/kernel/entity/timing/throughput fields in
  `0..47` and padding in `48..63`.
- `SimdHydrodynamicTuningDTO` is explicit 64 bytes with fixed-step, quality, drag, buoyancy, base flow,
  turbulence, max-speed, scalar-probe, approximation quality/error, and polynomial degree fields in `0..59`
  with explicit tail padding at `60`.

Runtime boundary: `GenerateMockSimdBenchmarkJob` deterministically fills the 250000-row SoA workspace.
`VectorizedHydrodynamicsJob` consumes local positions, velocity, drag, output-force, and tuning lanes without
GlobalRegistry polling inside the job. AUP localization is isolated in `VectorizedAupLocalizationJob`, which
subtracts `double3` origin before emitting aligned local float lanes. The Dear Lie path replaces heavy
transcendentals with quality-weighted polynomial approximations; `simd_math_tolerances.csv` is parsed cold
from Vault scratch with `ReadOnlySpan<byte>` and updates the unmanaged tuning row. Telemetry records vector
microseconds, scalar-probe microseconds, entities/ms, quality, flags, and state hash into the 300-entry ring;
regression or non-finite vector time dumps `Docs/AgentLogs/Dump_SHINOBU_201.bin`.

Scalability boundary: `GlobalQualityWeight` continuously drives turbulence contribution, approximation
quality, active benchmark interpretation, and scalar-probe comparison. There is no low/high binary hardware
switch in the SIMD kernels.

Verification status: static source and docs are present. Guarded compile, Unity import, Burst Inspector,
player benchmark, profiler, GCMonitor, and ARM64 device proof remain pending.
Loop 8 static polish: scalar hydrodynamic reference now carries synchronous deterministic Burst flags; AI/resource-adoptable
SIMD helper kernels use deterministic float mode; hydrodynamic, spatial, and frustum mask inputs are finite-gated before
NativeArray writes; owned buoyancy/SIMD/editor files are statically clean for `math.sqrt`, `Mathf.Sqrt`, `.normalized`,
`math.normalize`, and `math.length(`. Runtime/Burst/player proof remains pending behind the CPU build guard.

Loop 9 static polish: hydrodynamic SoA ingress/egress, AUP localization, resource map-reduce, and SIMD telemetry now
finite-gate all externally supplied or derived scalar/vector values before NativeArray writes. `RecordSimdTelemetryJob`
uses deterministic Burst mode and writes the 64-byte black-box row through `[WriteOnly, NoAlias]`; only presentation
frustum cull and visible-index compaction remain Fast-mode. `FixedTick` verifies boot-acquired Vault handles with
`HandlesReady()` instead of requesting handles from `GlobalDataVault` in the hot frame path. The active-runtime editor
bridge is wrapped in `#if UNITY_EDITOR`. Compile/player proof remains blocked by CPU guard.

Loop 11 static polish: `EvaluateBuoyancyJob` now finite-gates authority state AUP, velocity, mass, and volume immediately
after the DTO load, then finite-gates tuning AUPs, drag, density, dampening, flow, snap, sleep, and seafloor scalars before
force math. Producer-only debug, force-packet, cold-init, and telemetry lanes received `[WriteOnly, NoAlias]` where no
element reads occur. Static scan summaries were recorded; compile/player proof remains blocked because the CPU gate could not prove a
safe build window.

Loop 12 static polish: buoyancy force-packet emission no longer uses an atomic append in `EvaluateBuoyancyJob`.
The evaluator writes one candidate packet per scheduled `workIndex`, clears its own candidate slot on entry, and leaves
counter mutation to `CompactBuoyancyForcePacketsJob`. Runtime scheduling is now `EvaluateBuoyancyJob ->
CompactBuoyancyForcePacketsJob -> ReduceBuoyancyTelemetryJob`, preserving the dense force-packet prefix consumed by the
apply bridge without a main-thread `Complete()`. Static scan summaries are recorded as clean text only for `Interlocked`, `System.Threading`, the old
force-packet append helpers, forbidden sqrt/normalize/string parsing/native allocation patterns, and runtime proof remains
blocked by CPU guard.

Loop 13 static polish: the `EvaluateBuoyancyJob` invalid/sleep/non-finite data-dependent return ladder was replaced by
`hasBody`, `wasSleeping`, `simulateBody`, `simulateWeight`, `sleepNow`, `mathFinite`, and `forceOutputValid` masks.
`EntityHashID` is preserved for forensic identity; force vectors, flow, submerged fraction, depth, sleep score, net force,
and packet candidates are zeroed or skipped through masks for rows that must not simulate. Remaining evaluator branches are
structural buffer/bounds guards only. Static branch/forbidden scans and brace count passed; compile/player proof remains
blocked by CPU guard.

Loop 14 static polish: Dewey audit closure finite-gates mock `SurfaceAUP` before state writes, counts `FlagNonFinite`
telemetry rows through a frame-only mask so anonymous corrupt rows can still trigger dumps, and routes buoyancy force-packet
drain through the existing `ShinobuBuoyancyBodyBindings` Vault buffer. The apply bridge now validates cached
`RigidbodyIndex` by `StateIndex` and `EntityHashID` before falling back to folded-hash resolution, converting warm packet
body lookup from dictionary/possible O(N) scan to O(1) index validation. Compile/player proof remains blocked by CPU guard.

Loop 16 static polish: `CompactBuoyancyForcePacketsJob` no longer branches per candidate on `IsValidPacket(packet)`.
The reduction now sanitizes each candidate, field-selects sanitized versus preserved prefix data, and advances `write`
through `math.select(0, 1, valid)`. The earlier packet-capacity `math.select` note is superseded by Loop 17 because
C# evaluates `ForcePackets.Length` before `math.select` can protect default NativeArray metadata. Durable rationale was
corrected so invalid ingress preserves `EntityHashID` for black-box forensics and masks physics/queue output through
`simulateBody` / `forceOutputValid`. Compile/player proof remains blocked by CPU guard.

Loop 17 static polish: `CompactBuoyancyForcePacketsJob` reads `ForcePackets.Length` only after a structural
`if (ForcePackets.IsCreated)` guard. Candidate validity remains mask-selected inside the bounded reduction loop through
`SelectPacket` and `write += math.select(0, 1, valid)`. No DTO layout, Vault buffer, dependency chain, or assembly
reference changed. Static invalid-metadata guard scan summary is recorded as clean text only; compile/player proof remains blocked by CPU guard.

Loop 18 static polish: `BuoyancyForcePacketDTO._pad0` is now scrubbed to zero in `SanitizePacket` and selected through
`SelectPacket` together with semantic fields when a valid candidate is compacted. This does not change the explicit
128-byte layout, Vault lane, or dependency graph; it prevents stale slack bytes from surviving in byte-for-byte forensic
or native payload copies. Owned buoyancy DTO/job property scan found no getter/setter debt. Compile/player proof remains
blocked by CPU guard.

Loop 19 static polish: `CompactVisibleIndicesJob` no longer uses the per-candidate branch
`if (value >= 0 && write < VisibleIndices.Length)`. Optional NativeArray metadata remains protected by structural
`IsCreated`/capacity guards, while candidate validity is mask-selected inside the bounded reduction loop and `write`
advances through `math.select(0, 1, valid)`. `VisibleIndices` is intentionally read/write `[NoAlias]` now because the
mask path preserves the existing prefix slot. Broad Physics/AI alias scan was read-only; no non-SHINOBU AI owner file was
edited. Compile/player proof remains blocked by CPU guard.

Loop 20 static polish: `ReduceBuoyancyTelemetryJob` no longer uses a lazy ternary to guard `DebugForces.Length`.
It now initializes `count` to zero and reads `DebugForces.Length` only after `DebugForces.IsCreated` is true. Telemetry
mask math, non-finite counting, ring writes, DTO layouts, Vault buffers, and dependency chain are unchanged. Static
stale-metadata, forbidden-pattern, brace/preprocessor, and whitespace scan summaries are recorded as clean text only; compile/player proof remains
blocked by CPU guard.

Loop 21 static polish: `BuoyancyDisplacementRuntime.cs` no longer imports `Hecton8.World`. The runtime uses
`Hecton8.Core.HectonFloatingOrigin` through the existing Core import for sector AUP and debug runtime-position
conversion, so AUP precision is preserved without a direct World namespace edge. No DTO layout, Vault buffer, Burst job,
or dependency chain changed. Compile/player proof remains blocked by CPU guard.

Loop 22 static polish: `NativeDisableParallelForRestriction` use in `BuoyancyDisplacementJobs.cs` now has explicit
partition-invariant comments. Mock seeding documents one lane -> `States[index]`; evaluator state/debug writes document
the injective `workIndex * max(1, stride) + offset` mapping and dependency fence before debug reads. No DTO layout, Vault
buffer, Burst directive, or dependency chain changed. Compile/player proof remains blocked by CPU guard.

Loop 23 static polish: `VectorizedFrustumCullJob` now uses a fixed six-plane culling loop with `inRange`/`math.select`
to make inactive plane slots neutral, while preserving a structural empty-plane guard before any `Planes[]` read. Runtime
scheduler ternaries for active count, evaluation offset, and mock count were folded into `math.select` over safe scalar
operands. No Vault ID, DTO layout, global authority route, or dependency chain changed; compile/player proof remains
blocked by CPU guard.

Loop 24 static polish: culling and helper math ingress was vaccinated. `VectorizedFrustumCullJob` now checks
`Planes.IsCreated` before reading plane metadata, and `EstimateObjectHeightMeters`, `FastSpeed`, `SinPolynomial`, and
`ExpNegPolynomial01` finite-gate helper inputs before rsqrt/floor/saturate/lerp paths. No DTO layout, Vault buffer, or
dependency chain changed; compile/player proof remains blocked by CPU guard.

Loop 25 static polish: Bacon audit closure. Reusable SIMD jobs now guard required NativeArray lanes before first
`.Length` reads; `GenerateMockSimdBenchmark()` is editor-only/manual blocking sync and boot/editor complete points are labeled; buoyancy force packet drain resolves
the physics manager once before the packet loop; `BuoyancyDisplacementLayout` validates offsets for every buoyancy
runtime DTO instead of only `BuoyancyStateDTO`. No Vault buffer IDs or DTO sizes changed. Compile/player proof remains
blocked by active compiler processes.

Loop 26 static polish: SIMD DTO layout validator added. `SimdVectorizationLayout` cold-validates exact sizes and
manual field offsets for `SimdFloat3Padded` (16B), `SimdMathToleranceDTO` (16B), `SimdTelemetryEntry` (64B), and
`SimdHydrodynamicTuningDTO` (64B). `BuoyancyDisplacementRuntime` handle acquisition/readiness now requires both
buoyancy and SIMD layout validators; the Burst Vectorization X-Ray editor audit reports validator OK/FAIL. Vault IDs,
buffer capacities, DTO sizes, and scheduler dependencies are unchanged. Compile/player proof remains pending; no
build or rebuild was launched for this static ABI pass.

Loop 27 static polish: cold IO and compile-wall boundaries audited. Existing material-volume CSV, SIMD-tolerance CSV,
shared scratch file read, black-box dump, and SIMD telemetry dump paths are now labeled as cold tuning, fault-only, or
editor/benchmark-only surfaces. Parent/editor/physics asmdefs were reviewed; the buoyancy/SIMD files still inherit the
broader `Hecton8.Core` assembly because two SHINOBU files are partial injections into existing core-owned classes.
No direct sibling-domain import was introduced, and a local physics asmdef split is recorded as unsafe without an
integrator-owned bridge refactor. No Vault IDs, DTO sizes, or scheduler dependencies changed.

Loop 28 static polish: explicit hydrodynamics lane packing added for the editor X-Ray benchmark. `VectorizedHydrodynamicsLane4Job`
processes four entities per scheduled lane using `float4` x/y/z/drag registers over the existing SHINOBU SIMD Vault
buffers, and `SimdTranscendentalApproximator.SinPolynomial(float4, ...)` mirrors the scalar polynomial/current Dear Lie.
`GenerateMockSimdBenchmark()` now rounds benchmark count to a multiple of four, schedules lane groups, and records the
vectorized entity count in `SimdTelemetryEntry`. No Vault IDs, DTO sizes, or player fixed-tick force semantics changed.
Compile/Burst Inspector proof remains pending; no build or rebuild was launched for this static pass.

Loop 29 static polish: the lane-4 hydrodynamics job now marks writable `Velocities` and `OutputForces` lanes with
`NativeDisableParallelForRestriction` and a source-adjacent partition proof. Scheduled lane `i` owns rows
`[i * 4, i * 4 + 3]`; the benchmark schedule count is rounded down to `vectorizedCount / 4`, so row writes are injective
and non-overlapping. `[NoAlias]` remains the cross-array alias proof, while the suppression covers Unity's per-index
ParallelFor safety contract. No Vault IDs, DTO sizes, telemetry ABI, or player fixed-tick force semantics changed.
Compile/Burst Inspector proof remains pending; no build or rebuild was launched because CPU sampled above the local gate.

Loop 30 static polish: the Burst Vectorization X-Ray editor facade removed the scalar-probe slider lambda and uses a
named `ChangeEvent<float>` callback. The fixed 1024-char readout writer now bounds-checks `AppendFixed2` fractional
digit writes. This is editor-only facade hygiene; no Vault IDs, DTO sizes, Burst jobs, player fixed-tick force semantics,
or runtime quality curves changed. Compile/Burst Inspector proof remains pending; no build or rebuild was launched.

Loop 31 static polish: `BuoyancyDisplacementRuntime` no longer persists legacy pointer-bearing
`VaultBufferHandle<T>` fields or routes through obsolete `.Resolve(vault)` bridges. The 22 SHINOBU buoyancy/SIMD lanes
are stored as 16-byte `VaultGenerationHandle<T>` descriptors, existing descriptors are validated through
`IDataVault.TryResolveHandle` before cold reacquisition, and all job scheduling, force drain, CSV hydration, telemetry,
black-box, and editor gizmo paths use method-local `NativeArray<T>` views only for the execution phase that consumes
them. Owner teardown and DataVault replacement release descriptors through `IDataVault.ReleaseBuffer`; same-vault service
notifications keep live descriptors. No Vault IDs, DTO sizes, Burst jobs, quality curves, or force semantics changed.
Static scan is clean for `VaultBufferHandle`, `GetBufferHandle`, `.Resolve(`, and handle `.IsCreated` in the owned
runtime file. Compile/Burst Inspector proof remains pending; no build or rebuild was launched because CPU exceeded the
local gate.

Loop 32 static polish: descriptor reacquisition now respects `IDataVault.IsAllocationLocked`. If the Vault is locked,
`EnsureVaultDescriptor` adopts only an already-existing descriptor through `TryGetGenerationHandle` plus
`TryResolveHandle` and capacity validation; it does not call `GetGenerationHandle` or attempt buffer growth under a
compaction/AUP allocation fence. No Vault IDs, DTO sizes, Burst jobs, quality curves, force semantics, or lifecycle
release routes changed. Compile/Burst Inspector proof remains pending; no build or rebuild was launched because CPU
exceeded the local gate.

Loop 33 static polish: runtime Vault readiness now retries cold boot and stale generation descriptor recovery after
allocation locks clear instead of leaving a registered but inert buoyancy solver. Cold/manual mutators, including
emergency mock seeding, SIMD X-Ray benchmark generation, material CSV hydration, SIMD tolerance hydration, and DataVault
service replacement, refuse mutation while `IDataVault.IsAllocationLocked` is true. No Vault IDs, DTO sizes, Burst math,
quality curves, or force packet ABI changed. Compile/player proof remains pending behind the CPU gate.

Loop 34 static polish: stale descriptor repair now tries current metadata adoption before create/grow fallback.
`TryAdoptExistingVaultDescriptor` uses `TryGetGenerationHandle` + `TryResolveHandle` + capacity proof, and
`GetGenerationHandle` remains restricted to genuinely absent/undersized buffers and unreachable while allocation is
locked. No Vault IDs, DTO sizes, Burst jobs, quality curves, or force semantics changed. Compile/player proof remains
pending behind the CPU gate.

Loop 35 static polish: Task 07 packed query proof added without changing cross-domain ownership. Existing lane-1
`VectorizedSpatialQueryJob` remains intact for current callers; new `VectorizedSpatialQueryLane4Job` processes four
prey positions per scheduled lane using `float4` x/y/z registers, finite masks, branchless squared-distance radius
tests, `[NoAlias]`, and `[NativeDisableParallelForRestriction]` with the invariant that scheduled lane `i` owns rows
`[i * 4, i * 4 + 3]`. No new Vault IDs, DTO sizes, telemetry ABI, runtime scheduling, or AI-domain route was introduced.
Compile/Burst Inspector proof remains pending; no build or rebuild was launched because CPU exceeded the local gate.

Loop 36 static polish: lane-1 spatial query fallback now matches the lane-4 finite-mask contract. `VectorizedSpatialQueryJob`
keeps prey and predator finite masks and folds them into the branchless valid-mask expression, preventing NaN/Infinity
positions from being sanitized to origin and reported as valid targets. No Vault IDs, DTO sizes, telemetry ABI, runtime
scheduling, or AI-domain route changed. Compile/Burst Inspector proof remains pending; no build or rebuild was launched
because CPU was 100% and a `dotnet` process was active.

Loop 37 static polish: lane-4 spatial query now supports `ceil(Count / 4)` scheduling instead of flooring to a multiple
of four. Tail lanes clamp reads to the last valid row and sanitize invalid/out-of-range prey coordinates through
`safePx/safePy/safePz` before squared-distance math, preventing stale tail masks and poisoned SIMD registers. No Vault
IDs, DTO sizes, telemetry ABI, runtime scheduling, or AI-domain route changed. Compile/Burst Inspector proof remains
pending behind the CPU gate.

Loop 38 static polish: the lane-4 spatial query tail path no longer uses conditional stores. Tail lanes clamp
out-of-range indices to the last valid row and use cascading `math.select` masks so duplicate stores preserve the last
in-range value. This keeps non-multiple-of-four query counts covered without a scalar tail job and without
`if (laneNInRange)` writes in the packed query body. No Vault IDs, DTO sizes, telemetry ABI, runtime scheduling, or
AI-domain route changed. Compile/Burst Inspector proof remains pending; no build or rebuild was launched because CPU
exceeded the local gate.

Loop 39 static polish: the lane-4 hydrodynamics kernel now supports `ceil(Count / 4)` scheduling instead of hiding
tails through benchmark-side rounding. Tail lanes clamp to the last valid row and duplicate-store identical final
velocity/force values within the same scheduled lane. The SIMD X-Ray benchmark now generates, schedules, hashes, and
records the full count rather than a rounded-down vector count, and `RecordSimdTelemetryJob` stores its cursor as a
strict circular index inside the 300-frame telemetry ring. No Vault IDs, DTO sizes, quality curves, force packet ABI,
or assembly references changed. Compile/Burst Inspector proof remains pending behind the CPU gate.

Loop 40 static polish: Task 08 now has an explicit eight-object cull lane. `VectorizedFrustumCullLane8Job` processes
eight AABB centers/extents as two `float4` groups across up to six packed planes, finite-gates centers/extents/planes,
uses branchless `math.step`/`math.select` visibility masks, and writes duplicate-safe tail visible-index rows through a
documented eight-row ParallelFor ownership contract. Existing lane-1 `VectorizedFrustumCullJob`, renderer/BRG ownership,
Vault IDs, DTO sizes, telemetry ABI, and runtime scheduling remain unchanged. Compile/Burst Inspector proof remains
pending; no build or rebuild was launched because CPU exceeded the local gate.

## 2026-05-20 SHINOBU_205 AUP Precision Vault Lane

SHINOBU_205 reserves owner-local Vault IDs `73200..73208` for AUP precision localization proof. The earlier
candidate range `73053..73061` is rejected because `73053`/`73054` are already owned by SHINOBU_200 SignalWarden
overflow. Static range scan found `73200..73208` clear before adoption.

- `73200` `AupPrecisionTargetAups`: `double3[capacity]`, uninitialized, authoritative target samples.
- `73201` `AupPrecisionRuntimeState`: `AupPrecisionRuntimeStateDTO[1]`, explicit 64-byte control row.
- `73202` `AupPrecisionLocalOffsets`: `float3[capacity]`, uninitialized, localized output only.
- `73203` `AupPrecisionResultFlags`: `uint[capacity]`, uninitialized result bitfield.
- `73204` `AupPrecisionTelemetryRing`: `AupPrecisionTelemetryEntry[300]`, explicit 64-byte black-box ring.
- `73205` `AupPrecisionToleranceProfiles`: `AupToleranceProfileDTO[64]`, explicit 64-byte cold tuning rows.
- `73206` `AupPrecisionCsvScratch`: `byte[16384]`, uninitialized cold CSV staging.
- `73207` `AupPrecisionMockExtremeAups`: `double3[capacity]`, uninitialized +/-100 km mock samples.
- `73208` `AupPrecisionFaultCounter`: `AupPrecisionFaultCounter64[1]`, explicit 64-byte cache-line counter row.

Runtime boundary: `AupPrecisionVault` is a handle-only static route in Core. It requests `VaultGenerationHandle<T>`
records from `GlobalDataVault`, resolves transient `NativeArray<T>` views only for the scheduling/cold editor phase,
and stores no private persistent arrays. `TryScheduleLocalization` writes observer AUP once, schedules
`LocalizeAupCoordinatesJob`, then chains `AupPrecisionTelemetryFoldJob` without a caller-thread `Complete`.
No hot job queries `GlobalRegistry`; no sibling runtime assembly route is introduced.

Precision boundary: localization always executes `double3 local = targetAup - observerAup` before any `float3`
downcast. `GlobalQualityWeight` only changes the continuous distance gate `1000..5000m` and kernel estimate; it
does not switch to float-first authority at low quality. Fault telemetry dumps to `Docs/AgentLogs/Dump_SHINOBU_205.bin`.

Route card: `Docs/ARCHITECTURE/SHINOBU_205_AUP_PRECISION_ROUTE_CARD.md`.
Verification status: static source scan summaries are recorded for direct AUP/double3 `(float3)` casts, runtime component `(float)` AUP casts, and owned DTO layout hazards.
Strict `Transform.position` authority scan still reports 79 runtime blockers for owner-domain handoff after player/camera observer fallbacks were rewired to player pose snapshots/current AUP.
Editorless CI gate `Tools/AupPrecisionGate_SHINOBU_205.py` writes `Docs/Reports/AUP_PRECISION_SCAN_SHINOBU_205.json` and fails hard when direct AUP float casts, runtime component AUP float casts, or strict Transform authority reads exceed zero. Last recorded CLI result in this document: `FAIL_STATIC_GATE`, 1986 files scanned, direct casts 0, runtime component casts 0, editor reviews 5, strict Transform blockers 79 across 55 files; rerun before using that file count as current. Fixture proof: `Tools/TestAupPrecisionGate_SHINOBU_205.py` writes `Docs/Reports/AUP_PRECISION_GATE_SELF_TEST_SHINOBU_205.json` and was reported as a static/Python fixture pass.
Unity import, Burst compile, Play Mode, profiler/GC, and ARM64 device proof remain pending behind the CPU build guard.

## 2026-05-20 SHINOBU_203 Jacobi Convergence Vault Lane

SHINOBU_203 owns convergence control state and residual worker lanes for iterative Jacobi-family solvers in
power distribution, logistics pressure stabilization, and abyssal thermal voxel diffusion. This lane is
solver-control and telemetry ownership only; it does not create gameplay power truth, thermal source truth,
or cross-domain rollback authority.

Reserved owner-local DataVault buffer IDs:

- `731078` `PowerSolverConvergenceState`: `SolverConvergenceStateDTO[1]`, explicit 16 B row.
- `731079` `PowerSolverResidualSamples`: `SolverResidualSlot64[128]`, uninitialized lane; each worker residual slot is one 64 B cache line and is cleared before each pass.
- `70052` `AbyssalThermalSolverConvergenceState`: `ThermalSolverConvergenceStateDTO[1]`, explicit 16 B row.
- `70053` `AbyssalThermalSolverResidualSamples`: `ThermalResidualSlot64[128]`, uninitialized lane; each worker residual slot is one 64 B cache line and is cleared before each pass.
- `70054` `AbyssalThermalSolverDumpLatch`: `int[1]`, uninitialized lane; stores the last dumped solver fault key to prevent repeated black-box file writes for the same continuous fault.

Existing power counter lane `731068` now uses slot `5` as `CounterMaxIterationStreak` for SHINOBU_203
five-frame dump gating and slot `6` as `CounterDumpedFaultMask` for one-dump-per-continuous-fault gating.
These are scalar counters inside the owner-local power counter buffer, not new persistent arrays.

Primary DTOs:

- `SolverConvergenceStateDTO` is explicit 16 bytes with `MaxResidualFloat=0`,
  `PreviousResidualFloat=4`, `Omega=8`, `IterationCount=12`, and `FaultFlags=14`.
- `ThermalSolverConvergenceStateDTO` mirrors the same 16-byte layout and is validated during abyssal
  thermodynamics cold enable alongside `ThermalCellDTO`.
- `SolverResidualSlot64` and `ThermalResidualSlot64` are explicit 64-byte rows with
  `MaxResidualFloat=0`, `FaultFlags=4`, and 56 bytes of manual tail padding. They isolate per-worker
  residual writes from false sharing on ARM64/x86 cache lines.

Runtime boundary: relaxation jobs write finite residual maxima into `[NativeSetThreadIndex]` padded worker slots.
Fault flags, not `NaN`/`Infinity` values, carry non-finite/divergence state; black-box residual telemetry is bounded.
No solver writes `NaN` into pressure, power, or thermal double buffers. Abyssal heat diffusion performs one double-buffer Jacobi relaxation per scheduled pass with Jacobi-safe dynamic damping (`omega` 0.55..1.0); it does not run a hidden in-job
`JacobiIterations` loop, and it sanitizes ambient/max-stable tuning scalars before deriving the runaway limit. Reduction jobs consume the 128-slot map-reduce lanes, damp omega when residual grows, and mark terminal
convergence/divergence state so later ping-pong passes copy forward instead of repeating full-grid math.
Touched SHINOBU_203 solver boundaries sanitize non-finite quality, demand, smoothing, hazard radius/temperature,
abyssal grid resolution, source radius/intensity/falloff/conductivity, and abyssal tuning scalars before they enter continuous curves, integer index math, or write lanes.
Residual init, clear, and reduction jobs schedule over the 128-slot lane, not full node/voxel counts.
The lane adds no direct sibling Runtime assembly dependency; shared helpers remain source-local or
contract-facing, and Core enum edits were avoided during this batch by recording owner-local numeric IDs here.

Scalability boundary: `GlobalQualityWeight` continuously controls pass count, residual tolerance, cadence,
and Jacobi-safe damping. Every processed node/voxel contributes its already-computed residual to convergence
proof; sampled-only residual convergence is forbidden after audit because it can hide divergent unsampled cells.
Low quality uses lower cadence, looser tolerance, and stronger damping; middle quality tightens tolerance and cadence;
high/ultra quality approaches `omega = 1.0` with the strictest tolerance without binary hardware branches.

Blackbox boundary: thermal power and abyssal thermal faults dump the 300-frame ring to
`Docs/AgentLogs/Dump_SHINOBU_203.bin` as the XML-task alias. NaN/divergence dump immediately; max-iteration
exhaustion dumps after five consecutive residual-over-tolerance capped frames. Power uses counter slot `6`,
and abyssal thermal uses Vault buffer `70054`, to suppress repeated disk writes for the same continuous fault.
Existing owner dump paths remain valid where already present.

Status: `STATIC SOURCE UPDATED - COMPILE BLOCKED BY CPU GUARD`. Static scanner output exists at
`Docs/Reports/MATH_OPTIMIZATION_REPORT.json` with `blind_iteration_candidates = 0`. Unity import, Burst
compile, profiler, GCMonitor, and player-build proof remain pending; guarded dotnet retry was not launched
because local CPU load stayed above the project 50% build gate.

## 2026-05-20 SHINOBU_210 Offline Module Damage Baker Contract Lane

SHINOBU_210 owns offline Editor baking of habitat module damage mesh states. This is not gameplay structural truth,
not a physics runtime, and not rollback state.

Reserved owner-local IDs are documented for future baked-data import, but this pass does not edit the central
`BufferID` enum and does not request gameplay Vault buffers:

- `73320` `HabitatDamageStateMappings`: `ModuleDamageStateMappingDTO[4096]`, explicit 32-byte rows.
- `73321` `HabitatDamageHullProxies`: `HabitatDamageHullDTO[32768]`, explicit 64-byte rows.
- `73322` `HabitatDamageBakeTelemetryRing`: `HabitatDamageBakeTelemetryEntry[300]`, explicit 64-byte rows.
- `73323` `HabitatDamageBakeTelemetryCursor`: `int[1]`.

Runtime boundary: `HabitatDamageBakedContracts.cs` contains only blittable DTOs, numeric state enum, reserved ID
constants, and `HabitatDamageMeshStateResolver`; it has no UnityEngine mesh/object dependency. The managed
`HabitatDamageBakeManifest` lives in the Editor assembly only. The previous runtime `MonoBehaviour` mesh-swap bridge
was removed from SHINOBU_210 ownership because structural/rendering owners must consume integer state and mesh hashes,
not a direct prefab controller.

Route card: `Docs/ARCHITECTURE/OFFLINE_MODULE_DAMAGE_BAKER_SHINOBU_210.md`.
Status: `STATIC SOURCE UPDATED - PENDING UNITY IMPORT / PROFILER PROOF`.

## 2026-05-20 SHINOBU_204 Core Replay and Navigation ABI Addendum

`PrologueSequenceContracts.cs`, `InertialNavigationContracts.cs`, and `DodReplayRecorder.cs` no longer expose
compiler-owned Sequential DTO rows. Prologue orbital/reentry/complete snapshots, compass state, inertial navigation
snapshots, and DOD replay sidecars are now source-owned explicit layouts. Inertial `double3` AUP lanes remain at offsets
divisible by 8 (`0/24/48/72` in `CompassStateDTO`, `0/24/48` in `InertialNavigationSnapshot`); replay `long`/`ulong`
hash and timestamp lanes remain 8-byte aligned.

Runtime boundary: this is ABI proof only. No new owner, registry route, Vault allocation, managed sidecar, or replay
schema widening was introduced. File sizes were preserved where replay/navigation contracts may already be consumed by
tools or runtime readers. Static verification reports 0 `LayoutKind.Sequential` hits and 0 unaligned 8-byte
`FieldOffset` lanes in the three touched files; Unity import/Burst/player proof remains blocked behind the existing
dependency wall and rebuild gate.

## 2026-05-20 SHINOBU_204 ArchitectEye Diagnostics ABI Addendum

ArchitectEye diagnostics payload rows are now explicit source-owned layouts. `ArchitectEyeQuadInstance` remains an
80-byte GPU instance stride with five 16-byte `float4` lanes at offsets `0/16/32/48/64`. `ArchitectEyeBlackBoxEntry`
and `ArchitectEyeRuntimeState` remain 64-byte rows for black-box forensic capture and runtime state. Core Contracts and
Persistence empty assembly markers were converted to explicit Size=1 to remove marker-only Sequential noise.

Runtime boundary: shader/GraphicsBuffer stride was preserved; no shader ABI widening, no managed sidecar, and no new
Vault lane were introduced. Unity-owned `NativeArray`/`NativeQueue`/generic NativeContainer wrapper structs remain
outside this addendum because their internal safety-handle layout is owned by Unity and must not be frozen by a
blind explicit-offset patch.

## 2026-05-20 SHINOBU_204 Burst Callback Handle ABI Addendum

The source-owned `BurstCallback` wrapper is now explicit Size=8 with its `FunctionPointer<BurstCallbackDelegate>` lane
at offset 0. The `BurstCallbackQueue` and nested `ParallelEventWriter` were intentionally left Sequential because they
embed Unity `NativeQueue`/parallel-writer internals and are not persisted DTO rows.

## 2026-05-20 SHINOBU_204 Crash Telemetry and Toxic Chemistry ABI Addendum

`CrashTelemetryBuffer.cs` crash export/live telemetry headers and `ToxicOutgassingChemistryTypes.cs` chemistry rows are
now explicit source-owned layouts. Toxicity grid/source/telemetry DTOs keep `double3` AUP lanes at offset 0 and 64-bit
pads at offsets divisible by 8. Existing toxic exposure/bioluminescence signal payloads were already explicit and
unchanged.

Runtime boundary: existing crash dump and toxic chemistry buffer sizes were preserved. No new chemistry owner, no
managed fallback object, no shader variant, and no Vault lane were introduced.

## 2026-05-20 SHINOBU_204 Material and TBDR Culling ABI Addendum

Material response DTOs in `ShinobuMaterialResponseRuntime.cs` are now explicit layouts. Fixed TBDR culling/shader rows
in `TBDRPipelineSurgeonTypes.cs` are also explicit, including vertex budgets, POI transforms, mock camera matrices,
AUP GPU localization input, texture streaming slices, telemetry/tuner snapshots, shader budget globals, and indirect
draw args.

Runtime boundary: shader/GraphicsBuffer strides were preserved. `MockScatterBuffer` remains Sequential by design because
it aggregates `NativeArray` wrappers whose internal layout is Unity-owned; it is not a persisted or shader DTO row.

## 2026-05-20 SHINOBU_204 Audio Virtualization Contract ABI Addendum

`AudioVirtualizationContracts.cs` is now explicit-layout for all virtual voice contract DTOs. Voice ingress/state,
sort keys, selected physical voice rows, statistics, acoustic telemetry, tuning snapshots, CSV rows, echo taps, and
mock acoustic payloads preserve their existing byte sizes while moving layout ownership into source. Embedded
`AcousticAup` rows remain aligned at offsets `0`, `40`, and `80` where present.

Runtime boundary: this was a contract-layout patch only. No DSP behavior, voice budget, sibling assembly reference, or
Vault ownership route changed. The editor smoke test now checks for explicit 48-byte voice DTO and 16-byte sort key
layout markers instead of obsolete Sequential source strings.

## 2026-05-20 SHINOBU_204 Audio DSP and Propagation ABI Addendum

Fixed audio DSP/propagation rows in Adaptive Stem, Echolocation Raymarch, Acoustic Portal Propagation, and Depth-Stress
Granular Synthesis are now explicit layouts. Existing byte sizes were preserved; `AcousticAup` portal lanes remain at
offsets `0`, `40`, and `80`, and `KineticImpactSineOscillatorState.Phase` remains a double at offset `0`.

Runtime boundary: audio jobs, NativeArray wrappers, and physical simulation behavior were not changed. This addendum only
source-owns fixed DTO/state byte maps used by the existing SDF/Sabine/oscillator approximation paths.

## 2026-05-20 SHINOBU_204 Scanner Route ABI Addendum

`ScannerDataMiningRouter.cs` scanner DTOs are now explicit layouts. Scan result, scannable metadata, spatial entity,
VFX, active state, mock scanner/tool input, SDF occlusion, query stats, telemetry, and settings rows preserve their
existing byte sizes while source-owning AUP, sector hash, depletion, and telemetry offsets.

Runtime boundary: scanner math and owner routes were not changed. The scanner continues to use the existing SDF/mock
occlusion path instead of Unity physics queries.

## 2026-05-20 SHINOBU_202 Acoustic Echo Vault Descriptor Addendum

Acoustic sensory runtime `AcousticEchoLocationRuntime` no longer persists legacy `VaultBufferHandle<T>`
descriptors. Four Vault lanes (`AcousticEchoFrameTaps`, `AcousticEchoPendingTaps`,
`AcousticEchoTrailState`, and `AcousticEchoBlackBox`) are stored as pointer-free
`VaultGenerationHandle<T>` descriptors and resolved only as method-local `NativeArray<T>` views through
`IDataVault.TryResolveHandle`.

The static echo queue drains pending taps into a phase-local frame tap view before scheduling the Burst tracking job.
Blackbox rows and dump serialization resolve a fresh generation-checked view per write/dump path. Dispose and DataVault
replacement release only the descriptors owned by this runtime; active tracking fences are completed before old
descriptors are released so Vault relocation never races a scheduled tap scan.

## 2026-05-20 SHINOBU_202 Path Funnel Navmesh Vault Descriptor Addendum

Path funnel runtime `PathFunnelNavmeshRuntime` no longer persists legacy `VaultBufferHandle<T>` descriptors. Five owned
Vault lanes (`PathFunnelActivePaths`, `PathFunnelCellMasks`, `PathFunnelInvalidations`,
`PathFunnelTelemetryRing`, and `PathFunnelRuntimeState`) are stored as pointer-free
`VaultGenerationHandle<T>` descriptors and resolved only as phase-local `NativeArray<T>` views.

The WFC outpost grid is an external read dependency, so the fast tick now creates a transient `VaultGenerationHandle<byte>`
through `TryGetGenerationHandle<byte>` and immediately resolves it through `TryResolveHandle`. No direct `TryGetBuffer`
view or persistent WFC grid descriptor remains in the path-funnel manager.

## 2026-05-20 SHINOBU_202 WFC Laser Cut Vault Descriptor Addendum

Tool runtime `WfcLaserCutRuntime` no longer persists legacy `VaultBufferHandle<T>` descriptors or converts cached Vault
metadata into raw cut-progress/blackbox pointers. The two owned lanes (`WfcDoorCutProgress01` and
`WfcLaserCutBlackBox`) are stored as pointer-free `VaultGenerationHandle<T>` descriptors and resolved into local
`NativeArray<T>` views for each cut attempt.

The laser-cut shader overkill scalar now uses a continuous `HomeostasisBrain.GlobalQualityWeight` smoothstep curve
multiplied by stress headroom. This replaces the previous discrete `GlobalRegistry.ScalabilityTier` branch and keeps
visual degradation continuous while the gameplay progress lane stays generation-validated.

## 2026-05-20 SHINOBU_202 Procedural Ladder Climb Vault Descriptor Addendum

Animation locomotion runtime `ProceduralLadderClimbRuntime` no longer persists legacy `VaultBufferHandle<T>`
descriptors. Five Vault lanes (`LadderClimbIkInput`, `LadderClimbIkOutput`, `LadderAUPs`,
`LadderClimbIkTelemetryRing`, and `LadderClimbIkTelemetryCursor`) are stored as pointer-free
`VaultGenerationHandle<T>` descriptors and resolved into local `NativeArray<T>` views only at input write, output read,
telemetry dump, and IK job scheduling boundaries.

DataVault loss or replacement now completes any outstanding IK job before releasing old descriptors, preventing Vault
relocation from racing a scheduled solve over ladder AUP or telemetry buffers.

## 2026-05-20 SHINOBU_202 Tool Haptics Vault Descriptor Addendum

Tool haptics runtime `ToolHapticsRuntime` no longer persists legacy `VaultBufferHandle<T>` descriptors. The two haptic
command lanes (`ToolHapticFrontCommands` and `ToolHapticBackCommands`) are stored as pointer-free
`VaultGenerationHandle<T>` descriptors and resolved into local `NativeArray<HapticCommand>` views per enqueue, merge,
tick, and readback operation.

DataVault loss or replacement releases the previous front/back descriptors before caching a new Vault reference.
The returned `ReadOnlySpan<HapticCommand>` snapshots are still phase-local views over the resolved front buffer and no
manager-owned pointer metadata remains.

## 2026-05-20 SHINOBU_155 Compile-Wall And Burst Alias Addendum

Player death reconciliation remains inside `Hecton8.Physiology` without direct sibling runtime assembly references. The runtime asmdef references `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity Burst/Collections/Jobs/Mathematics only; the editor asmdef is editor-only and references the runtime Physiology assembly plus the same Core/Unity base. No World, Physics, Rendering, Inventory, AI, Fauna, Construction, Habitat, Graphics, or Gameplay runtime asmdef reference was found in the Physiology asmdefs.

SHINOBU_155 reset/fade kernels remain deterministic Burst jobs with synchronous compile and standard precision. NativeArray and unsafe pointer lanes are explicitly `[NoAlias]`; `ScheduleSimulation` chains dispatcher input dependency into `ResetPlayerPhysiologyJob`, then into `UpdateRespawnFadeJob`, registers the resulting active fence with `H8Memory`, and returns that `JobHandle` rather than forcing a hot main-thread `Complete()`. Static source only; Unity import/profiler/player proof remains pending behind the build discipline gate and known external bridge compile blockers.

## 2026-05-20 SHINOBU_223 Power Jacobi Telemetry Addendum

Power-grid Vault lanes `70850..70864` remain owner-local SHINOBU_223 numeric `BufferID` casts. The telemetry proof
route now includes deterministic Burst `RecordPowerTelemetryJob`, which reads `PowerNodeDTO` plus demand lanes,
finite-clamps scalar inputs, writes 64-byte `PowerTelemetryEntry` rows into the 300-frame ring `70861`, and advances the
64-byte `PowerGridCounter64` cursor `70862`. The recorder stores no managed references, raw Vault pointers, or
`VaultBufferHandle<T>` state.

Verification status: static source plus editor regression coverage only. `RecordPowerTelemetryJob_WritesGenerationLoadPotentialAndCursor`
asserts generation/load/potential/brownout/cursor semantics. Latest guarded CLI build removed the SHINOBU-visible
`VaultGenerationHandle<>` symptom and remains blocked by external missing-symbol dependencies outside the power-grid
domain.

## 2026-05-20 SHINOBU_201 Buoyancy SIMD Runtime Vault Recovery Addendum

Buoyancy displacement runtime keeps its SHINOBU-owned buoyancy/SIMD Vault lanes as
`VaultGenerationHandle<T>` descriptors and resolves method-local `NativeArray<T>` views through
`IDataVault.TryResolveHandle`. Loop 33 adds a runtime recovery gate after descriptor migration:
`FixedTick` now refreshes the DataVault dependency, waits while `IDataVault.IsAllocationLocked` is true, retries cold
boot after the lock clears, and reacquires stale or missing generation descriptors through the existing
`EnsureVaultDescriptor` route before dropping the solver frame.

Cold/manual mutators are allocation-lock fenced. Emergency mock buoyant-object seeding, editor SIMD benchmark
generation, material CSV hydration, SIMD tolerance CSV hydration, and DataVault service replacement no longer adopt
existing descriptors and then write through a Vault allocation-lock window. They wait for the lock to clear and leave
steady-state Burst job math, DTO layout, BufferIDs, force packet ABI, and shader/telemetry ABI unchanged.

Verification status: static source only. Owned-path forbidden pattern scan returned no legacy `VaultBufferHandle`,
obsolete `.Resolve`, private native allocation, random, `foreach`, `Pack=`, hot string formatting, or binary hardware
switch matches. Braces, preprocessor pairs, non-ASCII, and touched-path whitespace checks are clean. CPU was 100%, so
the build gate was not opened; Unity import, Burst Inspector, profiler, GCMonitor, and player proof remain pending.

2026-05-20 reacquire addendum: SHINOBU_201 descriptor repair now adopts existing Vault generation descriptors before
calling the create/grow path. `EnsureVaultDescriptor<T>` first validates the cached descriptor, then calls
`TryGetGenerationHandle<T>` plus `TryResolveHandle<T>` and proves `Length >= requiredLength`; only absent or undersized
lanes can reach `GetGenerationHandle<T>`, and that fallback remains blocked while `IDataVault.IsAllocationLocked` is
true. Runtime Burst math, DTO layout, BufferIDs, force packet ABI, and quality curves are unchanged.

## 2026-05-20 SHINOBU_224 Active Equipment Registry Boundary Addendum

Active equipment truth remains in Vault-backed DTO lanes with `ActiveEquipmentDTO` fixed at 32 bytes and the
integration counters/telemetry lanes fixed at 64 bytes. The equipment solver and adjacent durability bridge now cache
registry services during cold bootstrap or hot-swap notifications only. `ModularEquipmentEngine` caches DataVault,
Thermodynamics, PowerGrid, ToolDurability, Player, and Submarine contracts; `PlayerTool` caches ModularEquipment,
PowerGrid, Submarine, Player, PlayerInventory, Input, InteractionSignals, and ToolDurability contracts;
`ToolDurabilitySystem` caches DataVault, Save, and Player contracts.

Runtime boundary: SHINOBU_224 does not poll `GlobalRegistry.DataVault`, `GlobalRegistry.Save`, or
`GlobalRegistry.Player` from the equipment-adjacent durability tick path. Durability Vault handles are resolved through
the cached `IDataVault`; save registration uses the cached `ISaveService`; player tool ownership uses the cached
`IPlayerRuntimeContext` with Transform fallback only for slow/cold owner discovery. DataVault replacement forces the
durability job fence to retire, clears stale handles, and reacquires owner-local durability lanes through the new vault.

2026-05-20 durability descriptor extension: `ToolDurabilitySystem` no longer persists legacy
`VaultBufferHandle<T>` descriptors for `ToolDurabilityItemStates`, `ToolDurabilityPendingDecay`,
`ToolDurabilityWearMultipliers`, `ToolDurabilitySlotActive`, or `ToolDurabilityBreakdownFlags`. These five lanes now
persist only 16-byte `VaultGenerationHandle<T>` descriptors, resolve method-local `NativeArray<T>` views through cached
`IDataVault.TryResolveHandle`, reacquire through `GetGenerationHandle<T>` only when missing/stale/undersized, and
release descriptors through `IDataVault.ReleaseBuffer` on DataVault rebind or owner destroy.

## 2026-05-20 SHINOBU_204 ARM64 DTO Alignment Addendum

SHINOBU_204 removed runtime `StructLayout(...Pack=...)` debt under `Assets/_Project/Scripts` and continued owner-safe
Sequential-to-Explicit migration for Core ABI surfaces. `GlobalRegistryContracts.cs`, `GlobalTelemetryBus.Blackbox.cs`,
`MacroDatabaseContracts.cs`, `H8MacroDatabaseService.cs`, and `H8StaticDataContracts.cs` now report zero
`LayoutKind.Sequential` hits by static source scan.

Latest explicit ABI additions:
- Lockstep replay/state/hash rows: `LockstepPlayerKinematicState=96`, `LockstepReplayInputFrame=48`,
  `LockstepReplayBlockHeader=128`, `LockstepArrayHash=32`, `LockstepTelemetryEntry=64`,
  `LockstepMasterHashHistoryEntry=32`. Remaining Sequential rows in `LockstepStateValidator.cs` are Unity
  `NativeArray` job wrappers, not element DTOs.
- MacroDatabase contracts/cache rows: `MacroDatabaseConfig=64`, `MacroDatabasePayloadHandle=40`,
  `MacroDatabaseNativeCacheStats=24`, `MacroDatabaseStats=80`, `MacroDatabaseCompactionSnapshot=48`,
  `SectorHydratedSignal=32`, `MacroDatabaseTelemetryEntry=72`, `SectorCoord64=24`, `HydrationCandidate=48`,
  `MacroDatabaseDirtyPayloadSlot=64`, and `MacroDatabaseSectorCoordSlot=64`.
- H8StaticData file/lookup/static records: `H8StaticDataHeader=64`, `H8StaticDataLookupEntry=16`,
  `H8BabelDictionaryHeader=32`, `H8BabelDictionaryEntry=16`, `BabelIndexDTO=16`,
  `BabelLookupResultDTO=16`, `MockUIBuffer=16`, four static balance records at `48`, static-data telemetry at
  `64`, and dump header at `32`.
- SaveSystem persisted rows: `MerkleNodeDTO=32`, `SectorEntryDTO=32`, `StateDeltaRecordDTO=64`,
  `SaveMerkleWalAppendHeader=64`, `SaveMerkleTelemetryEntry=64`, `SaveMerkleEmergencyHeader64=64`,
  `SaveMasterHashV10Result=32`, `SaveFileHeaderV10=72`, `SaveVoxelDeltaRun5=8`, `SaveVoxelDeltaRun8=8`,
  `QuantizedAupSectorHalf3=24`, `SaveAupLocalOffset32=32`, `StrictSaveFileHeader64=64`,
  `SaveChunkHeader32=32`, and `SectorPayloadDTO=264`.
- SaveBinaryStorage rows: all formerly Sequential records in `SaveBinaryStorage.cs` are now explicit. The legacy
  `IndexedSaveFileHeaderV8` remains 52 bytes but stores the two legacy 64-bit hashes as four 32-bit lanes at offsets
  `36/40/44/48` to avoid unaligned `ulong` loads on ARM64.
- H8BinaryWorldPager queue/telemetry rows: `PageWriteCommand=32`, `PageReadCommand=24`, `PageReadResult=32`, and
  `PagerTelemetryEntry=64` are explicit. `H8BinaryWorldPager.cs` reports zero `LayoutKind.Sequential` hits by static
  scan.
- SaveData fixed rows: all `[BinaryBlittableSafe]` records in `SaveData.cs` are now explicit. Remaining Sequential
  records in that file are managed compatibility DTOs with strings, arrays, or bool fields and are not accepted as
  unmanaged binary payloads.

Verification status: static source only. `StructLayout(...Pack=...)` scan is clean under `Assets/_Project/Scripts`.
Targeted `git diff --check` passed with CRLF warnings only. Build proof is still blocked by the existing dependency wall
and the active no-rebuild gate.

## 2026-05-20 SHINOBU_222 Sump Pump CSR Drainage Descriptor Addendum

Sump pump drainage owns owner-local numeric `BufferID` casts `95820..95843` in `SumpPumpDrainageBufferIds`; they are
not central `H8Memory.BufferID` enum additions. The lanes cover pump nodes, flat pipe edges, node AUPs, room indices,
CSR offsets/destinations/conductance/flow, pressure front/back, power potential, pump remainder, per-pump mass-error
rows, tuning, 300-frame telemetry, telemetry cursor, counters, CSV profiles/scratch, frame summary, and GPU flow
upload rows, plus 64-byte per-room drain lock rows. The `70820..70841` candidate range was rejected after static source audit because graphics, atmosphere,
sonar, and wreckage owners already cast those values locally.

Runtime boundary: `SumpPumpPipeGridRuntime` stores only 16-byte `VaultGenerationHandle<T>` descriptors and resolves
method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle` during cold writes, solve scheduling, visual
sync, editor gizmos, and black-box dumping. It does not persist `VaultBufferHandle<T>`, `NativeArray<T>`,
`NativeSlice<T>`, or raw Vault pointers across frames. Solve scheduling locks SHINOBU_222 owner-local buffers before
resolving descriptors; optional Fluid Incursion front/back rows and Logistics pressure rows are consumed through
method-local generation handles, not direct `TryGetBuffer` external views. Owner-local drainage descriptors are released
through `IDataVault.ReleaseBuffer` on runtime teardown after the scheduled fence is complete. The final scheduled
telemetry-chain handle is registered with `H8Memory.RegisterActiveJob(SystemID.Construction, handle)` so shared memory
teardown and defrag diagnostics see the active owner fence.

Boot fail-close addendum: after cold owner-local handle acquisition, `SumpPumpPipeGridRuntime` validates every drainage
`VaultGenerationHandle<T>` through `IDataVault.TryResolveHandle` and checks the expected minimum row count before
initializing tuning or setting `_buffersReady`. Any partial acquisition releases the owner-local descriptors and resets
the runtime to an unavailable state instead of letting later solver scheduling discover default handles.

False-sharing boundary: active pump drain writes per-pump mass-error rows and pump DTO rates directly by index; frame
evacuation, active pump count, power draw, and conservative mass error are reduced once by
`DrainageTelemetryRecorderJob`. The previous parallel adjacent-`int` aggregate path is not used.

Conservation addendum: active pumps targeting the same Fluid Incursion room are serialized through
`DrainageRoomDrainLock64` rows on lane `95843`. Each row is explicit 64 bytes with `LockState` at offset 0 and padding
through offset 56. `EvacuateWaterVolumeJob` now computes one bounded drain amount from the sanitized minimum of
front/back water and applies the identical delta to both Fluid buffers; the previous independent front/back
`AtomicDrainVolume` path is removed.

Safety polish: CSR rebuild now bounds each flat-edge write by the capped source-node range (`slot <
NodeEdgeOffsets[source + 1]`) after global edge-capacity trimming, preventing one high-degree source from overwriting
another node's CSR row. Fluid room acquisition is bounded to 64 lock attempts and returns zero on pathological
contention instead of spinning forever. Missing, locked, empty, non-finite, out-of-range, or undersized Logistics Power
Vault rows fail closed to `0.0` pump power instead of synthetic full power; the Jacobi pressure job also uses `0.0`
fallback power for missing `PowerPotential` rows. Drain quantization clamps to `[0, MaxQuantizedDrainUnitsPerPump]`
before integer conversion to prevent corrupted positive or negative rate/remainder overflow.

Verification status: static source only. SHINOBU_222 legacy Vault-handle scan, direct `TryGetBuffer` scan, hot-path
forbidden-pattern scan, Burst attribute scan, explicit DTO layout scan, central-`ShinobuDrainage` enum scan, and direct
job `Execute`/`Complete` scan summaries are recorded as clean text only. `git diff --check` reports no whitespace errors and only pre-existing CRLF
normalization warnings in broader touched docs. Unity import, Burst compile, profiler/GCMonitor, and play mode proof
remain pending because total CPU remains above the build gate; latest sample was 100% and the gate forbids
`dotnet build` above 50%.

## 2026-05-20 SHINOBU_221 Base Atmosphere Logistics Vault Lane

SHINOBU_221 owns base-interior gas logistics for oxygen, carbon dioxide, nitrogen, toxins, and temperature. The lane replaces legacy global oxygen reads with a Vault-backed CSR gas graph and double-buffered Jacobi diffusion.

Reserved owner-local Vault IDs `71500..71522` are declared in `AtmosphereLogisticsBufferIds` as local numeric `BufferID` casts and are not central `H8Memory.BufferID` enum additions. `71514..71518` are 64-byte padded `AtmosphereDeltaLane64` rows to isolate atomic source/sink writes from false sharing.

Primary DTOs: `AtmosphereCellDTO` is exact 32 bytes with offsets `NodeHash=0`, `Oxygen01=4`, `CarbonDioxide01=8`, `Nitrogen01=12`, `Toxin01=16`, `Temperature=20`, `Flags=24`, `_pad0=28`. `AtmosphereTelemetryEntry` and `AtmosphereDeltaLane64` are exact 64-byte rows.

Runtime boundary: PreSimulation ingests typed `SignalBus` snapshots into Vault rows; Simulation schedules Burst jobs and returns the final handle to `SystemDispatcher`; PostSimulation patches telemetry and fault dumps; VisualSync publishes one shader scalar payload. No atmosphere-owned persistent `NativeArray`, `NativeList`, `NativeHashMap`, or raw Vault pointer is retained across frames.

Legacy bridge: `HabitatIntegrityManager` global oxygen statics are fallback storage only. Public reads route to the SHINOBU_221 runtime snapshot when available, and module contribution syncing removes old fallback contributions instead of maintaining a parallel global oxygen authority.

Polish addendum: `ReactorDamageSignal` is a Core Contracts payload at `Assets/_Project/Scripts/Core/Contracts/Signals/ReactorDamageSignal.cs`, so the reactor publisher and atmosphere consumer meet at the signal ABI instead of an Atmosphere-owned contract. Simulation locks all scheduled solver lanes, including read-only nodes/source/tuning rows, before returning the job handle.

Static hardening addendum: CSR construction uses shifted degree counts with cumulative `EdgeOffsets[1..nodeCount]`, preserving `EdgeOffsets[i]..EdgeOffsets[i+1]` as node `i`'s adjacency range. Editor/gizmo read APIs return false while `_simulationScheduled` is true so debug presentation cannot read the newly swapped front buffer before the scheduled solver writes complete.

Lock/CSR safety addendum: active front/back cell `BufferID`s are frozen at simulation lock acquisition and reused during unlock, so odd Jacobi iteration counts cannot leak the originally locked Vault rows after front/back handle swaps. Diffusion clamps each CSR read span into `[0, EdgeCount]` before destination/conductance reads.

Jacobi addendum: diffusion uses the XML route formula with an explicit self term and guarded denominator: `(neighborGasSum + currentGas) / max(sumConductance + 1, 0.0001)`, then continuous alpha blending and source/sink deltas. This keeps the solver parallel Jacobi, not in-place Gauss-Seidel.

Conservation addendum: SHINOBU_221 quantizes gas first, then distributes residual O2/CO2/N2/toxin units across back-buffer cells with bounded capacity checks instead of applying all rounding error to `Back[0]`. Delta lanes consumed by the correction job are marked read-only and remain 64-byte padded rows.

Cold tuning boundary: `Docs/Atmosphere/gas_diffusion_profiles.csv` is parsed through Vault scratch `71521` into profile rows `71522`. First-column tokens accept either numeric IDs or lowercase FNV-1a hashes of module type names; no managed CSV row strings are part of the runtime gas truth.

Route card: `Docs/ARCHITECTURE/BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221.md`. Dump path: `Docs/AgentLogs/Dump_SHINOBU_221.bin`.

Verification status: static source/docs only. A legal single-thread `dotnet build Hecton8.Core.csproj` attempt failed with unrelated existing dependency errors outside SHINOBU_221-owned files. Unity import, Burst compile, Play Mode, profiler/GCMonitor, and player-build proof remain pending behind that external compile wall.

## 2026-05-20 SHINOBU_209 Offline Wreckage Geometry Baker Binary Boundary

SHINOBU_209 owns Editor-only offline wreckage deformation output for man-made structural meshes. It does not own runtime damage truth, physics simulation, rollback state, or a DataVault lane.

- Damage-state map payloads are generated as exact 32-byte `MeshDamageStateMappingDTO` records: `PristineMeshHash`, `StressedMeshHash`, `RupturedMeshHash`, `CollapsedMeshHash`, and 16 bytes of explicit zero padding. The writer clears the stack span before emitting little-endian values, writes through unique same-volume `.tmp.<processId>.<ordinal>` paths, publishes existing artifacts with `File.Replace` before Unity asset import, and retries once after re-observing final-path state if another Editor tool changes final existence between the first observation and commit.
- Generated visual `.mesh` assets use an explicit interleaved 64-byte vertex DTO and immutable Stressed/Ruptured/Collapsed states. Their output paths include a sanitized source name plus source-path hash and are refreshed in place with `EditorUtility.CopySerialized` on rebake, preserving existing `.meta` GUIDs. Runtime systems are expected to synchronize only the integer damage-state index and consume mesh hashes/references through their own owner lanes.
- Collision output is a Dear Lie proxy: an offline 8-point support hull mesh under the 256-point budget, not torn visual topology as runtime collision truth.
- Thin-axis collision proxy rule: valid measured support bounds are preserved. Any collapsed axis expands to a 0.01 m half-extent and sets `WarningHullBoundsExpanded` in the 64-byte counter row/report/black-box warning flags; only invalid or non-finite support bounds fall back to a unit cube.
- Black-box dump payload `Docs/AgentLogs/Dump_SHINOBU_209.bin` is fixed binary: zero-cleared 32-byte little-endian header plus retained `OfflineWreckageTelemetryEntry` rows at 64 bytes each. The writer copies raw DTO rows through `UnsafeUtility.CopyStructureToPtr` and publishes through unique same-volume temp files plus `File.Replace` for existing dumps.
- Source mesh extraction preserves all triangle submeshes by emitting explicit 16-byte `OfflineWreckageSubMeshIndexRangeDTO` tiles. Each full tile covers 384 indices, carries source start, destination start, count, and `baseVertex`, clamps descriptor bounds to the source index buffer, and applies `baseVertex` through a 64-bit temporary with int clamping before collapsing to one immutable output triangle stream for runtime state-swap consumption.
- Present reports `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_209.json` are static/editor artifacts only. `Docs/Reports/WRECKAGE_BAKE_REPORT.json` is an expected Forge batch-bake output path and is absent in this checkout until an actual selected-folder bake generates it.
- Scanner canonical-report preservation is bounded: before overwriting `PHYSICS_OPTIMIZATION_REPORT.json`, SHINOBU_209 writes the previous JSON to `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_PREVIOUS_SHINOBU_209.json` and records only UTF-8 byte count, raw UTF-8 byte-stream hash, and agent in the new canonical/sidecar reports. It no longer embeds recursive full-report blobs. Scanner JSON string emission escapes control characters, previous-agent extraction uses backslash-parity quote termination, and non-string agent fields fail closed to `UNKNOWN`.
- CI/editor mock benchmark output `Docs/Reports/WRECKAGE_MOCK_BENCHMARK_SHINOBU_209.json` is an expected Editor-only `OfflineWreckageMockBenchmark` output path and is absent in this checkout until that menu/entrypoint is executed. The benchmark route is documented to exercise dense-grid mock vertices, generated six-face boundary surface indices, shear, radial blast, tear, normal, color, and hull jobs without source art assets or scene GameObjects.
- Unity import identity is stabilized by explicit `.meta` files for every owned `.cs` and `.asmdef` in `Assets/_Project/Scripts/World/OfflineWreckageBaker`; domain duplicate-GUID scan returned no duplicates. Baked output no longer uses `GenerateUniqueAssetPath`, so repeat bakes do not mint orphaned numbered mesh/map assets.
- Editor preview lifetime is bounded: the transient preview Mesh uses `HideFlags.HideAndDontSave` and `OfflineWreckagePreviewLifecycle` disposes both preview Mesh and black-box telemetry ring before assembly reload/editor quit.
- Native allocation tracking: the black-box telemetry ring registers through `Hecton8.Core.Contracts.NativeMemoryTrackingBridge` as owner `OfflineWreckageBlackBox`, label `s_ring`, lifetime `Session`, and unregisters before disposing. This avoids a direct root Core dependency from the offline baker while preserving sentinel visibility when the bridge is installed.

Verification status: static source and docs only. Unity import, Burst compile, actual Forge bake, mesh asset GUID proof, Console, profiler/GCMonitor, and player-build proof remain pending behind the CPU/build guard.

## 2026-05-20 SHINOBU_217 Construction Socket Preview And CSR Vault Lanes

SHINOBU_217 adds owner-local construction socket preview/CSR buffers without mutating the central `BufferID` enum:

- `70370` `ConstructionGhostPreview`: `GhostPreviewDTO[1]`, explicit 96-byte row containing active ghost AUP, rotation, bounds scale, snap radius, module hash, socket range, Dear Lie dampening, `GlobalQualityWeight`, flags, bounds center, and frame.
- `70371` `ConstructionSocketCsrRanges`: `int2[70]`, six target direction ranges plus 64 ghost-specific inverse-direction ranges.
- `70372` `ConstructionSocketCsrTargetIndices`: `int[3000]`, direction-bucketed target socket row indices for `EvaluateSocketSnappingJob`.

Runtime boundary: `PlayerBuilder` writes the preview row during the active snap pass after resolving cached `IDataVault` views. A valid snap immediately overwrites the preview AUP with the snapped root AUP and sets `ValidSnap | DearLieActive`; the shader presentation hides the instant move through dampening. The parallel `ConstructionPreviewSignal` stays 128 bytes and uses aligned padding offsets `96`, `100`, and `104` for `DearLieDampen`, `GlobalQualityWeight`, and `DearLieWiggleSpeed`, allowing the active preview renderer to push the same fake into `Hecton8/Fabrication/BlueprintWireInstanced` without a new signal lane. The renderer resets its Dear Lie envelope when preview count reaches zero, so stale result/module hashes cannot suppress the next pulse. Target socket hydration builds the six direction CSR buckets; each ghost socket writes a row at `6 + ghostIndex` pointing to the inverse target-direction bucket. The solver treats missing CSR range/index lanes as `CapacityExceeded`, not as permission to scan `0..TargetCount` directly. These buffers are presentation/read-model and solver-index state, not a second module-placement authority. Authoritative topology remains in `ConstructionSocketStates`, `ConstructionSocketAup`, `ConstructionSocketModules`, and `ConstructionSocketCounters`.

Verification status: static source/docs only. Unity import, Burst compile, Play Mode, profiler/GCMonitor, and player-build proof remain pending behind the CPU/build guard.

## Integration Backlog

| Priority | Task | Owner domain | Reason |
|---:|---|---|---|
| 0 | Keep `Data/Balance/Baked/Babel_Dictionary.h8bin` rebaked through `H8DataBaker`. | Core data / baker owner | SHINOBU_50 repaired the 16-byte alignment failure; future drift must fail hygiene again. |
| 1 | Decide one static-data source of truth: StreamingAssets DataMonolith or small `Data/Balance/Baked` stores. | Core data / bootstrap | Parallel static-data contracts will produce false reads and stale payloads. |
| 2 | Verify `BiolumPulseSyncRuntime` host in Unity scene/profiler. | VFX | Static source shows a runtime host fallback path through an atomic ownership claim and SHINOBU-isolated asmdefs; latest narrow Assembly-CSharp build predates the asmdef/H-PHI patch, so Unity import, fresh build, Frame Debugger, and Profiler proof are still missing. |
| 3 | Verify the new H8LR B-Tree reader for `Encyclopedia.h8bin` in Unity import/Play Mode/profiler. | Narrative/PDA/Core data | `PdaH8lrLoreStore` now consumes the generated H8LR+BTree blob in static source; runtime proof and GC/profiler evidence are still absent. |
| 4 | Promote PDA `H8PT` reader if PDA technical logs are intended for runtime. | PDA/UI/Narrative | Binary has good lookup contract but no runtime reader found. |
| 5 | Build a visual scalability selector for refraction, water-extinction variants, VFX budgets, VR comfort, tide, and Dalton variants. | Rendering/UX/Environment | Tier binaries are useless without hysteresis and platform gates. |
| 6 | Scope `Tools/VerifyBinaryHygiene.py` to product payloads or explicitly exempt Bakery. | Build/QA | Current gate mixes product payload drift with vendor editor fixtures. |

## Regression Model

CPU: documentation-only pass, no runtime CPU change. Future payload wiring must stay cold-path or
lazy-read and must not add per-frame file probes.

GC: documentation-only pass, no managed allocation change. Future readers must use caller-owned
buffers, `NativeArray`, `GlobalDataVault`, or fixed cold allocations only.

Memory: no payloads were deleted or loaded by this pass. Future tier selectors must account for MX350
VRAM and avoid loading low/base/ultra variants simultaneously unless explicitly budgeted.

Cadence: tier changes require hysteresis. Immediate low/high/ultra flipping is rejected.

Correctness: stale generated binary claims are subordinated to fresh filesystem and verifier output.
The stale "46 aligned payloads" statement in older docs is not current truth.

## Hot Path Impact

This ledger changes docs only. Runtime hot-path and GC impact were not measured in this pass; no
per-frame or allocation saving is claimed. No C# source was modified.

## Failure Modes

- Reintroducing a misaligned `Babel_Dictionary.h8bin` can break strict binary hygiene gates and any
  reader that assumes 16-byte sections.
- Keeping H8LR lore without a runtime reader produces false content-readiness.
- Keeping multiple acoustic, refraction, water, tide, and toxicity tables without selectors inflates
  package/import surface and can hide stale data.
- Broad verifier scope can fail product gates because of third-party editor fixtures unrelated to
  HECTON payload ownership.

## 2026-05-20 SHINOBU_202 Vault Generation Handle Safety Addendum

Core memory now exposes a pointer-free `VaultGenerationHandle<T>` descriptor for persistent Vault state. The
descriptor is 16 bytes and contains only `BufferID`, `SystemID`, `Generation`, and `Flags`; managers must resolve it
into a local `NativeArray<T>` view through `IDataVault.TryResolveHandle` inside the execution phase that uses the data.

Migrated runtime routes:

- `H8StaticDataArena`: Data Monolith payload `71103`, telemetry ring `71104`, and cursor `71105` are generation
  descriptors. The previous static arena `NativeArray<byte>` cache was removed.
- `StaticDataStore`: Static-data and B-Tree telemetry rings/cursors/accumulator are generation descriptors. Dump
  writers derive read-only pointers only after a successful local resolve.
- `BabelDictionaryStore`: Static-data/B-Tree telemetry and `BabelErrorUtf8` are generation descriptors. The padded
  Babel dictionary fallback is acquired through `GetBuffer<byte>` as an explicit external view so live defrag refuses
  relocation while SHINOBU_207 pointer jobs still consume `_basePointer`.
- `BurstTokenBucketJobAdmissionService`: Core scheduling buffers are generation descriptors and are released through
  `GlobalDataVault.ReleaseBuffer` on service teardown.
- `VaultMemoryContracts`, `VaultLegacyBinaryArchaeology`, and `VaultProbeUtility`: Core memory telemetry/configuration
  diagnostics use `VaultGenerationHandle<T>` descriptors and no longer export legacy pointer-bearing handles.
- `HardwareThermalService`: thermal severity byte and hardware throttling blackbox ring are generation descriptors and
  are released through `GlobalDataVault.ReleaseBuffer` on teardown or DataVault hot-swap.
- `GlobalSignals.SignalBus<T>`: per-lane frame snapshots no longer cache a persistent `NativeArray<T>` Vault alias.
  Snapshot buffers are generation descriptors, resolved as method-local views during flush/read/filter/sort, refreshed
  after generation churn, and released on lane disposal.
- `AlignmentTelemetryContracts.Arm64AlignmentTelemetry`: ARM64 alignment fault ring uses a generation descriptor and
  method-local views; stale legacy ring handles are no longer exported by this Core memory diagnostic route.
- `ModuloSimulationBucketer`: simulation bucket front/work tables, cost/load EWMA tables, rebalance scratch/result,
  frame state, and 300-frame blackbox buffers are generation descriptors. The bucketer resolves only method-local
  `NativeArray<T>` views and releases all descriptors through `GlobalDataVault.ReleaseBuffer` on dispose/re-init.
- `LockstepStateValidator`: deterministic hash source lookup no longer validates `VaultBufferHandle<T>.ptr`. It
  requests a generation descriptor, resolves a method-local `NativeArray<T>` view, and performs native alignment
  validation on that transient view pointer before hashing.
- `H8InputMappingFacade`: bridge input binding hydration no longer writes through `ResolvePointer`. The facade resolves
  `BridgeInputFacadeBindings` as a method-local `NativeArray<H8InputFacadeBindingEntry>` through a generation descriptor
  before clearing and writing entries.
- `H8PrefabRegistryRuntimeBinder`: prefab mapping and lore link hydration no longer write through `ResolvePointer`. The
  binder resolves `BridgePrefabMapping` and `BridgePrefabLoreLinks` as method-local `NativeArray<T>` views through
  generation descriptors before clearing and writing entries.
- `H8BridgeFacadeRuntime`: design facade values, macro header persistence, and the facade telemetry ring no longer use
  local `VaultBufferHandle<T>` descriptors or `ResolvePointer`. The runtime resolves `BridgeDesignFacadeValues`,
  `BridgeFacadeMacroHeader`, and `BridgeDesignFacadeTelemetryRing` as method-local `NativeArray<T>` views through
  generation descriptors before clear/write/hash/dump work.
- `ContentRuntimeServices`: content bundle ref state/count, content telemetry ring/cursor, and pending-load state/count
  no longer persist legacy `VaultBufferHandle<T>` descriptors. Content authority resolves those buffers as method-local
  `NativeArray<T>` views, derives transient pointers only inside the current method, and releases descriptors through
  `GlobalDataVault.ReleaseBuffer` on teardown or DataVault hot-swap.
- `HomeostasisBrain`: base hardware metrics, frame-time samples, and the 300-frame homeostasis blackbox no longer
  persist legacy `VaultBufferHandle<T>` descriptors. The global pressure authority resolves those buffers as
  method-local `NativeArray<T>` views and releases descriptors through `GlobalDataVault.ReleaseBuffer` on shutdown or
  DataVault hot-swap. `HomeostasisBrain.ScalabilityDictator.cs` now follows the same rule for scalability dictator
  lanes `70480..70485` and `70487`: persistent state is `VaultGenerationHandle<T>` only, editor/test facades use
  local `NativeArray<T>` views, the pending mock terrain sampler job is completed before release, and hot-swap
  releases descriptors against the previous Vault before `_dataVault` changes.
- `AupOriginShiftCoordinator`: origin-shift lanes `73030..73037` now persist only `VaultGenerationHandle<T>`
  descriptors. Rebase, mock camera, telemetry, CSV scratch, and the 64-byte padded counter resolve method-local
  `NativeArray<T>` views through `IDataVault.TryResolveHandle`; cached Vault replacement releases descriptors against
  the previous Vault before local state is reset. Rebase jobs still receive raw pointers only after descriptor
  validation and only for the scheduled phase.
- `GlobalTelemetryBus.Blackbox`: crash blackbox lanes `ShinobuCrashBlackboxBytes`, `ShinobuCrashMmfScratch`,
  `ShinobuCrashDumpHeader`, `ShinobuCrashTelemetryEvents`, `ShinobuCrashSourceSlots`, `ShinobuCrashLoggingMasks`,
  `ShinobuCrashAtomicState`, `ShinobuCrashWatchdogCounters`, `ShinobuCrashWatchdogSamples`,
  `ShinobuCrashWatchdogStaleProbes`, and `ShinobuCrashWatchdogActive` now persist only `VaultGenerationHandle<T>`
  descriptors. The previous static Vault-backed `NativeArray<T>` aliases were removed; event, source, frame commit,
  dump, MMF, watchdog, and editor routes resolve method-local views through `IDataVault.TryResolveHandle`. The
  blackbox still lifetime-locks those buffers while active because `TryGetBlackboxRingBuffer` intentionally exports a
  raw diagnostic ring pointer, but the manager no longer stores a stale native view and releases descriptors on failed
  bind or teardown.
- `MemorySentinelRuntime`: sentinel-owned lanes `70873..70882` now persist only `VaultGenerationHandle<T>`
  descriptors for validation states, target rows, results, rollback bytes, mock inventory, mod quarantine, telemetry,
  runtime state, AUP snapshot, and CSV scratch. External watched buffers are discovered through
  `TryGetGenerationHandle` plus `TryResolveHandle` before deriving locked phase-local target pointers. Result
  consumption and rollback correction now run before target-buffer unlock, closing the relocation window between
  validation and correction.
- `InputCurveHapticsTunerWindow`: the editor-only input curve/haptics facade now requests
  `ShinobuInputProfile` and `ShinobuInputCurrentDto` as `VaultGenerationHandle<T>` descriptors and resolves local
  `NativeArray<T>` views through `IDataVault.TryResolveHandle` before row read/write. The facade no longer teaches
  `GetBufferHandle`, `GetElementAsRef`, or `GetElementAsReadOnlyRef` in the human-control surface.
- `InputDispatcher`: deterministic input and haptics lanes now persist only `VaultGenerationHandle<T>` descriptors for
  `ShinobuInputCurrentDto`, `ShinobuInputJournalRing`, `ShinobuInputStateBridgeRing`,
  `ShinobuInputButtonMaskWindow`, `ShinobuInputBlockMask`, `ShinobuInputProfile`,
  `ShinobuInputTelemetryRing`, `ShinobuInputReplaySnapshot`, `ShinobuInputHapticCommands`,
  `ShinobuInputXRInputStates`, `ShinobuInputXRLookAtRayCommands`, and `ShinobuInputCsvScratch`.
  Runtime, haptic, XR, replay, telemetry, and CSV paths resolve method-local `NativeArray<T>` views through
  `IDataVault.TryResolveHandle`. The replay writer no longer dereferences `_inputReplaySnapshotHandle.ptr`; the
  phase-local staging path copies the Vault snapshot into the MMF payload before the worker thread flushes.
- `SystemDispatcher`: H8 time, dispatcher blackbox, master job handles, dependency scratch, master pipeline telemetry,
  presentation suppression, domain fence handles, fence telemetry, and dispatcher raycast command/hit buffers now
  persist only `VaultGenerationHandle<T>` descriptors. The dispatcher resolves method-local `NativeArray<T>` views
  through `IDataVault.TryResolveHandle` during enqueue, schedule, telemetry, blackbox, and fence phases. Shutdown and
  DataVault hot-swap release old descriptors through `IDataVault.ReleaseBuffer`; scheduled raycast buffers keep their
  existing owner-tagged Vault locks only while the scheduled `RaycastCommand` job owns the phase-local views.
- `AsynchronousTelemetryExporter`: analytics event ring, staging, routine/critical ingress, ingress cursor, counters,
  telemetry, tuning, CSV scratch, compressed scratch, heatmap debug, handoff A/B, worker accumulator, raw batch
  scratch, and dump snapshot buffers now persist only `VaultGenerationHandle<T>` descriptors. Main-thread event ingress
  writes resolve local `NativeArray<T>` views through `IDataVault.TryResolveHandle`; the background worker keeps the
  existing owner-tagged Vault locks while alive, but no longer builds worker views from cached `handle.ptr` metadata.
  Descriptors are released only after worker shutdown succeeds and the worker locks are removed.

Residual boundary: untouched owners still contain legacy `VaultBufferHandle<T>` debt. The legacy bridge remains
obsolete but non-breaking; it resolves through the generation path and does not trust cached `ptr` during `.Resolve`
or `ResolvePointer`. New manager code must not persist `VaultBufferHandle<T>`, `NativeArray<T>`, `NativeSlice<T>`, or
raw Vault pointers across frames.

- `StructuralIntegrityCalculatorRuntime` (SHINOBU_218): structural buffers `70488..70497` now persist only
  `VaultGenerationHandle<T>` descriptors. Runtime phases resolve method-local `NativeArray<T>` views through
  `IDataVault.TryResolveHandle`, validate required lengths at boot, and release descriptors through
  `IDataVault.ReleaseBuffer` on failed boot or owner shutdown. The route no longer stores legacy
  pointer-bearing `VaultBufferHandle<T>` fields. Player boot keeps deterministic default material strengths; structural
  material CSV file reads, file polling, parser helpers, and CSV material apply jobs are editor-only.
- `HullIntegrityRuntime` (SHINOBU_218 Habitat/Deformation cleanup): hull dent/deformation, breach jet, material
  strength, CSV scratch, telemetry, and pressure mirror lanes now persist only `VaultGenerationHandle<T>` descriptors.
  Runtime, editor, cold CSV, black-box, GPU upload, and read-model paths resolve method-local `NativeArray<T>` views
  through `IDataVault.TryResolveHandle`. Failed boot and owner shutdown release descriptors through
  `IDataVault.ReleaseBuffer`; scheduled and cold clear jobs are registered through
  `H8Memory.RegisterActiveJob(SystemID.HullIntegrity, handle)`. Scoped static scan across
  `Assets/_Project/Scripts/Habitat/Deformation` is clean for `VaultBufferHandle`, `GetBufferHandle`,
  `.Resolve(_dataVault)`, `ResolvePointer`, `GetElementAsRef`, and `.ptr`.
  2026-05-20 SHINOBU_218 follow-up: player builds no longer implement/register/unregister the structural or hull runtime
  on the cold dispatcher lane, CSV tuning hot reload and CSV parser/file polling are editor-only, and every Burst job in `HullIntegrityTypes.cs` now
  uses deterministic float mode because the lane mutates rollback-adjacent SIP, breach, deformation, pressure, indirect
  breach-jet, and telemetry state. `ValidateLayouts()` keeps `UnsafeUtility.SizeOf<T>()` checks in every build while
  reflection-backed DTO offset checks compile only under `UNITY_EDITOR`.
- `HabitatDamageMeshStateResolver` ownership correction: SHINOBU_210 owns staged baked damage mesh selection and keeps
  Stressed/Ruptured/Collapsed hashes reachable. SHINOBU_218 structural runtime does not call the pressure-to-mesh
  resolver; its pre-collapse deformation route remains `IntegrityStateDTO.BucklingScalar` plus shader-buffer upload.
- `ShinobuRespawnReconciliationRuntime` (SHINOBU_155): respawn buffers `71604..71613` plus shared physiology,
  metabolism, and player-kinematic Vault lanes now persist only `VaultGenerationHandle<T>` descriptors. Runtime,
  editor, CSV, and black-box paths resolve method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`.
  SHINOBU creates/grows only owner-local respawn descriptors `71604..71613`; shared Physiology, Decompression, Tissue,
  PhysiologyScalar, Metabolism, and PlayerKinematic descriptors are read only with `IDataVault.TryGetGenerationHandle`
  and must already exist. SHINOBU releases only owner-local respawn descriptors `71604..71613` on disable, DataVault
  hot-swap, or failed cold acquisition, then clears all descriptors. Shared Physiology, Metabolism, and PlayerKinematic
  lanes are never released or synthesized by SHINOBU_155. Existing owner-local descriptor recovery now runs before the
  allocation-lock check and proves each row count through `IDataVault.TryResolveHandle`; locked Vault state can recover
  already-created SHINOBU buffers but cannot create or grow missing/undersized ones. Cached descriptor metadata is not
  accepted as proof: `EnsureVaultState` resolves all sixteen cached descriptors and verifies required row counts before
  cold early return; stale/non-resolvable descriptors are cleared and reacquired through the existing-descriptor-first
  path. Fresh acquisition of shared Physiology, Decompression, Tissue, PhysiologyScalar, Metabolism, and PlayerKinematic
  descriptors is also row-proven: `TryGetExistingVaultDescriptor<T>` requires `TryGetGenerationHandle`, `TryResolveHandle`,
  `IsCreated`, and `Length >= requiredLength` before SHINOBU can accept the lane. Hot dispatcher gates do not allocate or
  reacquire handles; they reject active compaction fences and per-buffer generation drift through `TryGetBufferGeneration`,
  while row-zero reads and unsafe job pointer extraction require explicit `HasRequiredLength(...)` checks at the access seam.

## 2026-05-20 SHINOBU_219 Visual Pressure Aging Contract/Descriptor Addendum

Visual pressure aging owns render-only Vault buffers `71240..71246` for `VisualAgingParamsDTO`, runtime counters,
300-frame telemetry, tuning, CSV scratch bytes, and mock temperature. The SHINOBU_219 runtime stores only
`VaultGenerationHandle<T>` descriptors for these lanes and resolves method-local `NativeArray<T>` views during
dispatcher phases; no `VaultBufferHandle<T>`, persistent `NativeArray<T>`, `NativeSlice<T>`, or raw Vault pointer is
kept by the visual manager.

Compile-wall boundary: `Hecton8.Graphics.Materials.asmdef` references `Hecton8.Habitat.Deformation.Contracts` for
structural read DTOs and no longer references `Hecton8.Habitat.Deformation` Runtime. The shared structural Vault ABI
types `IntegrityStateDTO`, `StructuralTuningDTO`, and `StructuralIntegrityConstants` now live in the Contracts
assembly under the existing `Hecton8.Habitat.Deformation` namespace, preserving `GlobalDataVault` type-hash identity
for the structural owner and the visual reader.

Runtime boundary: SHINOBU_219 reads structural states, structural AUPs, optional structural tuning, and optional
thermodynamic temperature mirror through phase-local generation descriptors plus explicit Vault locks. If any input is
absent or locked, it falls back to deterministic mock visual-aging data. Visual aging remains excluded from rollback
and save/Merkle state.

Verification status: static source only. Direct Graphics-to-Habitat-Runtime asmdef scan summary is recorded as clean text only, SHINOBU_219 legacy
Vault-handle scan and diff summaries are recorded as local text only; artifact tuple required before proof reuse normalization warnings. Unity import, Burst
compile, Frame Debugger, profiler/GCMonitor, and player-build proof remain pending behind the CPU build gate.

2026-05-20 lock-fence addendum: SHINOBU_219 cold/editor Vault paths now lock their rows before method-local resolves.
Editor tuning read locks tuning/runtime; default hydration locks tuning/mock-temperature/runtime; pending editor tuning
locks tuning; CSV hot reload locks CSV scratch/tuning; VisualSync locks runtime while mutating upload counters and fault
flags. GPU parameter upload, shader ABI, BufferIDs, and the structural Contracts route are unchanged.

2026-05-20 acquisition addendum: SHINOBU_219 normal dispatcher phases now resolve cached generation descriptors before
acquisition. `GetGenerationHandle<T>` is confined to `TryResolveOrAcquire<T>` fallback for cold missing, stale, or
undersized lanes; current descriptors resolve through `TryResolveHandle` after a generation check. This keeps owned
lanes `71240..71246` Vault-backed without repeating `TryEnsureVaultBuffer` acquisition/sanitize work in every phase.
Descriptor validation also requires `SystemID.GraphicsMaterials`, so a wrong-owner BufferID collision fails closed.

2026-05-20 shader quality addendum: SHINOBU_219 aging functions in `Hecton8_UberNoir.hlsl` no longer use local
`_MATH_LOD_LOW` forks. Rust growth and glass micro-fracture detail are driven by continuous quality weights with cheap
zero-detail masks; non-finite `_H8GlobalQualityWeight` falls back to `0.0`. No new shader keyword or variant was added.

2026-05-20 payload-quality addendum: SHINOBU_219 aging now resolves shader quality through
`H8UberNoirVisualAgingQualityWeight`, blending the broader UberNoir quality toward `_GlobalBaseAgingRuntime.z` and
`VisualAgingParamsDTO.StressAndMicroFractures.w` using the finite payload availability curve. Loaded visual-aging
`float4` lanes are sanitized by `H8UberNoirFiniteSaturate4`; non-finite pressure falls back to `0.0`. Static shader
scan only; Unity shader import, Frame Debugger, profiler, and GCMonitor proof remain pending behind the CPU build gate.

2026-05-20 first-payload fence addendum: SHINOBU_219 now fails closed before the first generated visual-aging payload.
`VisualSyncTick` advertises `_GlobalBaseAgingRuntime.x/y` as `0/0` until `PostSimulationTick` marks
`_hasGeneratedPayload` after a scheduled simulation pass. Default hydration locks `VisualPressureAgingParams`, clears
row zero, resets upload counters, and Vault descriptor release invalidates payload readiness. This prevents a first
frame from binding one `NativeArrayOptions.UninitializedMemory` row as a valid shader payload. Static source only;
build/import/profiler proof remains pending behind active CPU/compiler gates.

2026-05-20 hot-registry fence addendum: SHINOBU_219 dispatcher phases no longer repair a missing cached Vault reference
by querying `GlobalRegistry.DataVault`. `ResolveVault` now defaults to cached-only and only cold/editor bridge calls pass
`allowRegistryLookup=true`. PreSimulation, Simulation scheduling, VisualSync, and pending tuning application fail closed
if `_vault` is absent, preserving the boot-cached dependency law. Static source only; build/import/profiler proof remains
pending behind CPU gate.

2026-05-20 gizmo-readiness addendum: SHINOBU_219 editor gizmo reads now use the same payload-readiness proof as the GPU
upload path. `TryAcquireAgingBufferRead` refuses to expose `VisualPressureAgingParams` until `_hasGeneratedPayload` is
true and clamps the exposed active count to the resolved `NativeArray<VisualAgingParamsDTO>` length. This prevents
designer preview rings from reading `NativeArrayOptions.UninitializedMemory` rows after cold boot or Vault rebind. Static
source only; Unity import, Scene View gizmo capture, profiler, and GCMonitor proof remain pending behind the CPU build gate.

2026-05-20 construction-crack-decal removal addendum: SHINOBU_219 removed the dead
`BaseDegradationSystem.GlobalCrackDecalMatrices` / `GlobalCrackDecalAtlasIndices` compatibility surface and its backing
lists. No consumers existed; visual pressure aging remains owned by the `VisualPressureAgingParams` Vault buffer and
UberNoir shader path. SHINOBU_149 impact/fluid decal runtime remains out of this route and was not changed. Static source
only; Unity import and runtime render proof remain pending behind the CPU build gate.

2026-05-20 structural-profile decal residue addendum: `StructuralIntegrityProfile` no longer stores or exposes rupture
decal atlas indices. The profile remains structural authoring data only; visible pressure aging class/strength is derived
by SHINOBU_219's Vault-backed `VisualAgingParamsDTO` rows and UberNoir procedural shader logic. Static source only; Unity
import proof remains pending behind the CPU build gate.

## 2026-05-20 SHINOBU_153 Procedural Geology Vault Descriptor Addendum

Procedural geology runtime `ProceduralOreSpawner` no longer persists legacy `VaultBufferHandle<T>` descriptors. Its
21 Vault lanes (`71530..71550`) are stored as 16-byte `VaultGenerationHandle<T>` descriptors, resolved only as
method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`. Resolve/acquire helpers reacquire missing,
stale, or undersized descriptors through `GetGenerationHandle` before returning a phase-local view or writer lock.
CSV scratch writes and generation-job buffer fences acquire writer ownership through
`TryAcquireWriteLock`/`ReleaseWriteLock` on those same descriptors.

Loop 20 rebind extension: DataVault service replacement is observed through `IGlobalRegistryHotSwapListener` and
`IGlobalRegistryHotSwapRefListener`, queued as a cached pending Vault pointer, and consumed by `EnsureNativeState`
without polling `GlobalRegistry.DataVault` in tick paths. Rebind is deferred until active geology generation jobs retire
through `DispatcherJobFence`; stale output is discarded before descriptors are released or reacquired.

Loop 22 editor inspection extension: the `ProceduralOreSpawner` editor gizmo no longer calls `IDataVault.TryGetBuffer`
directly. It resolves `ResourceNodes` through the same `VaultGenerationHandle<T>` descriptor route as runtime phases and
keeps the resulting `NativeArray<ResourceNodeDTO>` local to the gizmo draw call.

Loop 23 depletion/editor extension: `TryMarkOreDepleted()` now refuses depletion writes while a generation job is
scheduled and applies pending descriptor rebind state through `EnsureNativeState()` before resolving mutation views.
The UI Toolkit geology tuner no longer reads or writes `Tuning`/`TelemetryRing` through direct `GetBuffer` or
`TryGetBuffer`; it uses method-local `VaultGenerationHandle<T>` descriptors and `TryResolveHandle`.

Loop 24 terrain adapter extension: `ProceduralOreSpawner` no longer carries
`MapMagicBridge.QuantizedHeightmapPayload` through its spawn scheduling boundary. `RefreshTerrainPayload()` copies the
concrete MapMagic payload into the SHINOBU-owned phase-local `GeologyHeightPayloadView`, and `ScheduleSpawnJob()` consumes
only that view. Terrain/MapMagic service pointers are cached on enable and maintained through
`TerrainProviderRuntime` / `MapMagicRuntime` hot-swap events. When no quantized height payload is available, the
`MockTerrainSdf` lane is seeded from cached `ITerrainProvider.TryGetHeight()` converted to AUP Y instead of player
altitude.

Runtime boundary: ore truth remains `71533` (`DepletionMasks`) plus the deterministic sector/slot seed. Matrix lanes
and candidate slots are presentation/read-model materialization only; no stored per-vein coordinate corpus was
introduced by the descriptor migration.

## 2026-05-20 SHINOBU_153 Candidate Slot Sentinel Addendum

Procedural geology buffer `71543` (`CandidateSlots`) now uses `-1` as the only cleared-row sentinel. Deterministic
slot `0` remains a valid generated geology slot and must not be used as a dead-row marker. Depletion, ore hash
derivation, and first-live telemetry reject negative deterministic slots before deriving sector-slot authority.

Runtime boundary: live ore truth remains the sector hash plus deterministic slot bit in `71533` (`DepletionMasks`).
`CandidateSlots=-1` is a presentation/read-model dead-row marker only; it is not persisted as geology authority and
does not create a second depletion fact.

Status: `STATIC SOURCE UPDATED - PENDING UNITY IMPORT / PROFILER PROOF`.

## 2026-05-20 SHINOBU_155 Respawn Shader Bridge Generation Descriptor Addendum

The shared shader-global bridge used by the player death Dear Lie route no longer persists a legacy
`VaultBufferHandle<float4>` for `BufferID.ShaderGlobalState`. `HectonShaderGlobalDataVaultBridge` now stores a
16-byte `VaultGenerationHandle<float4>`, reacquires existing slot descriptors through `IDataVault.TryGetGenerationHandle`,
allocates the shared slot buffer only through `IDataVault.GetGenerationHandle` when the caller explicitly allows
allocation and the Vault is unlocked, and resolves method-local `NativeArray<float4>` views through
`IDataVault.TryResolveHandle` before slot writes.

SHINOBU_155 continues to publish `_HectonRespawnDearLieParams` through `PublishRespawnDearLie(IDataVault, Vector4)`,
passing its cached `_dataVault` from VisualSync or teardown clear. That cached-vault overload passes
`allowAllocation:false`, so absent shader slot storage falls back instead of allocating from the dispatcher-facing respawn
route. The parameterless/generic bridge routes still contain `ResolveSlotsVault()` for legacy non-SHINOBU callers, but
the respawn route does not use that overload in dispatcher phases. Static source only; Unity import, Frame Debugger,
profiler/GCMonitor, and player-build proof remain pending.

## 2026-05-20 SHINOBU_224 Active Equipment Generation Descriptor Addendum

`ModularEquipmentEngine` no longer persists Vault-resolved `NativeArray<T>` aliases for active equipment state,
published readback, AUP samples, grid-load requests, wear rates, telemetry, tuning, hardware specs, or legacy tool
mirrors. The owner stores only 16-byte `VaultGenerationHandle<T>` descriptors and resolves phase-local
`NativeArray<T>` views through cached `IDataVault.TryResolveHandle` before mutation, publication, gizmo reads, CSV
ingest, or Burst scheduling. Missing, undersized, or stale descriptors are released before reacquire through
`GetGenerationHandle<T>`, preventing refcount drift after Vault relocation.

DataVault rebind and shutdown complete any pending `EquipmentStateIntegrationJob` through `DispatcherJobFence` before
descriptor release. The thermodynamic grid readback is not retained as owner state; it is resolved as a method-local
view and passed directly to the equipment Burst job for AUP-relative cooling. Runtime authority remains the Vault lanes
`ShinobuActiveEquipmentState`, `ShinobuActiveEquipmentPublishedState`, `ShinobuActiveEquipmentAupSamples`,
`ShinobuActiveEquipmentGridLoadRequests`, `ShinobuActiveEquipmentWearDrainRates`, telemetry/counter/tuning/spec lanes,
and typed `SignalBus<EquipmentOverheatSignal>` / `SignalBus<ToolDepletedSignal>` outputs.

Static source only: Unity import, Profiler/GCMonitor 0 B player proof, and player-build proof remain pending behind the
current cross-domain compile wall and CPU/build gate.

## 2026-05-20 SHINOBU_219 Visual Pressure Aging CSV and Quality Gate Addendum

`VisualPressureAgingRuntime` no longer polls `Data/Visuals/environmental_aging_rules.csv` from `PreSimulationTick`.
The CSV byte-slice parser and `VisualPressureAgingCsvScratch` Vault lane remain available, but disk access is now a cold
editor action through `TryReloadEditorCsv()` and the `Abyssal Base Aging Tuner` button `Reload CSV Profiles`. The
dispatcher-facing PreSimulation path only resolves cached Vault state and applies pending editor tuning.

`Hecton8_UberNoir.hlsl` visual pressure-aging ranges no longer depend on `_MATH_LOD_LOW` for the SHINOBU aging surface.
Albedo-array triplanar detail, macro noise, RustDetail sampling, POM UV work, corrosion normal detail, and rich surface
response are gated by `quality` and `H8UberNoirSmoothRange01` ramps. At low quality the path exits before high-cost
aging detail; at mid/high quality it blends into the richer procedural rust, salt, biomass, pitting, and glass crack
work without per-instance material mutation or dynamic aging decals.

Static source only: scoped scans found no frame-path CSV/File reachability and no binary LOD tokens inside SHINOBU aging
shader ranges. Unity import, shader compile, player build, and profiler/GC proof remain pending behind the CPU/build gate.

## 2026-05-20 SHINOBU_219 Visual Pressure Aging Lock Fence Addendum

`VisualPressureAgingRuntime.VisualSyncTick` now locks owned render payload lanes before upload and fault-dump reads:
`VisualPressureAgingParams`, `VisualPressureAgingRuntime`, `VisualPressureAgingTelemetryRing`, and
`VisualPressureAgingTelemetryCursor`, then releases them in reverse order. Editor tuning read, default hydration, and CSV
reload paths were normalized to ascending owned BufferID lock order for overlapping lanes. Runtime state still stores only
`VaultGenerationHandle<T>` descriptors and resolves method-local `NativeArray<T>` views per phase.

`Hecton8_UberNoir.hlsl` now passes the row-aware quality produced by `H8UberNoirVisualAgingQualityWeight(visualAging)`
into `H8UberNoirResolveRustPomUv`. RustDetail and POM gating therefore use the same Vault/global blended quality scalar
as procedural rust, salt, biomass, pitting, and glass micro-fracture masks.

`VisualPressureAgingInquisition` now reports the XML archaeology targets for `Rendering/` and `Construction/`:
`BaseCorrosion.cs`, `GlassFracture.cs`, exact `GetComponent<Renderer>().material.SetFloat`, and rust/algae/corrosion/glass
aging decal tokens. Static source only; Unity import, shader compile, player build, and profiler/GC proof remain pending
behind the CPU/build gate.

## 2026-05-20 SHINOBU_224 Active Equipment Hot-Path Closure Addendum

The active equipment processor now rejects runtime Unity position fallback in central equipment sampling. Equipped tools
derive equipment AUP from cached `IPlayerRuntimeContext` player pose/current AUP only; non-equipped tool AUP fails closed
instead of querying Transform hierarchy state. Water and depth scalars are resolved once per refresh/publish pass, then
fed into the contiguous slot loop.

`ModularEquipmentEngine.TryResolveSlot()` uses a two-phase route: local 16-slot owner mirror scan first, then a single
Vault view fallback only after the mirror misses. `ToolDurabilitySystem.TryResolveBuffer<T>()` releases stale or undersized
`VaultGenerationHandle<T>` descriptors before reacquiring, preserving Vault refcounts across relocation/rebind.
`EquipmentLayoutVerifier` keeps reflection-based field-offset validation in editor/development builds; player builds keep
unmanaged size checks only.

Static source only: SHINOBU runtime scans found no persistent native aliases, legacy Vault pointer APIs, hot managed native
allocations, LINQ/foreach, prefab Update/coroutine routes, or runtime Transform fallback. Unity import, player Profiler/GC
proof, and player build proof remain pending behind the CPU/build gate and the existing cross-domain compile wall.

## 2026-05-20 SHINOBU_201 ParallelFor Safety Proof Addendum

The SHINOBU SIMD lane-packed kernels continue to use Vault-owned, caller-resolved native lanes. No BufferID, DTO layout,
generation descriptor, persistent owner, or public runtime route changed in this addendum.

`VectorizedHydrodynamicsLane4Job.Velocities`, `VectorizedHydrodynamicsLane4Job.OutputForces`,
`VectorizedSpatialQueryLane4Job.ValidMask`, and `VectorizedFrustumCullLane8Job.VisibleIndexMask` now carry source-local
three-paragraph safety justifications for `[NativeDisableParallelForRestriction]`. The invariant is explicit:
callers schedule `ceil(Count / 4)` for lane-4 kernels or `ceil(Count / 8)` for lane-8 culling, lane k owns only its
closed row range, and tail duplicate stores clamp to the last in-range row inside one Execute only.

Static source only: runtime math and binary payloads are unchanged. Scoped scans found all safety proof markers present,
balanced source braces/preprocessor/non-ASCII, and no forbidden hot-path pattern matches; diff check reports only repository
LF/CRLF normalization warnings. Unity import, Burst Inspector, profiler/GCMonitor, and player-build proof remain pending
behind the CPU/build gate.

## 2026-05-20 SHINOBU_201 Hydrodynamic Approximation Gate Addendum

Hydrodynamic SIMD payloads are unchanged. `SimdHydrodynamicTuningDTO` keeps the same 64-byte layout and the same
`ApproximationQualityWeight` / `SinPolynomialDegree` fields. Loop 43 only changes the branch shape of the validity gate:
`VectorizedHydrodynamicsJob`, `VectorizedHydrodynamicsLane4Job`, and `ScalarHydrodynamicsReferenceJob` now evaluate the
finite and epsilon predicates with non-short-circuit `&` before feeding `math.select`.

Static source only: no BufferID, DTO layout, Vault descriptor, telemetry ABI, or public route changed. Unity import,
Burst Inspector, profiler/GCMonitor, and player-build proof remain pending behind the CPU/build gate.

## 2026-05-20 SHINOBU_155 Respawn Med-Bay Radius And Fault Flag Addendum

`RespawnTuningDTO.MedicalBaySearchRadiusMeters` remains inside the existing explicit 64-byte tuning row and is now an
active routing scalar, not dead configuration. The player death reconciliation PreSimulation resolver and the Burst
fallback scan both sanitize the tuning row, clamp the radius to `1..50000` meters, derive `radius * radius`, and reject
medical-bay candidates outside that designer-controlled radius before accepting a respawn target.

`InvalidTargetAup` no longer leaks from rejected med-bay candidates into a successful selected-bay route. Rejected
candidate flags are accumulated locally and published only when the final route falls back to the deterministic lifepod.
A valid selected bay publishes only its selected-route flags, preserving black-box fault semantics for the actual
rebirth result.

Static source only: no DTO size, Vault ID, signal payload, asmdef edge, or private native owner changed. Unity import,
Profiler/GCMonitor 0 B, and player-build proof remain pending behind the CPU/build gate.

Loop 69 static refinement: corrupt med-bay rows now feed a local rejected-candidate mask for non-finite bay AUP,
non-finite death delta, non-finite local distance, invalid terrain-clearance delta, and zero medical-bay hash. The mask
is published only when fallback lifepod is the final route, preserving selected-bay flag semantics. Cold mock med-bay
hydration now uses `GenerateMockRespawnPointsJob.Run(bays.Length)` rather than direct `Execute(i)` calls. No payload size
or Vault lane changed.

## 2026-05-20 SHINOBU_222 Drainage Solver Authority Addendum

`PumpNodeDTO` remains an explicit 32-byte row and `PipeEdgeDTO` remains an explicit 64-byte row; no binary payload size,
Vault lane, asmdef edge, or runtime owner changed in this addendum. The active drainage worktree now routes cold mock
topology generation through `IJob.Run()`, treats missing or undersized Logistics power rows as zero pump power, clamps
quantized drain units before integer conversion, and reports pump watts as Vault `PowerDraw * saturate(CurrentEvacuationRate / MaxPumpRate)`.

The editor-only `Base Drainage Tuner` readout no longer formats telemetry through managed label strings. It uses
pre-created UI Toolkit value fields updated through `SetValueWithoutNotify`, leaving the runtime Vault/Burst binary
contract untouched. Static scans found no direct `.Execute()`, stale Vault pointer-handle API, synthetic full-power
fallback, `StringBuilder`, `ToString(`, `CultureInfo`, or `Mathf.Min` in SHINOBU_222 files. Unity import, Burst
Inspector, Profiler/GCMonitor 0 B, and player-build proof remain pending behind the CPU/build gate.

## 2026-05-20 SHINOBU_202 Procedural Bone Blender Generation Descriptor Addendum

`ProceduralBoneBlenderRuntime` no longer persists legacy pointer-bearing Vault descriptors. Rig, frame-input,
parent-index, bind-pose, bone-state, bone-matrix, frame-stats, telemetry-ring, telemetry-cursor, tuning, and mock-AI
signal lanes are stored as 16-byte `VaultGenerationHandle<T>` descriptors and resolved into method-local
`NativeArray<T>` views through `IDataVault.TryResolveHandle` before editor reads, CSV profile writes, emergency mock
rig generation, Burst solve scheduling, telemetry reads, blackbox dumps, or GPU matrix upload.

DataVault replacement, disable, and destroy paths complete any outstanding procedural bone solver job before releasing
the exact owned descriptors through `IDataVault.ReleaseBuffer`. The existing fauna animation visual fake remains a
quality-weighted procedural wave/IK solve and GPU matrix upload path; no per-bone rigid-body ownership or persistent
Vault view was introduced. Static source only: Unity import, Burst Inspector, profiler/GCMonitor, and player-build proof
remain pending behind the current compile/build gate.

Assembly boundary proof: the edited SHINOBU_222 source files resolve to the existing parent
`Assets/_Project/Scripts/Hecton8.Core.asmdef`; no asmdef file was edited and no new sibling runtime reference was added.
Latest compile gate sample was 100% CPU with zero active `dotnet`/`csc` processes, so no build was launched.

Final route recheck: `GenerateMockDrainageNetwork()` now invokes `DrainageMockNetworkJob` through `IJob.Schedule`,
registers `_mockSeedHandle`, and finalizes through `DispatcherJobFence`.
The full SHINOBU_222 forbidden-pattern scan reported zero `.Execute()` matches. Latest compile gate samples stayed at
68-100% CPU with zero active `dotnet`/`csc` processes, still above the allowed build threshold.
## 2026-05-20 SHINOBU_217 ModuleTemplate Ghost Prefab Bypass Addendum

The construction socket binary payload surface is unchanged. `GhostPreviewDTO` remains the owner-local Vault row at
`70370`, `SocketStateDTO` remains the 64-byte socket truth row, and the CSR lanes remain `70371`/`70372`.

`PlayerBuilder.SpawnGhost()` now routes every buildable with a `ModuleTemplate` through the reusable runtime ghost proxy
instead of `ObjectPoolManager.Spawn(activeBuildable.ghostPrefab)`. This keeps active socket-module preview authority on
template socket definitions, Vault ghost rows, and the Dear Lie shader signal rather than an authored preview-prefab
hierarchy. Non-template buildables keep the existing prefab branch and are outside the SHINOBU socket-module route.

Static source only: no BufferID, DTO size, signal layout, or asmdef edge changed. Unity import, profiler/GCMonitor, Frame
Debugger, and player-build proof remain pending behind the existing Core.Memory compile wall.
## 2026-05-20 SHINOBU_201 Gameplay ParallelFor Safety Proof Addendum

Gameplay buoyancy payloads are unchanged. `BuoyancyStateDTO` remains an explicit 64-byte row
(`CurrentAUP@0` 24 bytes, `Velocity@24` 12 bytes, `VolumeCubicMeters@36`, `MassKg@40`,
`EntityHashID@44`, `Flags@48`, `_pad0@52`, `_pad1@56`). `BuoyancyDebugForceDTO` remains 128 bytes
and `BuoyancyTelemetryEntry` remains 64 bytes. No BufferID, Vault descriptor, signal payload, asmdef edge,
or runtime owner changed in this addendum.

`GenerateMockBuoyantObjectsJob.States` now declares `[WriteOnly, NativeDisableParallelForRestriction, NoAlias]`
and documents the exact one-scheduled-index-to-one-state-row seed invariant used with `UnsafeUtility.AsRef`.
`EvaluateBuoyancyJob.States` and `EvaluateBuoyancyJob.DebugForces` now carry source-local three-paragraph proofs
for the fixed stride/offset mapping. The dispatcher dependency remains:
mock seed handle -> buoyancy evaluation handle -> telemetry reduction handle. No private native array allocation
or shadow state was introduced.

Static source only: safety proof markers cover the three gameplay suppression fields, braces/preprocessor/non-ASCII
are balanced, the scoped forbidden hot-path pattern scan returned no matches, and diff check reports only repository
LF/CRLF normalization warnings. Unity import, Burst Inspector, profiler/GCMonitor, and player-build proof remain
pending behind the CPU/build gate.
