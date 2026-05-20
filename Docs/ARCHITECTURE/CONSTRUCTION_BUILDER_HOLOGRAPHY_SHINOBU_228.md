# Construction Builder Holography - SHINOBU_228

Owner: `BUILDER_TOOL_HOLOGRAPHY_SYNC`
Status: static-source implemented; compile blocked by existing `Hecton8.Core.csproj` dependency wall and current CPU guard.

## Runtime Boundary

`PlayerBuilder` no longer creates or retains a preview prefab/proxy as placement truth. The equipped builder stores preview pose in local fields, validates structural placement through construction math, and publishes `ConstructionPreviewSignal`. Final placed modules still spawn through the existing object pool because those are gameplay objects, not preview ghosts.

Structural validation now has one source route: `HabitatConstructionManager.ScheduleIntegrityValidation(constructionManager, BuildableData, Vector3, Quaternion, gridSize, budget, penalty)`. The older `GameObject candidateGhost` and transform/socket alignment overloads were removed after source scan showed no callers.

The legacy `PlacementGhost` script and `PFB_Ghost_*` prefabs are removed. Existing `BuildableData.ghostPrefab` serialized references in construction data were nulled; the retained schema field is compatibility-only and is not a preview authority route.

## DTO Contract

`BuilderGhostStateDTO` is explicit 128 bytes:

- `LocalToWorld` at byte `0`
- `AUP_TargetPosition` at byte `64`
- `PrefabHashID` at byte `88`
- `ValidationFlags` at byte `92`
- `AnimationPhase` at byte `96`
- `ValidationStateHash` at byte `100`

The `double3` AUP starts on an 8-byte boundary. `BinaryLayoutManifest` and `BuilderHolographyStaticAudit` check this with `UnsafeUtility.SizeOf`, `UnsafeUtility.AlignOf`, and `Marshal.OffsetOf`.

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

All lanes use `NativeArrayOptions.UninitializedMemory`; writers overwrite active rows each frame.

## Validation

`BuildBuilderGhostStateJob` snaps target AUP in double precision and emits the GPU matrix only after subtracting the runtime origin. `ValidateBuilderGhostPlacementJob` checks all 8 OBB corners and existing module bounds in Burst. Voxel density sampling is collected into the byte sample lane first; the collision decision and flags are made in Burst.

`GlobalQualityWeight` does not reduce placement truth. `ResolveBuilderGhostSdfSampleCount` returns the fixed 8-corner validation count; quality controls visual shader cost and pipe preview presentation density only. Placement legality fails closed on non-finite state, SDF block, or bounds block.

## Rendering

`HectonBlueprintPreviewBatch` consumes `ConstructionPreviewSignal`, schedules `BuildBuilderGhostStateJob` for DTO matrix/visual writes, writes `BuilderGhostIndirectArgsDTO` through `BuildBuilderGhostIndirectArgsJob`, uploads double-buffered `GraphicsBuffer` data with `LockBufferForWrite`, and renders through `Graphics.DrawProceduralIndirect`. The shader procedurally builds a cube from `SV_VertexID`, reads `ValidationFlags`, and scales glow/scan/chromatic math continuously by `GlobalQualityWeight`.

The visual-sync path is deferred. State, visual, and indirect-args jobs publish into a pending `JobHandle`; `LateFrameTick` uploads only after `DispatcherJobFence.TryFinalizeCompleted` reports the payload ready. The current render frame keeps using the previously uploaded buffer, so the presentation path does not force a mid-frame job completion.

Unchanged `ConstructionPreviewSignal` batches are hashed without the frame counter and do not re-upload DTO/args buffers when the active payload is identical to the already uploaded batch. Shader time remains procedural, so the hologram can animate without wasting PCIe bandwidth on unchanged matrices.

Draw bounds are derived from active DTO matrices and rejected against camera near/far before submitting the indirect draw. Runtime material fallback is closed; editor-only fallback may create a temporary `DontSave` material for inspection.

`VRPipeBlueprintPreview` uses the same Dear Lie shader and indirect draw path. Its four AUP control points feed `BuildPipeBlueprintPreviewJob`, which emits cuboid pipe segment matrices into Vault buffers 70946-70948. Low `GlobalQualityWeight` continuously increases segment length to reduce instance count; higher weights shorten segments and let the shader spend more ALU on scan/rim/chromatic detail. The legacy serialized `segmentMesh` remains only as inert asset compatibility data and is not submitted.

## Rollback Exclusion

Builder hologram buffers are local presentation state. They carry `PresentationOnly | RollbackExcluded` flags and are not registered in `HectonRollbackNetcodeRuntime.InitializeAuthoritativeMerkleDescriptors()`. Gameplay authority remains final module placement, module hashes, AUPs, resources, and construction graph state.

## Verification Boundary

Static scans passed for the SHINOBU_228 route: no builder `_currentGhostObj` state, no first-party `PlacementGhost` source/assets, no `PFB_Ghost_*` prefabs, no non-null `BuildableData.ghostPrefab` asset references, no preview `OverlapBoxNonAlloc`, no runtime presenter `GlobalDataVault.TryGetLatestCreated` fallback, no `DrawMeshInstanced`, no `.SetData(` in target holography upload files, no direct `.Complete(` in `HectonBlueprintPreviewBatch.cs`, no `Matrix4x4[]` cache in `VRPipeBlueprintPreview.cs`, and no `TryResolveSocketAlignment(` or `candidateGhost` object validation route. A guarded `dotnet build Assembly-CSharp.csproj --no-restore /p:UseSharedCompilation=false` was attempted only when CPU/process guard was clear; it failed in `Hecton8.Core.csproj` on broader unresolved project dependencies and generated-project inclusion drift. No Unity import, Play Mode, profiler, Frame Debugger, GCMonitor, or player build proof is claimed.
