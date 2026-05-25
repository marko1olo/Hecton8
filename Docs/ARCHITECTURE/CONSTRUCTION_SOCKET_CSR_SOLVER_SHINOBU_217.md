# Construction Socket CSR Solver - SHINOBU_217



Domain: Habitat & Vehicles / Grid Snapping & Ghost Preview.



The construction socket path now owns a data-oriented snap layer:



- `SocketStateDTO` is 64 bytes, explicit layout, raw fields only.



- Vault lanes `ConstructionSocket*` hold `ConstructionSocketModuleDTO` records, socket states, AUP positions, snap results, tuning, bounds, counters, connection pairs, and the 300-frame black box.
- Owner-local buffer `70370` stores the active `GhostPreviewDTO`; owner-local buffers `70371` and `70372` store direction CSR ranges and target indices.
- Socket telemetry dump targets are `Docs/AgentLogs/Dump_SHINOBU_217.bin`, `Docs/AgentLogs/Dump_SHINOBU_217_Holography.bin`, and `Docs/AgentLogs/Dump_SHINOBU_217_ConstructionValidation.bin`; these are planned/generated-on-fault targets, not existing runtime artifacts unless a timestamped trigger and output are linked.
- These dump APIs write NativeArray bytes through a `ReadOnlySpan<byte>` pointer view rather than allocating dump-sized managed mirror buffers.



- `EvaluateSocketSnappingJob`:
  - Math space: `double3` AUP socket deltas.
  - Read scope: inverse-direction CSR bucket per ghost socket.
  - Quality budget: consumed after target row resolution.
  - Invalid direction masks: rejected before compatibility math.
  - Invalid CSR target rows: `NonFinite`; bucket continues.
  - Missing CSR range/index lanes: `CapacityExceeded`; no direct-scan fallback.
  - Output: aligned `float4x4` snap matrix.



- `AdaptConnectedSocketsJob`, `VerifyModuleBoundsJob`, and `CommitPlacedModuleJob` mutate socket flags and pending topology counters without touching GameObjects.



- Runtime proxy sockets no longer create trigger colliders.
- Active preview `PlayerBuilder.SpawnGhost()` is data-only: releases legacy ghost object, sets `_builderGhostPreviewActive`, and stores pose/rotation/scale instead of spawning `ghostPrefab`.
- Ghost socket truth is hydrated from `BaseModuleTemplate.SocketDefinitions`, builder preview pose fields, and `GhostPreviewDTO`, not preview-prefab hierarchy components.
- Connection visuals route through `Hecton8/Construction/DearLieHologram` and the active `Hecton8/Fabrication/BlueprintWireInstanced` preview shader.
- `ConstructionPreviewSignal` remains 128 bytes.
- Aligned padding offsets: `DearLieDampen@96`, `GlobalQualityWeight@100`, `DearLieWiggleSpeed@104`.
- `HectonBlueprintPreviewBatch` applies those values as a decaying material envelope, not snapped-prefab animation.
- Envelope resets when preview count reaches zero.
- Cold fallback proxy materials initialize `_H8SnapDampen` to `0`, so the fake is not permanently active on unsnapped module visuals.



Current migration boundary:



- `PlayerBuilder` uses cached DataVault views and a scheduled deterministic Burst chain instead of `Physics.OverlapSphereNonAlloc` for active structural socket snapping.
- SHINOBU active bridges do not fall back to `GlobalRegistry.DataVault`; binding/`InitializeVault()` run cold in `BindRuntimeReferences()`. Active snap/validation use pure cached-vault gates.
- `HectonBlueprintPreviewBatch` likewise binds its builder-holography `VaultGenerationHandle<T>` descriptors in `Awake()`/`OnEnable()` through `EnsureBuffersCold()`; active upload and signal consumption use `TryReadCachedBuffers()` with `IDataVault.TryResolveHandle(...)` only.
- The mutating socket-alignment bridge is named `TryUpdateShinobuSocketAlignment()` because it can prepare owner-local CSR rows, schedule jobs, finalize prior results, and update cached pose state.
- `EvaluateSocketSnappingJob` is `IJobParallelFor`; `SelectBestSocketSnapJob` depends on it, reduces into the `SnapResults` sink row, and finalizes through `DispatcherJobFence.TryFinalizeCompleted`.
- Builder holography/SDF validation:
  - `BuildBuilderGhostStateJob` schedules first.
  - `ValidateBuilderGhostPlacementJob` depends on it.
  - Updates final `BuilderGhostStateDTO` and sibling `BuilderGhostVisualDTO` flags/alpha.
  - Consumes the same deterministic all-eight corner order written by CPU hydration.
  - Registers final handle with construction memory.
  - Active preview ticks consume output only through `TryFinalizeCompleted`.
- Target socket source is Vault-only in the active bridge.
- Topology hash derives from Vault counters and `ConstructionSocketModuleDTO` rows.
- CSR is prepared from pre-published socket rows.
- Validation payloads read `ConstructionSocketModuleDTO.RootAup` before using current preview position as fallback.
- Pending and cached snap results additionally require a query hash over ghost root, yaw, blueprint hash, and ghost socket layout before a pose can be reused.
- Builder validation pending results require a separate query hash over module hash, preview pose/rotation, proxy bounds center/size, and snap/DearLie flags.
- Blueprint hash uses `ResolveShinobuModuleHash()`, so `ModuleHashId == 0` falls back to `TemplateHashId`; the same fallback is used for `ConstructionPreviewSignal.ModuleHash`, construction validation payloads, acoustic source fallback, and `FloraExclusionSignal.ModuleHash`.
- Cached Dear Lie pose state is invalidated on query mismatch, no-snap reducer results, failed result application, unsnap, placement reset, and builder reset; `float.MaxValue` cached distance is rejected.
- `HectonBlueprintPreviewBatch` derives alpha, telemetry SDF sign, and `_lastPreviewAllowed` from current-row validation flags.
- It ignores previous-frame `_lastPreviewAllowed` and pre-sanitized signal flags.
- All preview scale axes must be positive before valid flags survive.
- SDF/bounds/non-finite/invalid-scale truth reaches shader and black-box payloads immediately.
- Ghost socket Vault rows preserve source `SocketDefinitions` indices; invalid ghost rows are flagged `NonFinite | CollisionBlocked` and receive zero CSR range rather than being packed away.
- Invalid authored directions are not quantized to North, and the final snap-result sink rejects invalid target or ghost directions before calculating the pose.
- Active snapping fails closed if target socket rows have not been published into Vault.
- After SHINOBU snap placement, `PlayerBuilder` writes module and socket rows directly into Vault.
- It marks target socket and consumed ghost socket `Connected`.
- It writes one connection pair into `ConstructionSocketConnections`.
- It updates `Counters[4]` and invalidates cached topology on fail-closed commit.
- The per-frame snap decision operates on Vault arrays.



- Frame identity route:
  - Source: `TimeSliceScheduler.CurrentFrameId`.
  - Scope: SHINOBU-owned preview, validation, holography bridge.
  - Fallback: `CaptureShinobuFrameId()` / `CapturePreviewFrameId()` use owner-local monotonic counters only when dispatcher frame is zero.
  - Removed from `PlayerBuilder` / `HectonBlueprintPreviewBatch`: Unity `Time.frameCount`, `Time.unscaledTime`, `Time.time`.
  - Dear Lie phase: `frame / 120`.
  - BuilderGhost validation hashes do not depend on wall-clock time.



- SHINOBU builder/preview/habitat origin conversion does not call `GlobalSignals.CurrentRuntimeOriginAup()`.
- `PlayerBuilder`, `HectonBlueprintPreviewBatch`, and `HabitatConstructionManager` resolve `HectonFloatingOrigin.CurrentTotalOffsetDouble` through finite-guarded helpers.
- Jobs/socket hydration receive the double3 origin; subtraction happens in double precision before `Vector3` cast.



- The authored semantic placement-rule cache no longer owns a reusable `List<MonoBehaviour>` buffer or active `IBuildPlacementRule` interface dispatch.
- `CacheActivePlacementRule()` performs cold direct sealed-component lookup for the `DeepDrillModule` and `AutonomousExtractorModule` providers, stores a byte rule-kind tag, and active preview validation calls `ValidatePlacementWithService()` / `ValidatePlacementWithRuntime()` with cached dependencies.
- Deep-drill validation no longer builds `InteractionPacket`, stamps Unity time, or polls `GlobalRegistry.InteractionSignals`. Active providers use fixed `DeepDrillModule[128]` plus `s_ActiveModuleCount`.
- Extractor validation no longer creates its runtime owner during validation and no longer scores candidates through transform-position fallback.
- The remaining extractor `Physics.OverlapSphereNonAlloc` is a documented semantic-rule residue until the resource-node owner publishes an unmanaged spatial snapshot.



- The adjacent extractor runtime no longer uses a growable managed module registry.
- `AutonomousExtractorSystem` keeps a fixed `AutonomousExtractorModule[256]` plus `_moduleCount`, bounded registration, and explicit compaction without `List<T>.Add/RemoveAt`.
- Its `ExtractorJobInput`/`ExtractorJobResult` rows are explicit 32-byte records, and `AdvanceExtractionJob` uses deterministic synchronous Burst with `[NoAlias]` input/result lanes because extractor cycle completion feeds gameplay-visible inventory/power state.
- The unreferenced `AutonomousExtractorJobs.cs` duplicate was deleted.
- Resource-host semantic migration is blocked at the contracts boundary.
- `Hecton8.World.Contracts` exposes ore position/type read lanes.
- Missing without `Hecton8.World.Economy`: extractor support, yield item hash, host diameter, depletion, stable claim identity.
- The extractor private NativeArray SOA lanes remain a separate extractor-owner Vault migration item, not SHINOBU socket truth.



- Mock grid counter route:
  - `GenerateMockBaseConstructionGrid()` clears every `ConstructionSocketCounters` entry.
  - Module/socket/topology values are written after the clear.
  - Mock `Counters[4]` connection-pair count is zero, not stale `UninitializedMemory`.
  - `InitializeVault()` seeds counters only when the lane is absent, too short, or outside capacities.
  - Valid existing topology counters are preserved.
  - `TryResolveVaultViews()` remains resolve-only.



- Active module selection does not cross the compile/runtime service boundary.
- `CycleBuildable()` and `DebugDeployActiveBuildable()` consume cold-cached references and do not call `BindRuntimeReferences()`.
- `SetActiveBuildable()` no longer force-completes pending SHINOBU socket snap or builder-ghost validation jobs; active selection and post-placement preview refresh call `DespawnGhost(forceValidationReset:false)`.
- Pending socket snap and builder-ghost jobs store active buildable generation at schedule time.
- Cached snap reuse checks that generation.
- Stale completed results are discarded only after `DispatcherJobFence.TryFinalizeCompleted()` reports natural completion.



- Construction tuning reads are provenance-explicit.
- `ModularBaseConstructionValidator.TryReadTunerSettingsFromVault()` returns `false` plus default output for missing/invalid Vault data.
- `PlayerBuilder.TryBuildConstructionValidationPayload()` falls back to `GetTunerSettings()` only when Vault read fails.
- The read facade no longer hides static cached tuner state.



- `ModularBaseConstructionValidator` no longer stores pointer-bearing `VaultBufferHandle<T>` lanes.
- Tuning, telemetry, bounds, and occupancy descriptors are `VaultGenerationHandle<T>`.
- Explicit ensure/write routes acquire through `GetGenerationHandle(...)`.
- Read routes resolve with `IDataVault.TryResolveHandle(...)`.



- Builder surface hits are interaction-owned.
- `PlayerBuilder.TryGetBuildHit()` no longer calls `UnityEngine.Physics.RaycastNonAlloc` or owns a `RaycastHit` buffer.
- Preview/deconstruction targeting consumes cold-cached `IInteractionSignalService.TryRaycastPrimary()` runtime-position overload with finite guards and stable requester id.
- Missing interaction service fails closed; no private PhysX fallback.



- Builder deconstruction target identity is registry-owned.
- After an interaction-owned hit, `PlayerBuilder` resolves the collider through `LaserCutterTargetRegistry.TryResolveModule()`.
- `BaseModule.OnEnable()` populates module collider trees.
- Active deconstruction target path no longer calls `GetComponentInParent<BaseModule>()` or scene hierarchy traversal.



- `HabitatConstructionManager` now uses a SHINOBU socket-Vault topology signature for existing integrity graph cache invalidation when Vault module count matches the construction registry count.
- The signature folds `ConstructionSocketModuleDTO` AUP/rotation/socket/topology fields and `SocketConnectionPairDTO` connection rows.
- If the Vault topology is absent or count-mismatched, the fallback scene signature hashes `ModuleHashId`, family, AUP-quantized root, and rotation bits instead of Unity instance IDs.
- This does not claim full Vault-only integrity graph source.
- Missing socket DTO facts: support-root/family and resource mass.
- Scene-built graph fallback remains until a construction-owner route card adds those facts.



- `HabitatConstructionManager.BuildAdjacency()`:
  - Validates unmanaged connection rows before adjacency scratch access.
  - Requires distinct endpoints within active node count.
  - Applies the endpoint check during degree counting and final adjacency writes.
  - Fences `AdjacencyRanges` capacity and `_connectionCapacity`.
  - Fails closed on adjacency-count overflow.
  - Invalidates graph cache on malformed rows before integrity validation jobs.



- `ModuleSocket` components remain only for legacy authored sockets outside the SHINOBU Vault snap route. SHINOBU occupancy performs no authored-component hierarchy scans.



- Vehicle docking triggers are outside this snap-preview route and remain under vehicle docking ownership.



- 2026-05-21 correction: the active SHINOBU snap update no longer hydrates target socket rows from `ConstructionManager.SpawnedModules`, `GameObject.GetInstanceID()`, `ModuleMarker`, or module transforms.
- Inputs: already-published `ConstructionSocketModuleDTO`, `SocketStateDTO`, socket AUP, and connection-pair rows from Vault.
- Snap topology hash uses Vault counters/module/connection rows.
- It prepares only the owner-local CSR cache.
- It fails closed when construction owner has not published target socket rows.
- Occupied-cell validator scans finite `ConstructionSocketModuleDTO.RootAup` rows from the same Vault view and compares AUP-local integer `GridPos`.
- It no longer locks or hydrates `ConstructionBuilderOccupancy` from managed scene modules.
- `TryCommitShinobuSnapOccupancy()` no longer requires scene-list index or spawned-module transform read.
- Placed-module rows use `SceneModuleListIndex = -1`.
- Pose source: already selected placement command.
- Snap truth is the Vault row, not a scene component.


- Scalability is continuous through `GlobalQualityWeight`.
- `ResolveCandidateBudget()` and `ResolveSearchRadius()` apply `SmoothQuality()` plus `math.lerp()`.
- CSR target-row budget scales `16..256`.
- Search radius scales from near construction sector to ultra search radius.

- `EvaluateSocketSnappingJob` consumes those resolved values; it no longer runs the max candidate budget or ultra radius on low quality.

- Builder placement truth does not use quality to skip SDF corners.
- CPU hydration and `ValidateBuilderGhostPlacementJob` evaluate all eight deterministic bounds corners.
- Terrain placement truth uses fixed `TerrainProbeTruthCount = 9` for PlayerBuilder and validator job.

- Quality remains presentation/search-budget authority only.

- The target direction CSR contains open finite sockets only; occupied, blocked, non-finite, and invalid-direction rows are excluded during cold CSR rebuild.

- The runtime budget is consumed when a CSR target row is read, before radius/compatibility/alignment rejection, so low quality bounds memory bandwidth even when open sockets are far.

- Direction CSR removes incompatible buckets before that quality budget is spent.

- Missing CSR lanes fail closed with `CapacityExceeded`; they do not trigger a linear `0..TargetCount` fallback.

- Snap result storage is `64` ghost rows plus one best-result sink row to avoid aliasing.



Rollback construction validation jobs use deterministic Burst math: `BurstGridValidationJob`, `LogisticsGraphSpliceJob`, `DeconstructionConnectivityJob`, and `HabitatConstructionManager.IntegrityValidationJob` all use `FloatMode.Deterministic`. Fast math is reserved for presentation-only work outside this placement/connectivity truth boundary.



Verification boundary, 2026-05-20:



- `Hecton8.Core.csproj` now includes SHINOBU socket runtime files, and `Hecton8.Editor.csproj` includes the SHINOBU editor tuner/layout tools.



- `dotnet build Assembly-CSharp.csproj --no-restore --nologo` was reportedly attempted after CPU gate passed at `29.96%`.
- Treat as `CLI_COMPILE_ATTEMPTED` until log path, command, timestamp, environment, and output are linked.
- Earlier `PlayerBuilder` missing-SHINOBU-type errors are not current clean compile proof.



- Compile remains blocked by the Core.Memory asmdef surface.
- Referenced `Library/ScriptAssemblies/Hecton8.Core.Memory.dll` is stale and lacks `VaultGenerationHandle<T>`.
- Source `GlobalDataVault.cs` defines the newer generation-handle API.
- Regenerating/importing that assembly is Core.Memory dependency.
