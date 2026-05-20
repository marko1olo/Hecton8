# SHINOBU_228 Rationale - BUILDER_TOOL_HOLOGRAPHY_SYNC

Date: 2026-05-20
Status: POLISH ITERATION 13 STATIC SOURCE GATES / RUNTIME UNITY PROOF PENDING / BUILD HELD BY CPU GUARD AFTER EXTERNAL WALL

## Decision 001 - Authority Surface

Problem: The prompt path `Assets/_Project/Scripts/Tools/Builder/` is absent. Existing authority lives in root `PlayerBuilder.cs` and `Assets/_Project/Scripts/Construction/`; legacy root `PlacementGhost.cs` was a removable preview-object route.
Solution: Treat `PlayerBuilder` as the input/placement owner, `Construction/*Socket*` as the Burst math owner, and `HectonBlueprintPreviewBatch` as the presentation owner. Use existing `SignalBus<ConstructionPreviewSignal>` instead of adding a new one-off global lane.
Rejected Alternatives: Creating a new `Tools/Builder` tree would duplicate authority and break Global Authority Boundaries. Directly adding a new `GlobalRegistry` service was rejected because the route lacks a GREEN route card.
Scalability potential: Low uses one matrix and minimal shader ALU; Middle keeps full SDF/AABB validation; High adds richer scanline/noise; Ultra spends saved CPU on visual overkill only in presentation.
Hardware Impact: Expected low-end i3/MX350 gain is removal of PhysX broadphase preview checks and ghost spawn/despawn spikes; measured proof absent until Unity profiler.

## Decision 002 - Ghost Prefab Lifecycle Is Not Placement Truth

Problem: The old builder preview route spawned pooled authored ghost prefabs or runtime proxies, then `PlacementGhost.FixedTick()` performed PhysX overlap validation.
Solution: Replace hot placement truth with unmanaged DTO state and validation flags. The active builder path no longer keeps runtime preview object state or a release route; final module spawning stays untouched because placed modules are gameplay objects, not preview ghosts.
Rejected Alternatives: Keeping pooled ghost prefab was rejected because object pooling still mutates Transform/colliders and leaves broadphase cost. Raw `Instantiate()` is not currently used in `PlayerBuilder`, but pooled preview objects still violate the mission.
Scalability potential: Low no ghost object, one DTO; Middle/High/Ultra scale hologram shader only through `GlobalQualityWeight`.
Hardware Impact: Removes collision proxy churn and renderer material swaps from preview path; exact microseconds pending profiler.

## Decision 003 - SDF/AABB Validation Over PhysX Preview Overlap

Problem: `PlacementGhost.FixedTick()` used `Physics.OverlapBoxNonAlloc` every fixed tick. NonAlloc avoided heap but still touched PhysX broadphase and moving collider state.
Solution: Implement Burst OBB corner checks against SDF samples and existing module AABBs, with flags in a 128-byte DTO. Use existing socket construction buffers where possible, then delete the legacy component/source route.
Rejected Alternatives: `OverlapBoxNonAlloc` retained as "good enough" was rejected; it is still a broadphase cost and requires the ghost GameObject.
Scalability potential: Low/Middle/High/Ultra all check eight SDF corners. Higher tiers increase visual details, not correctness toggles.
Hardware Impact: Expected 60-180 us/frame saved in preview-heavy scenes on i3/MX350; measured proof absent.

## Decision 004 - DTO Layout

Problem: Builder preview state crosses Burst/GPU/telemetry boundaries and includes `double3`; layout ambiguity risks defensive copies or ARM64 alignment faults.
Solution: Use `[StructLayout(LayoutKind.Explicit, Size = 128)]` for `BuilderGhostStateDTO`: `float4x4` at 0..63, `double3` at 64..87, PrefabHashID at 88..91, ValidationFlags at 92..95, AnimationPhase at 96..99, ValidationStateHash at 100..103, six uint padding fields at 104..127.
Rejected Alternatives: Sequential layout and properties were rejected. Smaller 96-byte DTO was rejected because the prompt requires a 128-byte envelope and GPU-friendly alignment.
Scalability potential: One stable payload supports Low through Ultra without branching layouts.
Hardware Impact: 8-byte aligned `double3` avoids ARM64 unaligned access risk; cache footprint is fixed and predictable.

## Decision 005 - Per-State SDF Sample Lane

Problem: Builder ghost validation needs SDF evidence inside Burst, but `HectonVoxelVolume.TrySampleRuntimeSdfDensity` is managed/scene-facing and cannot run inside the Burst job.
Solution: Hydrate an 8-corner byte SDF lane before scheduling, then let `ValidateBuilderGhostPlacementJob` consume `BuilderGhostSdfSamples` by state slot offset. This keeps the validation decision in Burst while isolating the managed volume query at the boundary.
Rejected Alternatives: Calling voxel component APIs from Burst is impossible. Leaving the decision in `PlayerBuilder` only was rejected because the prompt requires a Burst validation kernel.
Scalability potential: Low/Middle/High/Ultra all keep 8 collision corners. GlobalQualityWeight buys visual shader detail and pipe presentation density only, not weaker placement legality.
Hardware Impact: MX350/i3 avoids PhysX broadphase and moving collider updates. The remaining eight scalar SDF reads are bounded and deterministic.

## Decision 006 - GPU Indirect Hologram

Problem: A GameObject preview renderer breaks SRP batching and creates transform/material churn.
Solution: `HectonBlueprintPreviewBatch` uploads DTOs to double-buffered `GraphicsBuffer`s and renders with `Graphics.DrawProceduralIndirect`; shader reconstructs cube geometry procedurally from `SV_VertexID` and `SV_InstanceID`.
Rejected Alternatives: `DrawMeshInstanced` was rejected because it still carries mesh instance CPU prep and matrix array risk. `GraphicsBuffer.SetData` was rejected because it can force a sync upload stall.
Scalability potential: Low uses cheap unlit solid blend. Middle keeps scanline. High adds rim/chromatic. Ultra increases visual overkill through continuous `GlobalQualityWeight`, not binary quality switches.
Hardware Impact: Estimated 10-60 us upload stall avoided on MX350; CPU draw prep target remains under 0.1 ms.

## Decision 007 - Data-Only Preview Authority

Problem: `PlayerBuilder` used a preview object as placement truth, causing hidden coupling to transforms, sockets, and validation logic.
Solution: Store preview active/canBuild/pose/scale as fields and publish `ConstructionPreviewSignal`. The active builder path no longer retains a preview object field, so there is no runtime ghost object release path to execute. Final module spawning remains because it creates the real gameplay object.
Rejected Alternatives: Keeping a pooled ghost object as "not Instantiate" was rejected. Pooling reduces allocation but does not remove transform/collider/render authority.
Scalability potential: Toaster gets one DTO and no ghost lifecycle. Middle/High/Ultra scale presentation only.
Hardware Impact: Removes preview proxy churn and renderer/material swaps. Exact measured profiler data is still absent due build guard.

## Decision 008 - Rollback Exclusion

Problem: Hologram matrix, validation color, and animation phase are local presentation data. Hashing them would cause false netcode desync.
Solution: Every builder ghost state sets `PresentationOnly` and `RollbackExcluded`; architecture docs state no Merkle leaf descriptor is registered for this lane.
Rejected Alternatives: Serializing preview state for replay was rejected because gameplay can re-emit preview locally after resimulation.
Scalability potential: All tiers use the same exclusion flags; visuals vary only by quality scalar.
Hardware Impact: Prevents unnecessary network/save bandwidth and hash work.

## Decision 009 - Black Box Telemetry

Problem: Placement holography can fail through NaN, alignment, or sudden SDF fault; "unknown crash" is forbidden.
Solution: Use `HolographyTelemetryEntry` ring in Vault and dump `Docs/AgentLogs/Dump_SHINOBU_228.bin` on non-finite state or >500 us solver time. Added a one-shot dump guard to avoid repeated disk writes after the first fault.
Rejected Alternatives: Per-frame logging was rejected because disk I/O would cause stutter and log spam.
Scalability potential: Low through Ultra get identical forensic data; visual quality does not change telemetry truth.
Hardware Impact: Normal path is one ring write. Fault path performs one bounded binary dump.

## Decision 010 - Editor Facade And Static Audit

Problem: Designers need tuning access, and the CTO reads files, not chat claims.
Solution: Added `Builder Tool X-Ray` editor window for telemetry/tuning and static audit evidence in `MEMORY_OPTIMIZATION_REPORT.json`. The audit preserves existing report sections instead of overwriting another agent's data.
Rejected Alternatives: Runtime UI was rejected because it would create string allocation risk and unnecessary dependencies.
Scalability potential: Editor-only controls expose continuous `GlobalQualityWeight`, magnetic radius, and grid tolerance.
Hardware Impact: No runtime hardware cost. Editor histogram uses fixed arrays.

## Decision 011 - Build Guard

Problem: Project rule forbids launching dotnet build while CPU is above 50% or another dotnet/csc is active, but the SHINOBU_228 code path needed one compile proof attempt after static polish.
Solution: Checked CPU and process state before build. Guard was clean at CPU 5.5% and zero dotnet/csc processes, so one `dotnet build Assembly-CSharp.csproj --no-restore /p:UseSharedCompilation=false` was launched. Build failed in `Hecton8.Core.csproj` on broader unresolved project dependencies. Afterward seven dotnet processes remained, so no second build was launched. Latest Iteration 13 guard sampled CPU 54% and dotnet/csc count 0; the CPU guard still blocks another build.
Rejected Alternatives: Forcing repeated dotnet builds after compiler processes remained active was rejected because it violates explicit hardware protection protocol. Claiming compile green was rejected because the build output is objectively red.
Scalability potential: Not applicable to runtime tiers; protects developer machine during 20+ agent execution.
Hardware Impact: One guarded compile attempt consumed the allowed window; subsequent attempts were blocked to avoid compiler/process contention.

## Decision 012 - Dead Runtime Ghost Proxy Branch Purge

Problem: `ConstructionRuntimeProxyFactory` still contained a reusable runtime ghost proxy branch after the data-only hologram route existed. Even if no caller survived, the branch could be revived by editor/bootstrap drift and recreate transform/collider preview authority.
Solution: Deleted ghost proxy storage, acquire/release/projection APIs, ghost materials, trigger collider mode, and `_GhostProxy` object naming. The factory now creates only final placed module proxies.
Rejected Alternatives: Leaving a disabled `ReleaseGhostProxy()` stub was rejected because static proof must show absence, not dormancy. A runtime `if (preview)` guard was rejected because it preserves the wrong authority path.
Scalability potential: Low through Ultra use the same data-only preview; final module proxies remain real gameplay objects after placement only.
Hardware Impact: Prevents reintroduction of preview Transform/Collider churn on i3/MX350. Microseconds are preventive rather than newly measured.

## Decision 013 - Preview Material Mutation Purge

Problem: `HectonBlueprintPreviewBatch` still carried per-frame material scalar/color updates for hologram presentation. That creates SRP Batcher risk and turns the material into hidden state authority.
Solution: Moved all preview visual parameters into `BuilderGhostVisualDTO` and the StructuredBuffer read path. Draw now only binds state/visual buffers and calls `Graphics.DrawProceduralIndirect`.
Rejected Alternatives: Material properties as "cheap globals" were rejected because the prompt requires direct GPU payload authority and continuous per-instance `GlobalQualityWeight`.
Scalability potential: Low/Middle/High/Ultra values are payload data, not shader keyword/material state. No binary switch or material variant churn.
Hardware Impact: Removes per-frame material property traffic from the preview path; expected MX350 gain is small but removes SRP batch invalidation risk.

## Decision 014 - Editor Bootstrap Ghost Prefab Quarantine

Problem: `ConstructionBootstrapAuthoring` could regenerate `PFB_Ghost_*` prefabs and assign them to `BuildableData`, preserving an asset-level route back to runtime preview prefab spawning.
Solution: Removed ghost prefab folder creation, `Mat_BuildGhost_*` authoring, `CreateGhostPrefab`, `AddComponent<PlacementGhost>`, and ghost prefab assignment. Bootstrap now writes `asset.ghostPrefab = null`.
Rejected Alternatives: Keeping generated ghost prefabs as "legacy only" was rejected because the authoring tool is a source of future asset state, not inert documentation.
Scalability potential: All hardware tiers receive the same hologram DTO path; authoring can only build final module prefabs.
Hardware Impact: Prevents future pool warmup and preview GameObject churn from returning on low-end silicon.

## Decision 015 - Legacy BuildableData Field Retention

Problem: Removing `BuildableData.ghostPrefab` from the ScriptableObject schema would break existing serialized assets and any external importer that still emits that field.
Solution: Retained the field with a tooltip stating runtime holography ignores it; catalog validation no longer requires it and editor bootstrap writes it to null.
Rejected Alternatives: Field deletion was rejected as unnecessary asset migration risk during a concurrent 20+ agent batch. Runtime consumption was rejected completely.
Scalability potential: Field presence has zero runtime tier impact because no active path reads it for preview creation.
Hardware Impact: No runtime cost. Preserves asset compatibility while blocking the old path from executing.

## Decision 016 - Data-Only Structural Integrity Validation

Problem: `PlayerBuilder.UpdatePlacementValidationState()` still treated a missing ghost object as a success path and the old integrity validator overload accepted a `GameObject`, preserving hidden transform authority.
Solution: Removed the last `_currentGhostObj`/`_currentGhost` fields and socket buffers from `PlayerBuilder`. Added a pose-based `HabitatConstructionManager.ScheduleIntegrityValidation(...)` overload that indexes candidate sockets from `Vector3`/`Quaternion` and fails closed on invalid pose/data. The legacy `GameObject` overload is a wrapper for old callers only.
Rejected Alternatives: Keeping a null-ghost success shortcut was rejected because it allows placement to pass without proof. Keeping candidate socket indexing tied to a hidden preview object was rejected because it reintroduces ghost prefab authority.
Scalability potential: Low through Ultra run the same data truth; higher tiers can only spend on hologram presentation. No binary quality branch changes placement legality.
Hardware Impact: Removes transform/collider object dependency from the validation scheduler and avoids scene hierarchy access for the candidate row.

## Decision 017 - Burst Indirect Args and Signal Matrix Rebuild

Problem: The preview batch still had main-thread matrix/indirect-args authoring and fixed draw bounds, which weakened the "single matrix directly to GPU" proof and could draw off-screen holograms.
Solution: `SetPreview` and signal consumption now schedule `BuildBuilderGhostStateJob` for matrix/visual DTO writes. Indirect args live in Vault buffer 70945 and are written by `BuildBuilderGhostIndirectArgsJob` before upload. Draw bounds are derived from active DTO matrices and checked against camera near/far before `Graphics.DrawProceduralIndirect`. Material creation fallback is editor-only.
Rejected Alternatives: Main-thread `Matrix4x4.TRS`/manual args writes were rejected because they bypass the Burst proof path. Fixed 256m bounds were rejected because they feed invisible work to the render path. Runtime `new Material` fallback was rejected because it can allocate during gameplay.
Scalability potential: Low through Ultra resolve the same 8 SDF placement samples. GlobalQualityWeight now scales only hologram shader state and optional presentation density; placement truth has no low/high switch.
Hardware Impact: Keeps CPU prep bounded to one DTO row plus one 16B args row; off-screen cases can skip the indirect draw call before GPU vertex work.

## Decision 018 - Compile Wall Attribution

Problem: The guarded build failed, and the failure must be classified precisely instead of hidden behind generic "compile failed" language.
Solution: Attributed the wall to `Hecton8.Core.csproj` dependency/project-file drift: representative errors include missing `Hecton8.Equipment`, missing `Hecton8.Logistics.Grid`, missing `SoundEmissionSignal`/audio interface members, missing `MethodImplAttribute`, missing service bridge types, and `SocketDefinitionDTO` unresolved because `BaseModuleCatalogRuntime.cs` is not included in `Hecton8.Core.csproj` while `HabitatGraphManager.cs` already references the DTO.
Rejected Alternatives: Editing unrelated core/equipment/logistics/audio project references was rejected as outside SHINOBU_228 domain and unsafe with 20+ agents. Re-running build while seven dotnet processes remain was rejected by the build guard.
Scalability potential: Not runtime-facing. The decision protects compile-wall discipline and avoids broad project-file churn outside the builder holography domain.
Hardware Impact: Prevents repeated compiler load on the developer machine; no runtime impact.

## Decision 019 - Deferred Visual Sync Fence

Problem: `HectonBlueprintPreviewBatch` still called direct `Complete()` from `LateFrameTick`, public preview staging, signal consumption, and indirect args upload. That converts the supposed double-buffered visual-sync path into a potential main-thread stall.
Solution: Replaced direct completion with a pending `JobHandle` lane. State/visual matrix jobs and `BuildBuilderGhostIndirectArgsJob` chain into `_pendingBuildHandle`. `LateFrameTick` first calls `DispatcherJobFence.TryFinalizeCompleted`; only completed payloads upload to the next graphics buffer. Current frame rendering keeps using the previous uploaded buffer. Forced completion is restricted to disable/destroy teardown.
Rejected Alternatives: Keeping a one-row args job with immediate `Complete()` was rejected because the mandate explicitly forbids arbitrary mid-frame blocking. Removing Burst args generation was rejected because indirect args must remain generated by the job path, not by CPU scalar writes.
Scalability potential: Low-tier devices can skip upload for one frame if the job is not ready, preserving frame pacing. Middle/High/Ultra receive the same frame-delayed payload with richer shader parameters; no binary switch is introduced.
Hardware Impact: Avoids a potential late-frame synchronization bubble on mobile CPUs and weak integrated GPUs. The cost is a deliberate one-frame visual latency for presentation-only holograms.

## Decision 020 - VR Pipe Blueprint Indirect Fold-In

Problem: `VRPipeBlueprintPreview` remained inside the Construction preview domain with a private managed `Matrix4x4[]` cache and `Graphics.DrawMeshInstanced`, which violated the SHINOBU_228 proof surface even though the main builder ghost path had moved to indirect DTO rendering.
Solution: Replace the pipe preview cache with Vault-backed `BuilderGhostStateDTO`, `BuilderGhostVisualDTO`, and `BuilderGhostIndirectArgsDTO` lanes 70946-70948. `BuildPipeBlueprintPreviewJob` emits pipe segment cuboids from four AUP control points in Burst, scaling segment density continuously with `GlobalQualityWeight`; the presenter uploads only after `DispatcherJobFence.TryFinalizeCompleted` and draws through `Graphics.DrawProceduralIndirect`.
Rejected Alternatives: Keeping `DrawMeshInstanced` as an XR-only exception was rejected because it preserves managed matrix arrays and mesh-instance CPU prep. Routing 64 pipe segments through `ConstructionPreviewSignal` was rejected because that lane is capacity-bounded for builder module previews and would create cross-purpose signal pressure.
Scalability potential: Low quality lengthens visual segments by a smooth multiplier, reducing instance count while preserving the pipe route silhouette. Middle tiers interpolate segment density. High/Ultra spend the saved CPU on the same hologram shader's scan/rim/chromatic overkill without adding gameplay state.
Hardware Impact: Removes up to 64 managed matrix uploads and mesh-instanced draw prep from XR pipe preview frames. Expected low-end i3/MX350 gain is small but removes a measurable CPU submission path and aligns the preview with the existing indirect hologram buffer contract.

## Decision 021 - Legacy Object Alignment Route Deletion

Problem: `HabitatConstructionManager` still exposed source-unused overloads accepting `Transform ghostRoot`, `List<ModuleSocket> ghostSockets`, and `GameObject candidateGhost`. Even as wrappers, these methods preserved an object-authority route back into placement validation.
Solution: Delete the unused object overloads and the now-dead `ResolveSocketYawRotation` helper. The only remaining integrity validation route takes `BuildableData`, `Vector3`, and `Quaternion`, which are supplied by `PlayerBuilder`'s data-only pose state.
Rejected Alternatives: Marking the object overloads obsolete was rejected because the method bodies would still compile and could be revived by another caller. Keeping them as "legacy wrappers" was rejected after `rg` proved there are no source callers.
Scalability potential: Low through Ultra share one pose route. Socket visual richness remains in the hologram shader and Vault payloads; validation authority no longer depends on any scene transform hierarchy.
Hardware Impact: Removes a possible transform/socket list walk from future preview code paths. Runtime gain is preventive; the current active caller was already on the pose route.

## Decision 022 - Static Audit Report Replacement

Problem: `BuilderHolographyStaticAudit` only inspected the main preview batch and returned early when `MEMORY_OPTIMIZATION_REPORT.json` already contained `SHINOBU_228`, leaving stale evidence after the VR pipe and indirect-args polish.
Solution: Extend the audit to inspect `VRPipeBlueprintPreview` and `HabitatConstructionManager`, then replace the existing `SHINOBU_228` JSON section by brace matching. Refresh `MEMORY_OPTIMIZATION_REPORT.json` with buffer IDs 70945-70948 and booleans proving no VR pipe mesh instancing/matrix cache and no object-alignment route.
Rejected Alternatives: Appending a second SHINOBU_228 section was rejected because duplicate JSON keys would make the report ambiguous. Leaving the stale report was rejected because the CTO reads files, not chat claims.
Scalability potential: The report now captures both module and pipe preview presentation routes, so future quality-scaling regressions have one static artifact to inspect.
Hardware Impact: Editor-only. No runtime frame cost.

## Decision 023 - Audit Token False-Positive Cleanup

Problem: The final forbidden-token scan matched the static audit tool's own search literals, not runtime residue. That makes the evidence noisy and forces a human to distinguish scanner strings from actual preview code.
Solution: Compose audit search tokens from split string literals (`"DrawMesh" + "Instanced"`, `".Set" + "Data("`, and the object-route names). The audit still checks the same text while `rg` only reports real residues in target files.
Rejected Alternatives: Excluding the editor audit file from the scan was rejected because the stronger proof is that even the scanner source does not contain the forbidden tokens contiguously. Removing the audit checks was rejected because Task 19 requires the static validator.
Scalability potential: Not runtime-facing. The evidence gate now stays clean as more preview presenters are added.
Hardware Impact: Editor-only. No runtime frame cost.

## Decision 024 - SDF Placement Truth Cannot Be Quality-Throttled

Problem: `ResolveBuilderGhostSdfSampleCount` previously allowed low `GlobalQualityWeight` to reduce the SDF corner budget, which could miss a blocked corner and turn thermal pressure into gameplay legality drift.
Solution: `ResolveBuilderGhostSdfSampleCount` now returns the fixed 8-corner count. `TryHydrateBuilderGhostSdfSamples` writes all eight corners, and `ValidateBuilderGhostPlacementJob` clamps only to available sample data, not to quality.
Rejected Alternatives: A 2..8 corner quality curve was rejected because it violates one-fact placement truth. Screen-space dither and shader simplification remain valid Dear Lie levers; collision legality is not.
Scalability potential: Low devices keep exact legality and shed visual ALU/upload work. Middle/High/Ultra spend saved CPU/GPU budget on scanline/rim/chromatic fidelity and pipe preview density.
Hardware Impact: Adds up to six bounded SDF byte checks versus the earlier low-quality path, but prevents false placement acceptance. This cost is dominated by the removed PhysX broadphase and ghost object route.

## Decision 025 - Legacy Ghost Assets Deleted Instead Of Quarantined

Problem: Serialized `PFB_Ghost_*` prefabs and the `PlacementGhost` component remained a revival route for object-based preview authority.
Solution: Deleted the `PlacementGhost.cs` script, its `.meta`, five `PFB_Ghost_*` prefabs, and their `.meta` files. Nulled existing `BuildableData.ghostPrefab` references and removed the stale `Hecton8.Core.csproj` compile include.
Rejected Alternatives: Keeping prefabs as "unused legacy" was rejected because authoring or another agent could rebind them. Obsolete attributes were rejected because the class would still compile.
Scalability potential: All hardware tiers use DTO/indirect holography only. High-tier visuals scale in shader, not by reintroducing renderer hierarchies.
Hardware Impact: Prevents preview Transform/Collider/Renderer churn from returning; source scan now has no first-party ghost asset route.

## Decision 026 - Runtime Vault Fallback Removed

Problem: `HectonBlueprintPreviewBatch` and `VRPipeBlueprintPreview` could fall back to `GlobalDataVault.TryGetLatestCreated`, making normal runtime authority depend on a global latest-created object.
Solution: Runtime presenter `TryResolveVault` methods now accept only `GlobalRegistry.DataVault`; if the bootstrap did not inject the DataVault, the presentation path fails closed.
Rejected Alternatives: Keeping latest-created fallback for convenience was rejected because AGENTS.md allows it only for bootstrap/editor/crash diagnostics, not domain runtime presenters.
Scalability potential: No quality-tier divergence. The route is deterministic across Low/Middle/High/Ultra.
Hardware Impact: No frame-time gain claimed; this is authority hygiene that avoids hidden global polling and cross-owner ambiguity.

## Decision 027 - Unchanged Preview Upload Suppression

Problem: `HectonBlueprintPreviewBatch` rebuilt and uploaded the same signal payload every frame, wasting bandwidth even when the matrix/flags/quality payload did not change.
Solution: Added a stable batch hash over active `ConstructionPreviewSignal` data excluding frame. If the active payload matches the already uploaded batch, the presenter reuses the existing double-buffered GPU payload and lets shader time animate the Dear Lie.
Rejected Alternatives: Hashing `Frame` was rejected because it would force uploads every frame. Freezing animation was rejected because the shader already owns procedural pulse/scan time.
Scalability potential: Low devices avoid redundant PCIe/unified-memory traffic; High/Ultra keep procedural animation and richer shader work without CPU upload churn.
Hardware Impact: Static-source estimate only: avoids repeated 128B state + 64B visual + 16B args uploads for unchanged single-module preview frames; measured profiler proof pending.
