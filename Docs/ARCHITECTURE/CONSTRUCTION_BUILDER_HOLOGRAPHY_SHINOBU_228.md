# Construction Builder Holography - SHINOBU_228



Owner: `BUILDER_TOOL_HOLOGRAPHY_SYNC`



Status: static-source implemented; compile blocked by existing `Hecton8.Core.csproj` dependency wall and current CPU guard.



## Runtime Boundary



`PlayerBuilder` no longer creates or retains a preview prefab/proxy as placement truth.

- Equipped builder stores preview pose in local fields.
- Placement validation uses construction math.
- Preview publishes `ConstructionPreviewSignal`.
- Final modules still spawn through the existing object pool.
- Reason: final modules are gameplay objects, not preview ghosts.



Successful placement no longer publishes managed `BaseModulePlacedEvent` objects from `PlayerBuilder`. The placed-module fact is owned by `ConstructionManager.RegisterModule`, which already publishes value-type `HabitatConstructionSignal` through `GlobalSignals`.



- `HabitatConstructionManager` creation is confined to `PlayerBuilder.Awake`/`OnSpawn`.
- Creation route: `EnsureHabitatConstructionManagerCold`.
- `BindRuntimeReferences` only binds existing cold-owned services.
- It fails closed if the manager was not prepared.
- It no longer performs fallback `new HabitatConstructionManager()` during equip/bind.



`PlayerBuilder` resolves already-published `GlobalRegistry.Player` and `GlobalRegistry.Environment` context services only. It no longer calls `PlayerRuntimeContextService.EnsureRuntimeInstance()` or `EnvironmentRuntimeContextService.EnsureRuntimeInstance()` from the consumer bind/equip path.



- Build-resource readiness now reads the Inventory owner's native read-only SOA lanes: item IDs, stack counts, and craft-locked counts.
- `HasBuildResources` no longer copies `PlayerInventory.ItemPlacement` rows into a managed snapshot and subtracts craft reservations before reporting coverage.
- `PrepareCostBuffers` groups duplicate build-cost rows by item hash into bounded stack spans.
- Grouping runs before readiness and commit consumption.
- One inventory stack cannot satisfy duplicate rows independently.
- `HabitatConstructionManager` no longer owns managed cost transaction arrays.
- The builder-facing cost digest routes (`PlayerBuilder`, `BuilderStatusOverlay`, and `PDAConstructionTab`) mirror that grouped view with stack spans and `CountAvailableTotal`, so displayed counts match craft-reservation-aware readiness.
- Final resource consumption still uses Inventory owner transaction `TryRemoveFirstMatchingItemByHash`. Future route: Inventory-owned immutable counts plus reserve/commit/release API, not a SHINOBU-local cache.


`PDAConstructionTab` dependency model:

- Mirrors the builder overlay.
- Caches Player and Environment contexts from cold lifecycle.
- Refreshes through `IGlobalRegistryHotSwapListener`.
- `Tick` consumes cached refs and typed signal snapshots only.
- Rejected per-tick routes: `AutoResolve`, `GameBootstrapper.TryGetCurrentPlayerTransform`, parent traversal, `HUDNotification.TryGetActive`, `GlobalRegistry.ConstructionRuntime`.



Structural validation has one source route: `HabitatConstructionManager.ScheduleIntegrityValidation(...)`. Older `GameObject candidateGhost` and transform/socket overloads were removed after no-caller source scan.



Legacy `PlacementGhost` and `PFB_Ghost_*` prefabs are removed. Existing `BuildableData.ghostPrefab` references were nulled; retained schema field is compatibility-only, not preview authority.



## DTO Contract



`BuilderGhostStateDTO` is explicit 128 bytes:



- `LocalToWorld` at byte `0`



- `AUP_TargetPosition` at byte `64`



- `PrefabHashID` at byte `88`



- `ValidationFlags` at byte `92`



- `AnimationPhase` at byte `96`



- `ValidationStateHash` at byte `100`



The `double3` AUP starts on an 8-byte boundary. `BinaryLayoutManifest` and `BuilderHolographyStaticAudit` check this with `UnsafeUtility.SizeOf`, `UnsafeUtility.AlignOf`, and `Marshal.OffsetOf`.



`BuilderGhostIndirectArgsDTO` is explicit 16 bytes: `VertexCountPerInstance@0`, `InstanceCount@4`, `StartVertex@8`, and `StartInstance@12`. The removed `HectonBlueprintPreviewBatch.BlueprintPreviewInstance` DTO is no longer referenced by `BinaryLayoutManifest` or `ModularBaseConstructionValidator`.



## Vault Lanes



- `70940` `BuilderGhostStateDTO[128]`



- `70941` `BuilderGhostVisualDTO[128]`



- `70942` `HolographyTelemetryEntry[300]`



- `70943` `BuilderGhostStateDTO[10000]` mock validation set



- `70944` `byte[1024]` 8-corner SDF samples



- `70945` `BuilderGhostIndirectArgsDTO[1]`



- `70946` `BuilderGhostStateDTO[64]` VR pipe blueprint preview segments



- `70947` `BuilderGhostVisualDTO[64]` VR pipe blueprint visual payload



- `70948` `BuilderGhostIndirectArgsDTO[1]` VR pipe blueprint indirect args



- `70949` `IntegrityNodeRecord[]` structural validation node snapshot



- `70950` `int2[]` structural validation adjacency ranges



- `70951` `int[]` structural validation flattened adjacency



- `70952` `int[]` structural validation BFS queue



- `70953` `int[]` structural validation BFS depths



- `70954` `IntegrityValidationResult[1]` structural validation output



- `70955` `int[]` structural validation adjacency-degree scratch



- `70956` `int[]` structural validation adjacency-write scratch



- `70957` `int2[]` structural validation undirected connection pairs



- `70958` `SocketLookupSlot[]` structural validation open-addressed socket lookup rows



All lanes use `NativeArrayOptions.UninitializedMemory`; writers overwrite active rows each frame.



## Validation



Validation route:

- `BuildBuilderGhostStateJob`: snaps target AUP in double precision.
- GPU matrix emits only after runtime-origin subtraction.
- `ValidateBuilderGhostPlacementJob`: checks all 8 OBB corners and existing module bounds in Burst.
- Voxel density samples collect into the byte sample lane first.
- Collision decision and flags are made in Burst.



`GlobalQualityWeight` does not reduce placement truth.

`ShinobuSocketConstructionRuntime.BuilderGhostSdfCornerCount` is fixed at 8. Hydration and `ValidateBuilderGhostPlacementJob` both loop over that constant.

Quality controls visual shader cost and pipe preview density only. Placement legality fails closed on non-finite state, SDF block, or bounds block.



- The legacy construction terrain-probe bridge is also fixed truth: `PlayerBuilder.TryFindVoxelSdfIntersection` calls parameterless `ModularBaseConstructionValidator.ResolveTerrainProbeCount()`, and the helper returns `TerrainProbeTruthCount` (`9`).
- No terrain SDF legality route accepts `GlobalQualityWeight` as a probe-budget parameter.
- Builder terrain probes and ghost SDF hydration call `HectonVoxelVolume.TryReadRuntimeSdfDensity`, a non-mutating published-volume read that skips stale volume entries instead of removing them from the registry inside validation.



- Socket magnetism legality is fixed truth as well.
- `EvaluateSocketSnappingJob` evaluates the full CSR target range for each ghost socket.
- Legality radius: `SearchRadiusUltraMeters`.
- `ResolveCandidateBudget(int minBudget, int maxBudget)` returns maximum configured budget.
- `ResolveSearchRadius(float lowMeters, float ultraMeters)` returns high radius.
- These truth helpers no longer accept a `GlobalQualityWeight` parameter.
- Quality may still damp the visual Dear Lie shrink/pulse, but it cannot hide a compatible socket candidate.



- `HabitatConstructionManager` structural graph validation resolves generation handles for buffers 70949-70958 from the injected catalog Vault and releases them on Vault replacement or shutdown.
- The validation job consumes `IntegrityNodeRecord`, `int2` adjacency ranges, flattened adjacency, queue/depth arrays, and result slot through transient `NativeArray` views with `[NoAlias]` fields.
- Vault-backed: adjacency degree/write scratch lanes, undirected connection pair lane, 48-byte `SocketLookupSlot` lookup lane.
- Removed from manager ownership: private persistent graph buffers, managed adjacency scratch arrays, graph `List`/`Dictionary` caches.
- Active `BuildValidationGraph` uses `HasValidationGraphCapacity` and fails closed when prepared Vault lanes cannot hold the graph; scheduling no longer calls resize helpers.


- Cache-miss structural graph rebuilds no longer iterate `ConstructionManager.SpawnedModules` through `IReadOnlyList<GameObject>`.
- `ConstructionManager` owns the placed-module registry and exposes an internal `GetSpawnedModuleAt(int)` accessor beside `ModuleCount`; `HabitatConstructionManager` uses that indexed owner route for graph signatures and existing-node indexing.
- This removes interface-list dispatch from the SHINOBU validation path without creating a second placed-module authority.
- The stronger future route is still a ConstructionManager-owned immutable topology snapshot.



Preview validation consumption uses `DispatcherJobFence.TryFinalizeCompleted`.

`ResetValidation` marks pending result for discard and returns without completing the job. Only teardown disposal still forces completion in `HabitatConstructionManager`.



Black Box dump ownership is SHINOBU_228. `ShinobuSocketConstructionRuntime.DefaultDumpPath` writes `Docs/AgentLogs/Dump_SHINOBU_228.bin`, and `HolographyDumpPath` writes `Docs/AgentLogs/Dump_SHINOBU_228_Holography.bin`; SHINOBU_217 dump constants are not accepted in the builder holography route.



`VRPipeBlueprintPreview` stores runtime control-point overrides in four scalar `AbsoluteUniversePosition` fields and four scalar validity flags.

The presenter owns no managed point-cache arrays. Index access routes through bounded switch helpers before the Burst pipe DTO build job.



## Rendering



`HectonBlueprintPreviewBatch` flow:
- consume `ConstructionPreviewSignal`
- schedule `BuildBuilderGhostStateJob`
- write `BuilderGhostIndirectArgsDTO` through `BuildBuilderGhostIndirectArgsJob`
- upload double-buffered `GraphicsBuffer` data with `LockBufferForWrite`
- render through `Graphics.DrawProceduralIndirect`

The shader builds a cube from `SV_VertexID`, reads `ValidationFlags`, and scales glow/scan/chromatic math by `GlobalQualityWeight`.



The visual-sync path is deferred.

State, visual, and indirect-args jobs publish into a pending `JobHandle`.

`LateFrameTick` uploads only after `DispatcherJobFence.TryFinalizeCompleted`; current frame keeps the previous buffer and avoids mid-frame completion.



- Presenter finalize paths do not create or resize `GraphicsBuffer`s.
- `Awake`/`OnEnable`/cold XR activation own `EnsureGraphicsBuffers`.
- Finalize only gates on `HasGraphicsBuffers()`.
- It fails closed when cold boot did not provision buffers.



Socket magnetism follows the no-readback rule.

After scheduling snap jobs, `PlayerBuilder` returns the cached snap pose for the current frame. Next update finalizes through `DispatcherJobFence.TryFinalizeCompleted`.

There is no immediate same-frame socket-snap result readback after scheduling.



Unchanged `ConstructionPreviewSignal` batches are hashed without the frame counter.

Identical active payloads do not re-upload DTO/args buffers. Shader time remains procedural, so hologram animation does not require unchanged matrix uploads.



`HectonBlueprintPreviewBatch.LateFrameTick` writes one active presentation sample into `HolographyTelemetryEntry[300]` through the cached Vault view.

This replaces the unused tiny telemetry job. The owner presentation phase updates the ring without same-frame schedule/readback.



- Draw bounds derive from active DTO matrices.
- Bounds are rejected against camera near/far before indirect draw submission.
- Runtime material fallback is closed.
- Editor-only fallback may create a temporary `DontSave` material for inspection.



- `VRPipeBlueprintPreview` uses the same Dear Lie shader and indirect draw path.
- Its four AUP control points feed `BuildPipeBlueprintPreviewJob`, which emits cuboid pipe segment matrices into Vault buffers 70946-70948.
- Low `GlobalQualityWeight` continuously increases segment length to reduce instance count; higher weights shorten segments and let the shader spend more ALU on scan/rim/chromatic detail.
- Legacy mesh-preview fields are absent: `HectonBlueprintPreviewBatch` no longer exposes `previewMesh` or `BlueprintPreviewInstance`, and `VRPipeBlueprintPreview` no longer exposes `segmentMesh`.
- Both presenters submit only DTO and indirect-args payloads.



`VRPipeBlueprintPreview` caches the `HectonXRRuntimeState.XRActiveChangedHandler` delegate in cold lifecycle and subscribes with that cached field, avoiding method-group delegate creation on repeated XR preview enable/disable.



- `VRPipeBlueprintPreview` mirrors main preview descriptor discipline.
- Lanes `70946..70948`: pointer-free `VaultGenerationHandle<T>` descriptors.
- Hot phases resolve transient views through `IDataVault.TryResolveHandle`.
- Runtime point AUP conversion reads `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
- Payload frames use `TimeSliceScheduler.CurrentFrameId` with owner-local fallback counter.
- It no longer uses legacy `VaultBufferHandle<T>`, `GlobalSignals.CurrentRuntimeOriginAup()`, or `Time.frameCount` in the pipe preview payload route.



## Rollback Exclusion



- Builder hologram buffers are local presentation state.
- They carry `PresentationOnly | RollbackExcluded` flags.
- They are not registered in `HectonRollbackNetcodeRuntime.InitializeAuthoritativeMerkleDescriptors()`.
- Gameplay authority remains final module placement, module hashes, AUPs, resources, construction graph state.



## Verification Boundary



- Static scans passed for the SHINOBU_228 route: no builder `_currentGhostObj` state
- no first-party `PlacementGhost` source/assets
- no `PFB_Ghost_*` prefabs
- no non-null `BuildableData.ghostPrefab` asset references
- no preview `OverlapBoxNonAlloc`
- no runtime presenter `GlobalDataVault.TryGetLatestCreated` fallback
- no `DrawMeshInstanced`
- no `.SetData(` in target holography upload files
- no direct `.Complete(` in `HectonBlueprintPreviewBatch.cs`
- no `Matrix4x4[]` cache in `VRPipeBlueprintPreview.cs`
- no `BlueprintPreviewInstance` source/layout-gate reference
- no serialized `previewMesh`
- no serialized `segmentMesh`
- no active `BuildValidationGraph` Vault resize call
- no `EnsureAdjacencyCapacity`
- no `HabitatConstructionManager` `IReadOnlyList<GameObject>`/`SpawnedModules` validation iteration
- no forced completion from `ResetValidation`
- no PlayerBuilder runtime-context owner creation
- no `PDAConstructionTab.Tick` `AutoResolve`/scene-bootstrap fallback
- no socket truth helper `GlobalQualityWeight` parameter
- no `HabitatConstructionManager` `_inventoryPlacementBuffer`
- no managed build-cost transaction arrays
- no `GetPlacements(` readiness copy, grouped build-cost preparation through `FindCostGroupIndex` and `TryAccumulateCostAmount`, grouped craft-reservation-aware builder cost presentation in `PlayerBuilder`, `BuilderStatusOverlay`
- and `PDAConstructionTab`
- no PlayerBuilder call to mutating `TrySampleRuntimeSdfDensity`
- no VR pipe `VaultBufferHandle<T>`/`ResolveBuffer`/`GetBufferHandle` residue
- no VR pipe `GlobalSignals.CurrentRuntimeOriginAup()` or `Time.frameCount` payload source
- no `TryResolveSocketAlignment(` or `candidateGhost` object validation route
- and no static-audit self-residue from unsplit forbidden probe literals, including socket-trigger, collider, PhysX, fixed-joint, instantiate/destroy
- and object-creation probes in the editor scanners.

- Guarded `dotnet build Assembly-CSharp.csproj --no-restore /p:UseSharedCompilation=false` ran only when CPU/process guard was clear. It failed on broader Core dependency/project drift.

- No Unity import, Play Mode, profiler, Frame Debugger, GCMonitor, or player build proof is claimed.



`BuilderHolographyStaticAudit` updates `Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json` by replacing or inserting only the `SHINOBU_228` object.

If the shared report cannot be spliced safely, the editor audit writes `Docs/Reports/MEMORY_OPTIMIZATION_REPORT.SHINOBU_228.json` and leaves the shared report untouched. Malformed-report recovery cannot delete other agents' proof sections.



`HectonBlueprintPreviewBatch.RecordActiveTelemetryHeartbeat` is fenced against the visual DTO producer.

If `_pendingBuildScheduled` is true, heartbeat returns without reading `BuilderGhostStateDTO` or `BuilderGhostVisualDTO`. Telemetry resumes only after `TryFinalizePendingBuildAndUpload`; the 300-frame ring stays an immutable snapshot consumer.
