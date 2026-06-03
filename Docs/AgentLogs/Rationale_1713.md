# Rationale 1713

Status: PENDING VERIFICATION

Problem: The batch protocol referenced a retired standalone domain map.
Solution: Use `Docs/PROJECT_ATLAS.md` for the 85-domain index and `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` for Echelon 2 World And Terrain source anchors.
Rejected Alternatives: Inventing a domain definition from prompt prose; reading archived batch files as active authority.
Scalability potential: Low/Middle/High/Ultra unaffected; this is governance proof, not runtime behavior.
Hardware Impact: 0 us runtime; prevents cross-domain edits that would create integration debt on i3/MX350 lanes.

Problem: Agent 1713 prompt contains 26 tasks and requires disk-backed anti-amnesia state before work.
Solution: Created `Status_1713.md` with five explicit loops and initialized this rationale file before code edits.
Rejected Alternatives: Chat-only state tracking; batch-wide prompt memory after extraction.
Scalability potential: Low/Middle/High/Ultra unaffected directly; reduces coordination failure with concurrent agents.
Hardware Impact: 0 us runtime; static workflow containment only.

Problem: RB-014 permits runtime brine pool roots, pool anchors, fog objects, disabled colliders, and mesh arrays from `HectonBrinePoolMeshGenerator.cs`.
Solution: Keep the serialized component alive for authored prefab/addressable references, but move generation, cleanup, mesh construction, and hazard mutation behind `UNITY_EDITOR`.
Rejected Alternatives: Keeping `Application.isPlaying` checks; they still compile allocation code into the player and keep the hot-load route alive.
Scalability potential: Low uses static authored brine prefabs only; Middle/High/Ultra can load richer baked meshes/fog volumes without changing runtime logic.
Hardware Impact: MX350/i3 path avoids runtime `new GameObject`, `AddComponent`, and `new Mesh`; expected biome-entry spike reduction is pool-count dependent, target 1000+ us avoided for 32 pools.

Problem: RB-009 clones materials in `ImpostorSystem` and then destroys cached material instances on unregister/destroy.
Solution: Replace cloned fallback/geology materials with serialized shared atlas material ownership and LateFrameTick-owned unmanaged atlas draw DTOs submitted by `RenderMeshIndirect`.
Rejected Alternatives: Per-renderer material clones, shader fallback materials, and pooled MeshRenderer atlas-index MPB writes; all recreate material or renderer-order debt.
Scalability potential: Low/Middle/High/Ultra all use one shared atlas material; fidelity scales by atlas content/offline bake resolution, not player material count.
Hardware Impact: 50 distant geology types stay at 1 material asset instead of 50 clones; shared-atlas active cards collapse to one indirect submit on i3/MX350.

Problem: Offline rock sculpting must avoid runtime gameplay dependencies while still supporting detailed abyssal erosion.
Solution: Author `RockSculptorEngine1713` as an EditorWindow and use bake-only SDF/noise/erosion, MeshData serialization, semantic vertex colors, LOD/proxy generation, and mandatory crash black-box dump only.
Rejected Alternatives: Adding runtime procedural terrain sculptor; violates offline asset permanence and creates same-frame mesh allocation risk.
Scalability potential: Low=500 erosion drops/lower budgets; Middle=mid drops and LOD budget; High/Ultra=more drops/detail baked into static meshes.
Hardware Impact: 0 us player runtime; editor bake pays the math cost once, then cheap devices stream static mesh/proxy assets.

Problem: Standard pooled `MeshRenderer` billboards cannot be trusted to map Unity instance IDs to the same order as `_activeImpostors`.
Solution: Stop using pooled MeshRenderer order for shared-atlas candidates; active shared-atlas impostors are emitted into a 64B draw DTO buffer and rendered through `SV_InstanceID`.
Rejected Alternatives: Relying only on `unity_InstanceID`; this can render wrong atlas tiles if Unity changes batching order or does not instance a draw. Recreating one material per tile or keeping MPB scalar indices was rejected as RB-009.
Scalability potential: Low/Middle/High/Ultra keep one atlas material; atlas record count scales by buffer capacity and authored atlas resolution, not material count.
Hardware Impact: 50 rock types avoid 50 material clones and move rect/tint/center/size payload to 64 bytes per active shared-atlas impostor; on MX350/i3 this removes per-renderer property writes.

Problem: The first erosion pass read and wrote the same SDF buffer from `IJobParallelFor`, creating nondeterministic neighbor reads.
Solution: Split erosion into `SourceSdf` and `OutputSdf`, adding `NativeDisableParallelForRestriction` only to source neighborhood reads and keeping output single-index writes.
Rejected Alternatives: In-place erosion under Burst; standard Unity safety would reject or serialize the wrong assumption, and cross-index races would corrupt deterministic geology.
Scalability potential: Low uses smaller resolution/drop equivalent; Middle/High/Ultra can increase resolution and erosion detail while remaining editor-only.
Hardware Impact: 0 player us; editor memory cost adds one float SDF buffer but removes race risk and failed bake retries on weak machines.

Problem: Surface extraction needed better normals/zero-crossing behavior and LOD1/LOD2 needed real collapse scoring instead of stride thinning.
Solution: Add Burst surface face classification, shift exposed faces toward the local SDF zero-crossing, compute vertex normals from finite-difference SDF gradients, and run editor-only quadric edge-collapse for LOD1/LOD2. Stride thinning remains only an emergency fallback.
Rejected Alternatives: Smooth sphere/noise mesh; axis normals only; keeping stride thinning as primary LOD reduction.
Scalability potential: Low gets cheap voxel-surface static meshes; Middle/High/Ultra use higher authored resolution, erosion passes, and the same QEM budget clamp.
Hardware Impact: 0 player us; editor extraction reduces full-grid interpreted scans and bakes vertex semantic channels so runtime does not pay AO/curvature.

Problem: Build verification was required but host policy forbids starting dotnet under high CPU or active compiler load.
Solution: Sampled CPU and compiler processes; CPU remained above 90% with active `dotnet:3100`, so Task 21 is recorded as `[BLOCKED BY HOST BUILD GUARD]` and no extra build was launched.
Rejected Alternatives: Violating the build guard to get a fake pass; launching parallel compiler under concurrent agents.
Scalability potential: Low/Middle/High/Ultra unaffected; this is host-safety compliance.
Hardware Impact: Prevents further CPU contention and preserves other agents' compiler work; compile proof remains pending.

Problem: The previous JSON report artifact conflicts with the current source-only proof directive and adds pointless editor I/O.
Solution: Remove `ReportPath`, `WriteReport`, SHA hashing, `StringBuilder`, whole-file byte reads, and the untracked JSON artifact. Keep status/rationale/log files and crash-only `Dump_1713.bin`.
Rejected Alternatives: Keeping obsolete JSON telemetry because an earlier batch task mentioned it.
Scalability potential: Low/Middle/High/Ultra unaffected; source validators and authored assets remain the proof route.
Hardware Impact: 0 player us; editor bake no longer writes the JSON report.

Problem: The editor rock sculptor introduced unmanaged bake structs whose implicit sizes were 36 bytes for `RockVertex` and 20 bytes for `SculptTelemetryEntry`, violating the 8-byte ARM64 layout rule.
Solution: Added explicit padding fields and cold `UnsafeUtility.SizeOf<T>()` validation gates; `RockVertex` is expected at 40 bytes and `SculptTelemetryEntry` at 24 bytes.
Rejected Alternatives: Trusting implicit struct packing; changing MeshData streams to a single interleaved buffer just to hide the alignment defect.
Scalability potential: Low/Middle/High/Ultra use the same baked asset path; the validation blocks corrupt authoring output before it can enter runtime streaming.
Hardware Impact: 0 player us; avoids misaligned editor-generated DTOs feeding later native upload/telemetry paths on i3/MX350 and ARM64.

Problem: The geology impostor atlas DTO needed proof that the LateFrameTick UV/tint payload stays 32 bytes and 8-byte aligned.
Solution: Added a cold startup `UnsafeUtility.SizeOf<ImpostorAtlasInstanceData>()` gate before registry registration.
Rejected Alternatives: Assuming `[StructLayout(Size = 32)]` would be enough without a runtime assertion.
Scalability potential: One shared atlas material remains valid from weak to ultra devices; fidelity scales by authored atlas resolution and active count, not material clones.
Hardware Impact: 0 steady-state us; cold failure path prevents malformed buffer stride from corrupting shader reads.

Problem: LOD1/LOD2 generation still used deterministic stride thinning, which met triangle budgets but did not satisfy the QEM collapse requirement or preserve fractures intelligently.
Solution: Replaced the primary reducer with editor-only quadric edge-collapse: per-triangle plane quadrics, sorted edge candidates, normal/fracture cost penalties, compacted vertex output, and post-emission topology validation. Stride thinning remains only as an emergency fallback if QEM produces no valid mesh.
Rejected Alternatives: Keeping stride thinning and relabeling it as QEM; adding a new reducer class or runtime simplification system.
Scalability potential: Low bakes cheaper LODs with preserved silhouette anchors; Middle/High/Ultra get richer LOD0 while LOD1/LOD2 stay bounded and static.
Hardware Impact: 0 player us; MX350/i3 runtime consumes pre-decimated meshes instead of paying extra triangles or runtime simplification.

Problem: Shared-atlas geology impostors still depended on pooled MeshRenderers and one reusable MaterialPropertyBlock to pass atlas indices, leaving renderer-order correctness and SRP Batcher risk.
Solution: Moved shared-atlas active impostors to a single `RenderMeshIndirect` submission in `LateFrameTick`; each active draw record is a 64B unmanaged DTO containing center, size, atlas rect, and tint. The shader resolves records by `SV_InstanceID`.
Rejected Alternatives: Keeping per-renderer MPB; relying on `unity_InstanceID` order for pooled renderers; creating cloned materials per atlas tile.
Scalability potential: Low uses one cheap unlit/fog atlas card draw; Middle/High/Ultra can increase active atlas count and atlas resolution without material proliferation.
Hardware Impact: Removes per-active renderer property writes and GameObject billboard spawn for shared-atlas cases; MX350/i3 path pays one buffer upload and one indirect draw for active geology cards.

Problem: Static syntax proof was requested without spamming compilation, but host guard reported CPU 79.05% with active `dotnet:3100`.
Solution: Build was not launched. Roslyn PowerShell load attempts were tried against SDK and Unity plugin assemblies and failed to expose `CSharpSyntaxTree`, so no AST pass is claimed from those attempts.
Rejected Alternatives: Starting `dotnet build` or compiling an analyzer under active compiler load; reporting a fake AST result after loader failure.
Scalability potential: Low/Middle/High/Ultra unaffected; this preserves host compile bandwidth for concurrent agents.
Hardware Impact: 0 runtime us; avoids additional CPU contention while keeping source-level scans honest.

Problem: Shared-atlas geology material could still be resolved when indirect drawing was disabled or no indirect mesh was available, causing a pooled renderer fallback with no UV/tint payload after MPB removal.
Solution: Make `ResolveSharedImpostorMaterial` fail closed for distant geology unless `_authoredImpostorAtlasMaterial`, `_enableIndirectAtlasDraw`, and a cold-resolved indirect billboard mesh are all valid.
Rejected Alternatives: Reintroducing MPB, cloning a per-tile material, or letting the wrong atlas tile render.
Scalability potential: Low/Middle/High/Ultra keep one atlas route; invalid authoring state refuses registration instead of corrupting visuals.
Hardware Impact: Prevents wrong visuals without adding hot-path work; 0 player material clones.

Problem: Missing authored atlas entries would fall back to `DefaultAtlasRect`, allowing distant geology to render the whole atlas without a clear failure.
Solution: `TryResolveAuthoredAtlasEntry` now resolves rect and tint in one cold pass; shared-atlas registration fails if no source-material or albedo-texture entry exists. Default zero tint is treated as white to avoid black cards from uninitialized entries.
Rejected Alternatives: Accepting `DefaultAtlasRect` for shared-atlas geology or performing two separate entry scans for rect and tint.
Scalability potential: Low/Middle/High/Ultra all get deterministic atlas selection; content scale is atlas-entry driven, not material driven.
Hardware Impact: 0 hot us; prevents bad distant visuals and removes duplicate cold search work.

Problem: The atlas material was only forced to `enableInstancing=true` in editor `OnValidate`.
Solution: Add `EnsureAuthoredAtlasMaterialCold` and call it from `OnEnable`; player builds no longer depend on editor validation having run.
Rejected Alternatives: Trusting importer/inspector state or setting material flags every frame.
Scalability potential: Same one-material route from weak to ultra devices.
Hardware Impact: 0 hot us; one cold material flag check.

Problem: `UploadAndSubmitActiveAtlasDraws` used `_textureCache.TryGetValue` per active shared-atlas card in visual sync.
Solution: Copy `ImpostorAtlasInstanceData` into `ImpostorInstance` during cold registration and read it linearly in `LateFrameTick`.
Rejected Alternatives: Keeping dictionary probes in the visual sync phase.
Scalability potential: Sparse and dense impostor sets both avoid per-card hash lookup during upload.
Hardware Impact: Removes one dictionary probe per active shared-atlas card per submitted frame.

Problem: The procedural atlas shader must address the same indirect instance-id ambiguity as the first-party wreck/vegetation indirect shaders.
Solution: Keep raw `SV_InstanceID` as fallback and override with `unity_InstanceID` under `UNITY_ANY_INSTANCING_ENABLED`, matching the local indirect shader pattern.
Rejected Alternatives: Removing raw `SV_InstanceID`; relying only on macro state; stripping Unity instancing macros.
Scalability potential: Same shader path works for weak to ultra devices; atlas count scales through draw args and DTO buffer.
Hardware Impact: Avoids shader compile risk with no runtime cost.

Problem: The indirect DTO upload unlocked the reserved buffer window instead of the number of records actually written.
Solution: Call `UnlockBufferAfterWrite<ImpostorAtlasDrawInstanceData>(writeCount)`; manual upload budget remains conservative because the active count is only known after filtering.
Rejected Alternatives: Uploading unwritten tail records or adding a second hot counting pass.
Scalability potential: Lower active counts upload less data; high-tier active counts still use the same bounded capacity.
Hardware Impact: Reduces wasted GPU upload bytes when active shared-atlas impostors are sparse.

Problem: Authored atlas entry lookup used `ReferenceEquals` against `UnityEngine.Object` assets.
Solution: Use Unity object equality for material/texture match in the cold atlas entry scan, so asset wrapper identity does not reject a valid entry.
Rejected Alternatives: Comparing string paths, instance IDs, or adding another atlas lookup table.
Scalability potential: Low/Middle/High/Ultra all preserve deterministic atlas selection without runtime fallback materials.
Hardware Impact: 0 hot us; prevents false registration failure without adding per-frame work.

Problem: QEM and stride fallback LOD meshes were validated from emitted indices but saved with LOD0 bounds.
Solution: Recompute and pass decimated bounds from the actual emitted index stream before `CreateMesh`.
Rejected Alternatives: Reusing broad LOD0 bounds for every reduced mesh.
Scalability potential: Low/Middle gets tighter static culling from cheaper LODs; High/Ultra keep rich LOD0 while reduced meshes cull by their own geometry.
Hardware Impact: 0 player CPU change; can reduce overdraw/culling conservatism on MX350/i3 scenes with dense rock fields.

Problem: Build guard changed from CPU-bound to active-compiler-bound during the final pass.
Solution: Still did not launch `dotnet build`; latest sample was CPU 48.79% with active `dotnet:3100`.
Rejected Alternatives: Starting a second build while another dotnet process is active.
Scalability potential: Low/Middle/High/Ultra unaffected; this is host safety.
Hardware Impact: Avoids compiler contention; compile proof remains pending.

Problem: `RockSculptorEngine1713` was saving generated geology under a separate `Assets/Prefabs/Environment/Geology` route while the first-party GeologyForge already owns baked geology folders and audits.
Solution: Made `GeologyForgeConstants` public and routed RockSculptor mesh/prefab saves through those constants.
Rejected Alternatives: Keeping two geology output roots or duplicating the folder strings locally.
Scalability potential: Low/Middle/High/Ultra assets now enter the same baked geometry area instead of fragmenting content ownership.
Hardware Impact: 0 player us; reduces asset-routing drift and keeps static geometry discoverable for existing editor validation.

Problem: Rock UV0 used raw world XZ tiling, which is weak for decal/impostor atlas packing and can bleed at mip edges.
Solution: Added normal-selected triplanar UV0 projection with a fixed 8px-at-1024 normalized edge guard.
Rejected Alternatives: Leaving stretched XZ UVs or adding a full MaxRects packer duplicate inside the 1713 window.
Scalability potential: Low uses the same compact UV channel; Middle/High/Ultra can pack richer impostor/detail atlases without dark-edge bleed from generated UVs.
Hardware Impact: 0 player CPU; improves baked texture stability on MX350/i3 without runtime shader cost.

Problem: Read-only SDF inputs in editor jobs carried `NativeDisableParallelForRestriction` without a write-alias requirement.
Solution: Removed the unsafe restriction attributes from classification and erosion SDF reads.
Rejected Alternatives: Adding long safety comments to justify a bypass that is not needed.
Scalability potential: Same offline path across tiers; less unsafe surface area in Burst authoring jobs.
Hardware Impact: 0 player us; editor-only safety cleanup.

Problem: The final compile pass remains unsafe to launch on the host.
Solution: Build was not launched after guard reported CPU 79% with active `dotnet:3100`.
Rejected Alternatives: Starting another `dotnet build` under compiler/runtime load.
Scalability potential: Low/Middle/High/Ultra unaffected; host stability preserved for parallel agents.
Hardware Impact: Avoids compiler contention; compile proof remains static-only this pass.

Problem: Routing RockSculptor assets directly through the top-level GeologyForge output folders would collide with the existing manifest/self-audit contract.
Solution: Keep `GeologyForgeConstants` as the one route owner, but write 1713 rocks under `RockSculptor1713` subfolders beneath the GeologyForge mesh/prefab roots.
Rejected Alternatives: A parallel `Assets/Prefabs` route, duplicated path constants, or top-level GeologyForge writes that would be flagged as unmanifested generated assets.
Scalability potential: Low/Middle/High/Ultra all use one baked geometry ownership area; generated rock sets can scale without corrupting the manifest lane.
Hardware Impact: 0 player us; prevents editor validation churn and wrong asset ownership during content bakes on shared machines.

Problem: QEM and telemetry DTOs still depended on implicit CLR packing even after the main vertex/telemetry size gates.
Solution: Pin `RockVertex`, `SculptTelemetryEntry`, `QuadricError`, and `EdgeCollapseCandidate` with explicit `StructLayout` sizes and retain cold `UnsafeUtility.SizeOf<T>()` gates.
Rejected Alternatives: Trusting field-order packing or adding a separate DTO helper layer.
Scalability potential: Low/Middle/High/Ultra share the same authoring DTO contract; only authored resolution and LOD budgets scale.
Hardware Impact: 0 player us; protects editor native memory/bake telemetry alignment on ARM64 and low-end i3/MX350 lanes.

Problem: Repository hygiene scan found two orphan `.meta` files in Unity package-cache directories.
Solution: Removed only those two validated workspace-local orphan files and repeated the recursive scan to `OrphanMetaCount=0`.
Rejected Alternatives: Ignoring cache-area orphans or deleting broad folders.
Scalability potential: Low/Middle/High/Ultra unaffected; import database hygiene only.
Hardware Impact: 0 player us; reduces Unity import noise without touching source assets.

Problem: Build verification is still blocked by the host guard.
Solution: Latest guard sampled CPU 100% with active `dotnet:3100` and `dotnet:30140`; no `dotnet build` was launched.
Rejected Alternatives: Starting a compiler while CPU is above 50% and another dotnet process is alive.
Scalability potential: Low/Middle/High/Ultra unaffected; this preserves shared-agent host stability.
Hardware Impact: Avoids compiler contention; compile proof remains pending and static-only this pass.

Problem: Destroyed impostor originals could trigger `List.RemoveAt(i)` inside the hot tick slice and leave `_textureCache`/candidate records behind.
Solution: Use O(1) swap-remove for hot null cleanup and remove candidate/texture records by the cached instance data.
Rejected Alternatives: Keeping ordered list removal; adding a second ownership map.
Scalability potential: Low/Middle/High/Ultra all avoid unload-spike memmove when geology chunks despawn.
Hardware Impact: Reduces destroyed-candidate cleanup from tail-shift O(n) to O(1) in the tick slice.

Problem: Shared atlas material state could stay procedurally enabled after disable or args-upload failure, and empty frames could keep writing zero state.
Solution: Clear atlas material state on disable, fail closed when indirect args upload fails, and track whether material state is already clean before writing `SetInt` clears.
Rejected Alternatives: Trusting no one else uses the shared material; clearing material properties every visual-sync frame.
Scalability potential: Low devices avoid useless material property writes; high/ultra keep the same indirect route without stale state.
Hardware Impact: 0 allocation; removes redundant empty-frame material writes and stale draw flags.

Problem: Runtime brine authored reference detection accepted an empty addressable reference as valid.
Solution: Require `AssetReferenceGameObject.RuntimeKeyIsValid()` when no direct authored prefab is assigned.
Rejected Alternatives: Null-only reference check or runtime generation fallback.
Scalability potential: Low/Middle/High/Ultra all fail closed to authored static content only.
Hardware Impact: 0 hot us; avoids runtime path ambiguity.

Problem: RockSculptor asset IDs were raw editor text and could inject invalid path characters into generated asset names.
Solution: Sanitize to a 64-character ASCII asset id before `AssetDatabase` path construction.
Rejected Alternatives: Trusting editor input or adding a separate naming utility.
Scalability potential: Low/Middle/High/Ultra authoring batches keep stable generated paths.
Hardware Impact: 0 player us; prevents failed bake/import churn.

Problem: The shared-atlas impostor route could still try `RenderMeshIndirect` on an unsupported graphics path or reuse an invalidated indirect args buffer.
Solution: Cache `SystemInfo.supportsInstancing && SystemInfo.supportsComputeShaders` in `OnEnable`, require it during shared material resolution/upload/args creation, reject zero-index billboard meshes, and recreate invalid args buffers before upload.
Rejected Alternatives: Relying on `RenderMeshIndirect` to fail internally; adding a managed fallback material route that would reopen RB-009.
Scalability potential: Low devices fail closed to authored non-indirect content; Middle/High/Ultra use the one-material indirect atlas route when graphics support exists.
Hardware Impact: 0 allocation in `LateFrameTick`; prevents invalid indirect submissions on weak or unusual devices.

Problem: `RemoveImpostorInstance` still used ordered list removal while hot destroyed-object cleanup had already moved to swap-remove.
Solution: Route unregister removal through `RemoveImpostorAtSwap` and include the same cold hardware-support bit in `CanUseIndirectAtlasDraw`.
Rejected Alternatives: Keeping an O(n) tail shift for chunk unloads; relying on material-resolution support checks while activation had a looser predicate.
Scalability potential: Low/Middle/High/Ultra all keep dense impostor arrays during unload churn without preserving meaningless order.
Hardware Impact: Reduces unregister removal from tail-shift O(n) to swap-remove O(1); 0 hot allocation.

Problem: Rock collision proxy creation rebuilt the same 36 box indices every bake and normalized a vector without a zero-length guard.
Solution: Move the 36 indices to a static readonly template and use `SafeNormal` for proxy normals.
Rejected Alternatives: Leaving editor batch bakes with repeated immutable array allocation; trusting bounds validation as the only NaN guard.
Scalability potential: Low/Middle/High/Ultra static rock batches bake through the same proxy route; runtime collision stays a convex 12-triangle box.
Hardware Impact: 0 player us; editor batch bake removes one managed array allocation per generated rock and hardens NaN rejection.

Problem: Procedural atlas material cleanup used only the current serialized atlas material reference, so a swapped or nulled material could leave the previously submitted shared material with procedural draw state enabled.
Solution: Track `_activeAtlasDrawMaterial` at submit time and clear that exact material in `ClearAtlasDrawMaterialState`; release paths reset the owner reference after clearing.
Rejected Alternatives: Clearing only `_authoredImpostorAtlasMaterial`; writing material zero-state every frame; adding a second material manager.
Scalability potential: Low devices avoid stale atlas state after disabled/failed submissions; Middle/High/Ultra keep the same one-draw atlas route without material drift.
Hardware Impact: 0 hot allocation; removes a stale shared-material failure mode without adding per-frame lookup.

Problem: Build verification remains unsafe on the shared host.
Solution: Latest guard sampled CPU 100% with active `dotnet:3100` and `dotnet:31328`; no `dotnet build` was launched.
Rejected Alternatives: Violating the >50% CPU / active compiler guard to manufacture a compile result.
Scalability potential: Low/Middle/High/Ultra unaffected; host contention is contained.
Hardware Impact: Avoids compiler contention; compile proof remains pending and static-only this pass.

Problem: Brine resident-sector refresh repeatedly called `SyncBrineHazardRegistration`, which rebuilt the same toxic mud cell and could leave `HazardRegistered=1` if `HazardZoneManager.RegisterZone` failed during an update.
Solution: Make brine hazard sync idempotent for the toxic mud grid: same zone plus live grid cell skips `RegisterCell`; changed zone ids unregister first; failed hazard registration clears existing zone flags and unregisters the manager volume.
Rejected Alternatives: Treating `HazardZoneManager.RegisterZone` as enough because it updates volumes; it does not avoid toxic grid bounds rebuild, and failure left brine state stale.
Scalability potential: Low devices avoid repeated brine grid rebuild work during sector refresh; Middle/High/Ultra retain hazard-manager update as a repair path after service reset.
Hardware Impact: 0 managed allocation; removes duplicate `HectonBrineToxicMudGrid.RegisterCell`/bounds rebuild work for already-registered brine sectors.

Problem: `LODSystemManager.RegisterLODGroup` wrote to lists/hashset created with the old 500-slot capacity, so dense offline geology fields could trigger managed collection growth during LOD registration before the system merely warned.
Solution: Prewarm the LOD group lists/hashset to 2048 slots, clamp the inspector cap through `ResolveRegisteredLODGroupCapacity`, and reject overflow before any `Add`.
Rejected Alternatives: Keeping the post-add development warning; adding a second LOD registry; allowing automatic List/HashSet growth because registration is not every-frame.
Scalability potential: Low keeps the default 500 authoring cap; Middle/High/Ultra can raise the cap up to 2048 without runtime collection resize.
Hardware Impact: Prevents resize/allocation spikes during geology chunk registration on i3/MX350; steady-state Tick/LateFrameTick remains unchanged at 64 processed groups per frame.

Problem: Shared-atlas impostor upload failure rendered the previous frame's buffer, which can keep stale distant geology cards visible after source objects move, deactivate, or fail the current upload pass.
Solution: On invalid atlas write buffer or rejected manual upload budget, clear procedural atlas state instead of calling the previous-buffer submit path; remove the fallback method entirely.
Rejected Alternatives: Keeping visual continuity at the cost of stale impostor truth; adding a managed dirty-list to validate previous-buffer contents.
Scalability potential: Low devices under upload pressure fail closed for a frame instead of drawing wrong geology; Middle/High/Ultra still use the normal one-draw path when upload succeeds.
Hardware Impact: 0 hot allocation; eliminates stale draw submission and one dead branch from the atlas upload path.

Problem: `LODSystemManager.ApplyLODTransitions` read `LODGroup.fadeMode` inside the visual-sync slice to decide whether a write was needed.
Solution: Add a pre-sized byte lane parallel to the registered LOD group arrays, seed it as `Unknown`, preserve it through swap-remove cleanup, and compare desired state against that byte before writing Unity properties.
Rejected Alternatives: Keeping Unity property reads in the hot slice; seeding from `LODGroup.fadeMode` at registration; adding a separate dictionary keyed by `LODGroup`; querying components again.
Scalability potential: Low devices process fewer LOD groups without Unity property read overhead; Middle/High/Ultra can increase visual transition density while keeping phase-owned presentation writes deterministic.
Hardware Impact: Removes one Unity `fadeMode` property read per processed LOD group in the scheduled transition batch; first processed pass claims presentation ownership with one bounded write; 0 managed allocation and no new lookup route.

Problem: Destroyed Unity objects can compare as null through Unity equality, so using the destroyed `OriginalObject` to clear `_registeredCandidates` can leave the managed owner in the set.
Solution: Remove `_registeredCandidates` with `OriginalObject` under a `ReferenceEquals` guard during destroyed-original cleanup before O(1) swap-remove.
Rejected Alternatives: Removing by Unity null equality; adding a duplicate candidate-owner field; adding a second candidate map; scanning `_registeredCandidates`.
Scalability potential: Low/Middle/High/Ultra unload dense geology chunks without candidate leaks that block future impostor registration.
Hardware Impact: Prevents stale candidate-set growth during destruction spikes; cleanup remains O(1) and adds no hot allocation.

Problem: Resource pool warmup revalidated every template prefab through `ValidateRuntimeNodePrefabCold()`, duplicating prefab `GetComponent<ResourceNode>` work after `ValidateAuthoredRuntimePrefabsCold()` already owned validation.
Solution: Let `EnsureRuntimePool()` consume the cached `ResourceNodeTemplate.RuntimeNodePrefab` accessor and remove the repeated validation call inside the warmup loop.
Rejected Alternatives: Leaving repeated validation in service hotswap/warmup; adding a new prefab cache layer; moving prefab validation into the pool service.
Scalability potential: Low devices avoid repeated cold prefab component scans during object-pool replacement; Middle/High/Ultra retain the same authored prefab route and warm larger pools.
Hardware Impact: Removes one prefab validation scan per resource template per warmup pass; no change to steady-state spawn truth.

Problem: Compile verification is still blocked by explicit host-safety policy.
Solution: Sampled CPU and compiler processes; CPU was 100% with active dotnet processes `3100,31076`, so no build was launched.
Rejected Alternatives: Starting another compiler under 100% CPU and active dotnet load; claiming compile proof without a build.
Scalability potential: Low/Middle/High/Ultra unaffected; preserves shared workstation stability for concurrent agents.
Hardware Impact: Avoids build contention; source-level verification remains the only completed proof this pass.

Problem: `ImpostorSystem` prewarmed only 512 managed collection slots while the atlas upload path supports 2048 draw records, so late chunk registration/activation could force Dictionary/List/HashSet resize GC before the upload cap was reached.
Solution: Set `InitialImpostorCapacity` to `MaxAtlasInstanceUploadCapacity`, reject new registrations before candidate/active/texture-cache growth, track pooled billboards only when the dictionary has the key or free capacity, cache renderers only into existing/free slots, and guard `_activeImpostors.Add` with a count check plus `TryGetValue`.
Rejected Alternatives: Adding a second capacity manager; letting collection growth happen because registration is "cold"; lowering atlas upload capacity back to 512 and wasting GPU batch capacity.
Scalability potential: Low devices fail closed when dense far geology exceeds the prewarmed budget; Middle/High/Ultra can author up to 2048 shared-atlas records without material clones or managed resize spikes.
Hardware Impact: Prevents hidden managed resize allocation during distant geology registration/activation after the old 512-slot boundary; no steady-state Tick/LateFrameTick allocation route added.

Problem: Compile verification remains blocked by host policy.
Solution: Sampled CPU at 71%; no build launched under the >50% guard.
Rejected Alternatives: Running `dotnet build` anyway to manufacture a compile result.
Scalability potential: Low/Middle/High/Ultra unaffected; host safety only.
Hardware Impact: Avoids additional compiler contention; source-level scans and `git diff --check` are the completed verification for this pass.

Problem: A close-range active impostor could lose its billboard object, repair visibility locally, but fail to persist the inactive state back into `_activeImpostors`.
Solution: Write the repaired inactive `ImpostorInstance` back in the missing-billboard branch for non-indirect active instances.
Rejected Alternatives: Leaving the repeated repair branch; removing the instance and risking candidate churn.
Scalability potential: Low devices avoid repeat stale repair checks; Middle/High/Ultra retain the same indirect atlas and close-object fallback behavior.
Hardware Impact: Removes redundant tick-branch work after a pooled billboard/object mismatch; 0 allocation.

Problem: `HectonBrineToxicMudGrid.ToxicMudCell` used explicit 56-byte layout but had no cold `UnsafeUtility.SizeOf<T>()` gate.
Solution: Add a static cold layout gate and make registration/read accessors fail closed if observed stride is not 56 bytes or is not 8-byte aligned.
Rejected Alternatives: Logging every mismatch; adding a separate validator class; trusting CLR/Unity packing.
Scalability potential: Low/Middle/High/Ultra all share one brine grid DTO contract; hazard truth does not mutate if unmanaged layout drifts.
Hardware Impact: 0 managed allocation; one static bool branch on grid entry points, with no hot scene lookup or DataVault access.

Problem: Compile verification is still blocked by host CPU policy after the latest code patch.
Solution: Sampled CPU at 77% and compiler process list as empty; no build launched because the >50% guard is sufficient.
Rejected Alternatives: Starting `dotnet build` under high host load; claiming compile proof without a build.
Scalability potential: Low/Middle/High/Ultra unaffected; host safety only.
Hardware Impact: Avoids compiler contention; static scans and diff hygiene are the completed verification artifacts.

Problem: The geology impostor shader retained a legacy atlas-instance `StructuredBuffer` path after CPU-side per-renderer atlas payloads were removed.
Solution: Delete `_HectonImpostorAtlasInstances`, atlas instance count/index properties, and the resolver branch; CPU now only binds the draw-instance buffer used by `RenderMeshIndirect`.
Rejected Alternatives: Keeping a dead buffer binding; reintroducing per-renderer MPB or material clones; adding a keyword switch in the hot path.
Scalability potential: Low devices avoid missing-buffer risk and material state noise; Middle/High/Ultra keep the one shared-material indirect draw path.
Hardware Impact: Removes one unused GPU resource contract and two material int writes from submit/clear paths.

Problem: The shared atlas material could see the shader before the first non-empty indirect submit, with `_HectonImpostorDrawInstances` not yet bound.
Solution: Bind the prewarmed draw buffer with zero draw count during cold atlas resource prewarm.
Rejected Alternatives: Setting the buffer every empty `LateFrameTick`; relying on first active draw; allocating a separate dummy buffer.
Scalability potential: Low/Middle/High/Ultra share a stable material resource state before active geology cards exist.
Hardware Impact: 0 hot-frame writes; one cold `SetBuffer` during prewarm.

Problem: `#if UNITY_EDITOR` still allows brine mesh/object generation during Editor Play Mode, which violates the Edit Mode-only requirement.
Solution: Add an `Application.isPlaying` fail-closed gate at the start of `BuildBrinePools`.
Rejected Alternatives: Trusting editor harness discipline; moving the whole component into an editor assembly and breaking serialized runtime authored references.
Scalability potential: Low/Middle/High/Ultra runtime scenes consume authored brine content only; offline bake remains available in Edit Mode.
Hardware Impact: Prevents editor-play runtime root/mesh/fog/collider/hazard creation; 0 player allocation path.

Problem: Compile verification remains blocked by host CPU policy after shader/brine guard changes.
Solution: Sampled CPU at 96% and compiler process list as empty; no build launched because CPU exceeds the 50% guard.
Rejected Alternatives: Starting `dotnet build` under high CPU; claiming compile proof without a build.
Scalability potential: Low/Middle/High/Ultra unaffected; host safety only.
Hardware Impact: Avoids compiler contention; static scans and diff hygiene are the completed verification artifacts.

Problem: `ImpostorSystem.TryResolvePrimaryMaterial` used `GetComponentsInChildren(..., List<Renderer>)`, which can grow the scratch list during runtime LOD/chunk registration if a candidate has more renderers than the prewarmed capacity.
Solution: Replace the list scan with a bounded 256-slot transform DFS stack using `Transform.GetChild` and `TryGetComponent<Renderer>`, clearing scratch references and failing closed on overflow.
Rejected Alternatives: Increasing the list capacity and hoping hierarchy size stays below it; adding a second renderer registry; keeping Unity's list-fill API and accepting hidden resize allocation.
Scalability potential: Low devices fail closed on oversized candidate roots instead of allocating; Middle/High/Ultra can author normal geology roots through the same fixed traversal and rely on indirect atlas rendering after registration.
Hardware Impact: Prevents managed list resize spikes during impostor candidate registration; no steady-state `Tick`/`LateFrameTick` cost.

Problem: Compile verification remains blocked by explicit host-safety policy.
Solution: Sampled CPU at 91% and compiler process list as empty; no build launched because CPU remains above the 50% project guard.
Rejected Alternatives: Starting `dotnet build` while host load violates the local rule.
Scalability potential: Low/Middle/High/Ultra unaffected; host safety only.
Hardware Impact: Avoids compiler contention; static scans and `git diff --check` remain the completed verification artifacts.

Problem: `UploadAndSubmitActiveAtlasDraws` could call buffer capacity allocation methods from `LateFrameTick` if a prewarmed `GraphicsBuffer` was lost or invalidated.
Solution: Split cold allocation from hot readiness: visual sync now uses `HasAtlasDrawInstanceBufferCapacity` and `HasAtlasIndirectArgsBufferReady`, clearing procedural material state on failure instead of allocating.
Rejected Alternatives: Recreating `GraphicsBuffer` resources in `LateFrameTick`; keeping stale previous-frame draw submission; adding a managed recovery queue.
Scalability potential: Low devices fail closed for a frame after resource loss; Middle/High/Ultra keep the same one-draw atlas route when cold prewarm resources are valid.
Hardware Impact: Prevents GPU buffer allocation/release spikes in visual sync; static saving is allocation risk removal, measured us unavailable under build/profiler guard.

Problem: Compile verification remains blocked by explicit host-safety policy after the atlas allocation closure.
Solution: Sampled CPU at 77% with active `dotnet:32588`; no build launched.
Rejected Alternatives: Starting `dotnet build` under >50% CPU and active dotnet process.
Scalability potential: Low/Middle/High/Ultra unaffected; host safety only.
Hardware Impact: Avoids compiler contention; source scans and `git diff --check` remain the completed verification artifacts.

Problem: `RockSculptorEngine1713` black-box dump wrote only 20 bytes per telemetry entry while `SculptTelemetryEntry` is explicitly validated as a 24-byte ARM64-aligned DTO.
Solution: Write the padding lane as part of each dumped telemetry record so the binary artifact preserves the validated stride.
Rejected Alternatives: Letting dump readers infer missing padding; replacing the binary dump with JSON or report I/O.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; crash analysis receives byte-stable records.
Hardware Impact: 0 player us; editor crash dump records remain fixed-size and alignment-consistent.

Problem: `LODSystemManager` classified distant geology impostor candidates with `LODGroup.GetLODs()`, which creates managed arrays during runtime LOD group registration/chunk churn.
Solution: Replace the LOD array copy with a fixed 256-slot transform DFS that inspects child renderers and material/shader names; overflow fails closed.
Rejected Alternatives: Caching `LOD[]` arrays per group; adding a new geology classifier service; keeping Unity-managed array copies because registration is "cold".
Scalability potential: Low devices avoid registration allocations in dense rock fields; Middle/High/Ultra can register larger geology roots within the fixed traversal budget.
Hardware Impact: Prevents managed array allocation during geology LOD registration; no change to steady-state Tick/LateFrameTick.

Problem: Compile verification remains blocked by explicit host-safety policy after the LOD registration closure.
Solution: Sampled CPU at 69% with active `dotnet:32588`; no build launched.
Rejected Alternatives: Starting `dotnet build` under >50% CPU and active dotnet process.
Scalability potential: Low/Middle/High/Ultra unaffected; host safety only.
Hardware Impact: Avoids compiler contention; source scans and `git diff --check` remain the completed verification artifacts.
