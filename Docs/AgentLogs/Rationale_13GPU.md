# Rationale_13GPU

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE BLOCKED BY HOST CONTENTION; prior project graph blocker remains

## Decision 001: Identity and Batch Mismatch

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="13GPU">`, while the user explicitly assigned ID `13GPU` and a GPU/flora/rocks/detail domain.

Solution: Use `13GPU` as the operational ID, create `Status_13GPU.md`, `Rationale_13GPU.md`, and `LOG_13GPU.md`, and treat user directive plus active domain roster as authority. Read nearest relevant batch prompt only as domain context if needed, not as identity.

Rejected Alternatives: Silently adopting `1316` would violate strict ID parsing and could collide with another vegetation-memory agent. Blocking the session would leave confirmed domain problems unexamined.

Scalability potential: Low/Middle/High/Ultra work remains scoped to continuous `GlobalQualityWeight`, GPU Resident Drawer, manual BRG only where procedural data lacks MeshRenderer ownership, and fake-first presentation.

Hardware Impact: No runtime change. Prevents process collision on i3/MX350 shared machine.

## Decision 002: Scatter Budget Continuous Scaling

Problem: `ScatterBudgetController` applied scatter/scavenge/collider budgets through discrete Surface/MidDepth/Deep thresholds. It had no hysteresis and did not consume `HomeostasisBrain.GlobalQualityWeight`, violating the continuous scalability pillar and risking budget flicker near 60m/180m thresholds.

Solution: Added a 3m minimum / 5m default depth hysteresis band for the debug/stable band label, replaced profile selection with smooth depth blending across threshold windows, and scaled scavenge radius/spawn cadence plus collider radius/op cadence through smoothed `GlobalQualityWeight`.

Rejected Alternatives: Editing already-modified GPU scatter or indirect vegetation renderer files would create cross-agent collision. Binary low/high quality switches were rejected because they violate the systemic mandate. Rewriting ScavengePopulator or ProximityColliderSystem was rejected because their existing `SetRuntimeBudget` contracts already clamp safe values.

Scalability potential: Low uses smaller radius/cadence to keep weak devices stable; Middle interpolates density without profile cliffs; High keeps longer residency and richer detail; Ultra increases visible density/collider processing without changing save identity or truth ownership.

Hardware Impact: Estimated 35-80 us saved on i3/MX350 during depth-boundary churn by avoiding repeated budget reapplication and collider/scavenge spike oscillation. Profile proof absent; status remains PENDING VERIFICATION.

## Decision 003: Build Deferral

Problem: Project rules forbid `dotnet build` when CPU is over 50% or another compiler is running. CPU counter returned `100.0%`.

Solution: Skipped build. Performed static diff and pattern scans only.

Rejected Alternatives: Running `dotnet build` under saturated CPU would violate host policy and interfere with other agents.

Scalability potential: No runtime change.

Hardware Impact: Prevented unnecessary host contention on the shared machine.

## Decision 004: BRG Culling Fail-Closed Guards

Problem: `HectonBatchRendererGroupUtility` accepted invalid instance matrices, culling planes, sphere centers, radii, and bounds. A NaN/Inf in shared BRG culling can leak into signed-distance tests and make corrupted instances visible or poison culling output.

Solution: Added finite checks inside `BuildMatrixVisibilityMaskJob`, `IsSphereVisible`, and `IsBoundsVisible`. Invalid inputs now write invisible/false and return before draw command population.

Rejected Alternatives: Rewriting BRG allocation/output ownership was rejected because Unity owns callback TempJob output and the current utility is the shared route. Editing already-modified procedural vegetation renderers was rejected to avoid cross-agent collision.

Scalability potential: Low/Middle/High/Ultra use the same fail-closed guard. The guard does not change quality behavior; it prevents corrupt data from wasting GPU submissions or crashing presentation.

Hardware Impact: Estimated 15-40 us saved on i3/MX350 during corrupt culling frames by stopping bad instances before draw range emission. Normal valid frames pay only scalar finite checks.

## Decision 005: HLOD Upload Compaction

Problem: `HectonHLODRenderer.BindNativeInstances` copied every incoming HLOD instance into upload arrays and merged bounds without validating matrix, bounds, fade, or origin-shift data. One corrupted instance could inflate/NaN the global bounds or upload unusable transform data.

Solution: Added compacting validation. Invalid HLOD instances are skipped; valid instances are uploaded contiguously. Origin shift and `SetGlobalBounds` now reject non-finite bounds/offsets.

Rejected Alternatives: Per-instance compute culling was rejected as an unproved job/compute path for a far-field HLOD renderer. Clearing the whole batch on the first bad instance was rejected because it destroys valid visual coverage.

Scalability potential: Low keeps far-field coverage stable by rejecting poison data without extra buffers. Middle/High/Ultra preserve valid overdraw/detail opportunities because the batch is compacted rather than dropped wholesale.

Hardware Impact: Estimated 20-60 us saved on i3/MX350 during poisoned HLOD batches by avoiding invalid BRG bounds churn and useless GPU work. No normal-frame runtime proof yet.

## Decision 006: Scatter Spatial Cache Input Guard

Problem: `WorldProceduralScatterWorkingMemory.TryRegisterGridPlacement` accepted non-finite placement positions and negative spacing into native scatter spatial buckets. That can corrupt spacing acceptance for flora/rocks and make placement nondeterministic.

Solution: Added a zero-allocation finite position check and non-negative finite spacing guard before capacity growth and native bucket insertion.

Rejected Alternatives: Rewriting spatial capacity growth was rejected because it is shared with dirty scatter backends and not necessary for the confirmed bug. Throwing exceptions was rejected because runtime scatter should fail closed, not crash.

Scalability potential: Low/Middle/High/Ultra all keep deterministic placement cache hygiene. Quality can scale density elsewhere, but invalid spatial truth is never accepted.

Hardware Impact: Estimated 10-30 us saved on i3/MX350 during corrupted scatter placement passes by avoiding bad bucket entries and downstream spacing comparisons.

## Decision 007: Compile Blocked By Existing External Dependency

Problem: A limited `Assembly-CSharp.csproj` build was first skipped by the CPU guard at `52.8%`. A later policy-valid filtered build ran and failed outside the 13GPU domain: `CandiceSQLiteProvider.cs` cannot resolve `Mono.Data` and `SqliteDataReader`.

Solution: Recorded compile as blocked by external dependency. The filtered error list contains only the Candice SQLite dependency errors and no errors in touched GPU/scatter/HLOD files.

Rejected Alternatives: Editing the unrelated third-party Candice save provider would exceed the assigned GPU/flora/rocks/detail domain. Launching a full solution rebuild adds noise after the filtered runtime assembly build already isolated the blocking errors.

Scalability potential: No runtime change.

Hardware Impact: No runtime change. Compile proof is blocked by a pre-existing external dependency, not by the touched files.

## Decision 008: Instance Culling Compute Fail-Closed Guards

Problem: `InstanceCulling.compute` trusted `_HectonCullDistanceMeters`, `_HectonInstanceBoundsRadius`, matrix rows, frustum planes, voxel SDF transform constants, UVW coordinates, and sampled SDF values. A single NaN/Inf can leak into GPU branch conditions, integer casts, texture sampling, and visibility writes.

Solution: Added shader-side finite validation for matrix rows, planes, centers, radii, cull distance, voxel SDF transform, UVW coordinates, and sampled values. Invalid inputs now leave the instance invisible instead of making malformed data participate in culling.

Rejected Alternatives: Editing `InstanceCullingService.cs` was rejected because it was already modified by other agents. CPU-side readback validation was rejected because hot GPU visibility must not pay SetData/GetData or readback costs.

Scalability potential: Low/Middle/High/Ultra all share the same fail-closed safety route. Quality can still scale density/cadence elsewhere; invalid GPU cull truth never becomes visible content.

Hardware Impact: Estimated 20-50 us saved on i3/MX350 only during corrupt compute cull frames by avoiding poisoned branch/sample paths and useless visible writes. Normal frames pay scalar finite checks.

## Decision 009: Impostor Atlas UV Sanitization

Problem: `Hecton_Impostor.hlsl` used `atlasGrid.zw` as the cell scale while `atlasGrid.xy` represented columns/rows. If material data was inconsistent, impostors could sample the wrong atlas region or leak outside the intended cell.

Solution: Derived inverse grid scale from sanitized columns/rows and wrapped `viewIndex` within total cell count. This keeps shared impostor atlas addressing coherent even when material data is malformed.

Rejected Alternatives: Duplicating the fix in every impostor shader was rejected because `Hecton_Impostor.hlsl` is the shared route. Adding CPU material validation was rejected as a weaker cold-path-only guard.

Scalability potential: Low uses stable cheap billboard/impostor sampling; Middle/High/Ultra can spend saved budget on richer atlas/view counts without unstable addressing.

Hardware Impact: Estimated 5-20 us saved only in bad-material fallback cases by preventing expensive overdraw/debug churn from wrong impostor cells. Primary gain is visual correctness.

## Decision 010: HLOD Fallback Material Contract

Problem: `HectonHLODRenderer` exposed `_shader` and documented a hidden fallback shader, but `ResolveMaterial()` returned only `_material`. With no assigned material, the far-field HLOD renderer silently disabled itself.

Solution: Added a cold cached runtime material built from the assigned shader or hidden fallback shader, plus deterministic release on disable/destroy and finite global-floating-offset guards.

Rejected Alternatives: Per-frame material creation was rejected because it allocates and breaks hot-path discipline. Forcing every scene author to assign a material was rejected because the component already advertises a fallback shader contract.

Scalability potential: Low gets stable cheap far-field representation instead of missing HLOD. Middle/High/Ultra can keep valid far-field visual density while runtime material ownership remains one cached object.

Hardware Impact: No CPU saving claim. It restores expected HLOD visual output with one cold material allocation, avoiding manual scene fixes and missing far-field coverage.

## Decision 011: Build Contention And Guarded Compile Recheck

Problem: A `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` process was already running under PID 24312. Project policy forbids concurrent `dotnet`/`csc` builds.

Solution: Did not relaunch compile while PID 24312 was active. After process check returned no `dotnet`/`csc` and CPU was 27%, ran a narrow `dotnet build Assembly-CSharp.csproj --no-restore -v quiet /clp:ErrorsOnly -maxcpucount:1`. It failed only on the pre-existing Candice SQLite dependency: missing `Mono.Data` and `SqliteDataReader`.

Rejected Alternatives: Starting a second build during PID 24312 would violate the shared-machine rule and interfere with other agents. Full solution rebuild was rejected because the runtime assembly build already isolated the compiler blocker.

Scalability potential: No runtime change.

Hardware Impact: Prevents host contention on the shared i3/MX350-class target and keeps compile ownership clean. No touched GPU/scatter/HLOD file was reported in compiler errors.

## Decision 012: Distant Landmark BRG Fail-Closed Path

Problem: `HectonDistantLandmarkRenderer` promised a hidden shader fallback but `ResolveMaterial()` returned only `_material`. It also accepted external buffers, native bounds, native HLOD entries, origin shifts, and draw bounds without finite validation. A bad payload could poison BRG global bounds or upload invalid matrices.

Solution: Added fail-closed validation for external buffer binding, native bounds, native HLOD entries, origin shifts, and fallback draw bounds. Native payloads are compacted so one bad entry does not drop valid far-field landmarks. Added one cached runtime material from the assigned or hidden silhouette shader and deterministic release.

Rejected Alternatives: Editing `HectonOctahedralImpostorRenderer`, `GpuScatterLodManager`, `CullingManager`, or `HectonRockManager` was rejected because those files are already dirty from other agents. Per-frame material creation was rejected because it allocates and violates the BRG owner contract.

Scalability potential: Low keeps cheap far-field silhouettes instead of missing landmarks. Middle/High/Ultra retain valid distant coverage and can spend visual budget on denser landmark silhouettes without accepting corrupt bounds.

Hardware Impact: Estimated 15-45 us saved on i3/MX350 only in corrupt landmark/HLOD payload frames by avoiding invalid BRG bounds/upload churn. Fallback material restores visuals; no normal-frame CPU saving is claimed.

## Decision 013: Impostor Atlas Pack Bounds Guard

Problem: `PackImpostorAtlas.compute` clamped `_ViewIndex` only to non-negative. If `_ViewIndex >= atlasGrid.x * atlasGrid.y` or texture dimensions disagreed with tile settings, the kernel could address outside the intended atlas tile/output texture.

Solution: Wrapped view index by sanitized total atlas cells, read source/output texture dimensions, clamped source pixels to shared source dimensions, and rejected output pixels outside the atlas target.

Rejected Alternatives: Trusting editor tooling was rejected because the atlas compute kernel is the last line before corrupt texture writes. Adding CPU readback validation was rejected because the shader can cheaply fail closed without readback.

Scalability potential: Low/Middle/High/Ultra all use stable impostor atlas generation. Stronger devices can use more view cells without undefined writes when authoring data is malformed.

Hardware Impact: No frame-time saving claim. This is atlas correctness and corruption containment for impostor generation.

## Decision 014: Compile Blocked By Project Graph

Problem: After new 13GPU edits, guarded `dotnet build Assembly-CSharp.csproj --no-restore -v quiet /clp:ErrorsOnly -maxcpucount:1` no longer reached runtime compile. It failed on MSBuild circular `ResolveProjectReferences` in `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj`. A guarded `--no-dependencies` attempt failed on missing `Temp/CodexBuild/Unity.ShaderGraph.Editor.dll`.

Solution: Recorded compile as blocked by external project graph / generated editor DLL state. Did not edit Unity editor project graph or package artifacts because that exceeds 13GPU domain and risks other agents.

Rejected Alternatives: Fixing generated Unity package/editor project references from this GPU/flora agent was rejected as cross-domain interference. Repeated build attempts after two infrastructure blockers were rejected.

Scalability potential: No runtime change.

Hardware Impact: No runtime change. Static validation remains the only available proof for this pass.

## Decision 015: GPU Scatter Grid Candidate Window

Problem: `Hecton_GpuScatter.compute` sanitized `_HectonScatterCandidateCount` but did not clamp it to `gridResolution * gridResolution`. If CPU-side input published more candidates than cells, `GenerateScatterInstances` could derive `cellZ` outside the scatter field and `CompactVisibleScatterInstances` could read stale visibility cache entries for non-existent cells.

Solution: Added `ResolveGridCandidateCount(gridResolution, candidateCount)` and used it in both generate and compact kernels. Threads beyond the valid grid-cell window now clear visibility cache when still inside published candidate count, then return.

Rejected Alternatives: Editing dirty `GpuScatterLodManager.cs` was rejected to avoid cross-agent collision. Trusting CPU dispatch contracts was rejected because the compute kernel owns the final safety boundary before GPU writes and append compaction.

Scalability potential: Low/Middle/High/Ultra all keep candidate generation bounded to real grid cells. Higher tiers can raise grid resolution/candidate counts without undefined stale-cache compaction when authoring or runtime inputs drift.

Hardware Impact: Estimated 10-35 us saved on i3/MX350 only during malformed scatter dispatches by avoiding out-of-field cell work and stale append compaction. Normal valid frames pay two scalar min checks.

## Decision 016: Impostor Atlas Dilation Dimension Guard

Problem: `DilateImpostorEdges.compute` trusted `_AtlasSize` for source, mask, and output accesses. If source/mask/output texture dimensions disagreed with `_AtlasSize`, dilation could sample or write outside the real atlas surfaces.

Solution: Added `GetDimensions` checks for source atlas, mask atlas, and output atlas. Runtime atlas size is clamped to the minimum real dimensions across all three textures before any read/write or neighbor clamp.

Rejected Alternatives: Trusting importer/editor invariants was rejected because this compute kernel is the last write barrier for impostor atlas quality. CPU readback validation was rejected because shader-side dimension checks are direct and cheaper.

Scalability potential: Low/Middle/High/Ultra all keep stable atlas dilation. Higher tiers can bake larger atlases without undefined writes if a single texture dimension is malformed.

Hardware Impact: No frame-time saving claim. This is atlas corruption containment during impostor generation.

## Decision 017: Kelp GPUI Continuous Motion And Finite Ingress

Problem: `Hecton_KelpMaster_GPUI.shader` consumed raw vertex positions/normals, raw `_TotalUniverseOffset`, raw sway settings, and raw `_Time` in kelp sine/parabola motion across forward, shadow, and depth passes. The `_QUALITY_MX350` and `_QUALITY_HIGH` variants also imposed fixed visual behavior instead of consuming the global continuous quality weight inside the active variant.

Solution: Added shader-side finite ingress guards for vertex positions/normals/tangent, global offset, sway frequency/speed/phase, time, UV height mask, and vertex color seeds. Added `_H8GlobalQualityWeight` gates for sway motion, interaction displacement, and high-quality parallax so the active variant still scales continuously from survival to visual overkill.

Rejected Alternatives: Editing GPU Instancer managers was rejected because this defect is shader-local and those routes are shared/dirty. Removing shader variants wholesale was rejected because material/keyword contracts may be authored elsewhere. CPU validation or readback was rejected because hot flora presentation must remain GPU-side.

Scalability potential: Low keeps cheap, bounded sway and reduced interaction deformation; Middle restores moderate flow motion; High adds richer kelp movement and parallax; Ultra spends saved budget on full motion/parallax without changing gameplay truth, DTO layout, or ownership route.

Hardware Impact: Estimated 8-25 us avoided only during corrupt kelp payload frames by preventing NaN propagation through vertex motion/shadow/depth paths. Normal valid frames gain continuous visual scalability; no profiler-backed CPU saving is claimed.

## Decision 018: Coral GPUI Ripple Sanitation And Continuous Overkill Gate

Problem: `Hecton_CoralMaster_GPUI.shader` cast `_BiolumTouchRippleParams.x` directly to an int and consumed `_BiolumTouchRipples` entries without finite validation. A malformed ripple payload could poison biolum math, while high parallax and touch ripple intensity were not gated by `_H8GlobalQualityWeight` inside the active shader variant.

Solution: Added `_H8GlobalQualityWeight`, smooth quality gates for high parallax and touch ripple energy, finite position checks, sanitized ripple count clamping, and per-ripple finite guards before distance/radius math.

Rejected Alternatives: CPU readback validation was rejected because shader-side validation is the final cheap barrier before fragment work. Editing flora runtime managers was rejected because the clean shader already owns the confirmed issue and dirty managers may be owned by other agents.

Scalability potential: Low keeps coral readable with minimal ripple overkill; Middle enables controlled touch response; High and Ultra restore richer biolum ripple/parallax detail continuously without touching gameplay authority or save identity.

Hardware Impact: Estimated 5-18 us avoided only during corrupt ripple payload frames by skipping malformed ripple work. Normal-frame effect is visual budget shaping, not a measured CPU/GPU saving.

## Decision 019: Final Build Gate Respect

Problem: Final verification needed a compile check, but project policy forbids `dotnet build` while CPU is above 50% or another compiler/dotnet process is active. Final sample returned CPU `100` and active `dotnet` PID `2448`.

Solution: Did not relaunch compile. Kept proof to JSON parse, `git diff --check`, static hot-path scan, and prior guarded compiler results.

Rejected Alternatives: Launching another build under CPU saturation would violate the shared-machine rule and interfere with other agents. Killing or inspecting another agent's active dotnet process was rejected because this agent does not own it.

Scalability potential: No runtime change.

Hardware Impact: Prevented additional host contention on the shared low-end target.

## Decision 020: Geology Impostor Billboard Fail-Closed Inputs

Problem: `Hecton_GeologyImpostorBillboard.shader` trusted vertex position, UV, base color, alpha clip threshold, and ambient floor before atlas sampling and alpha clip. Non-finite UV/material data can push atlas sampling/clip into undefined visual output.

Solution: Added finite helpers for scalar, UV, and color inputs. Vertex position now falls back to zero, UV falls back to atlas center/saturate, base color and sampled atlas color fail closed, and alpha/ambient material controls are finite-saturated before use.

Rejected Alternatives: CPU-side material validation was rejected because the shader is the final cheap guard before sampling. Changing atlas/importer contracts was rejected because the billboard shader can contain malformed data locally.

Scalability potential: Low keeps geology impostors deterministic and cheap; Middle/High/Ultra can use the same impostor atlas path without undefined samples when authoring data drifts.

Hardware Impact: Estimated 3-8 us avoided only during malformed impostor payload frames by preventing bad samples/clip churn. Normal valid frames pay scalar finite checks.

## Decision 021: Voxel SSAO Parameter Sanitation

Problem: `Hecton_VoxelSSAO.compute` used raw projection scale, radius, intensity, and depth sigma before deriving pixel radius, rounding to int2, and accumulating occlusion. A NaN radius path can reach float-to-int conversion and invalid sampling offsets.

Solution: Added finite fallback for SSAO parameters and for `FastRoundToInt2` input. Projection/radius/intensity/sigma now clamp to deterministic defaults before sample offset and accumulation math.

Rejected Alternatives: CPU readback/depth prevalidation was rejected because this is a compute-local contract and readback violates GPU hot-path discipline. Increasing sample count for robustness was rejected because it spends performance without fixing malformed parameters.

Scalability potential: Low keeps 4-tap SSAO stable on weak GPUs; Middle/High/Ultra can raise dispatch resolution externally without invalid parameter data poisoning the kernel.

Hardware Impact: Estimated 5-15 us avoided only during malformed SSAO dispatches by avoiding bad sample offsets and wasted accumulation. Normal valid frames pay scalar finite checks.

## Decision 022: Abyssal Voxel Rock Finite Contract

Problem: `Hecton_AbyssalVoxelRock.shader` accepted unchecked vertex/absolute positions, floating-origin offset, biome influence grid params, volume tint coordinates, cut-mask rects, damage volume params, and caustic time/layer data before int casts, 2D/3D samples, noise math, and vertex displacement.

Solution: Added finite scalar/vector helpers, sanitized SafeNormalize inputs, sanitized sample position/origin offset, guarded vertex displacement ingress, fail-closed biome grid casts, finite volume/cut/damage mask sampling, and finite time/caustic layer defaults.

Rejected Alternatives: Editing terrain/voxel runtime owners was rejected because the shader owns the confirmed last-mile hazard and those systems are cross-domain. Disabling caustics, biome tint, or deformation was rejected because immersion is the goal; malformed inputs now fail closed while valid data keeps visual overkill.

Scalability potential: Low keeps rock geometry readable and stable with cheap guards; Middle keeps biome tint and carving stable; High/Ultra preserve screen-space bevels, caustics, and visual family tint without accepting corrupt payloads.

Hardware Impact: Estimated 12-40 us avoided only during corrupt rock/geology payload frames by preventing NaN propagation, invalid int casts, and malformed texture samples. Normal valid frames pay scalar/vector finite checks.

## Decision 023: Rock Pass Build Gate Recheck

Problem: After the rock/geology shader pass, compile verification was still required, but CPU samples were `73` and then `77` after a 30-second wait. No `dotnet/csc` process was active, but project policy also forbids builds above 50% CPU.

Solution: Did not launch build. Kept verification to JSON parse, `git diff --check`, hot-path scan, and prior compiler blocker history.

Rejected Alternatives: Launching compile under sustained CPU saturation was rejected because it violates the shared-host rule and interferes with other agents.

Scalability potential: No runtime change.

Hardware Impact: Prevented additional build contention on the shared machine.

## Decision 024: Terrain Damage Volume Bounds Contract

Problem: `Hecton_TerrainDamageVolume.compute` trusted `_HectonDamageVolumeResolution`, source/result 3D texture dimensions, world-min/inv-size params, recovery, and stamp payloads before `Texture3D.Load`, `RWTexture3D` writes, radius math, and strength blending.

Solution: Query source/result texture dimensions in-kernel, clamp active resolution to the shared valid 3D region, sanitize world parameters and recovery, skip malformed stamp centers, sanitize radius/strength, and force finite output.

Rejected Alternatives: CPU-side stamp validation was rejected because the compute kernel is the final write barrier. Replacing the damage volume with physical terrain deformation was rejected because this is a visual dear-lie mask path.

Scalability potential: Low keeps cheap volume damage masks stable; Middle/High/Ultra can raise damage-volume resolution or stamp count externally without undefined writes when authoring/runtime params drift.

Hardware Impact: Estimated 4-12 us avoided only during malformed damage-volume dispatches by preventing out-of-resource texture writes and poisoned radius math. Normal valid frames pay scalar/resource dimension guards.

## Decision 025: Micro Particle Flow Fail-Closed Path

Problem: `ParticleUpdate.compute` cast `_FieldParams.y` to `uint` without a non-negative clamp or read/write buffer count guard. It also derived flow-field indices from raw resolution/origin/cell-size and consumed raw particle/flow samples before velocity integration.

Solution: Query particle/flow buffer counts, clamp particle count to the shared read/write capacity, cap flow resolution to a sane visual-field range, reject flow sampling if required samples exceed the buffer, sanitize particle state, params, flow samples, dt, velocity, position, size, and life before writeback.

Rejected Alternatives: CPU particle dispersion and GPU readback validation were rejected. The existing shader fake is retained; malformed data now collapses to bounded visual motion instead of undefined GPU indexing.

Scalability potential: Low keeps blood/micro-particle dispersion bounded with cheap triangle turbulence; Middle/High/Ultra can feed richer flow fields without corrupting buffer indices if the flow payload is malformed.

Hardware Impact: Estimated 6-20 us avoided only during corrupt micro-particle/flow payload frames by skipping bad flow samples and preventing huge uint particle windows. No normal-frame profiler saving is claimed.

## Decision 026: Sargassum Flora Shader Finite Ingress

Problem: `Hecton_SargassumMaster.shader` used raw vertex position, normal, color, UV, `_Time`, prop-wash position/radius/force, global drift, cut-mask rect, buoyancy sink rect/depth, interaction params, and alpha clip in forward and shadow passes. Malformed data could poison sway/pulse/cut motion, texture mask UVs, shadow caster positions, or cutout clip.

Solution: Added finite helper overloads inside both passes, sanitized normalization, leaf mask, cut mask, sink mask, organic density, vertex ingress, prop-wash motion, pulse motion, wound curl, alpha clip, and final color output. The fake shader-driven motion path remains intact.

Rejected Alternatives: Disabling sargassum motion was rejected because immersion is the goal. Editing dirty `SargassumMicroFaunaBoids.compute` was rejected to avoid sibling-agent collision. CPU material validation was rejected because shader-local last-mile guards are cheaper and closer to the hazard.

Scalability potential: Low keeps stable cheap sargassum silhouettes and shadow casters; Middle keeps prop-wash/cut/sink response; High/Ultra preserve richer shader motion/biolum response without accepting corrupt inputs.

Hardware Impact: Estimated 6-18 us avoided only during malformed sargassum payload frames by preventing NaN propagation through vertex/shadow/clip paths. Normal valid frames pay finite checks and keep visual detail.

## Decision 027: TerrainMaster Binary Math LOD Residual Risk

Problem: Clean `TerrainMaster.shader` still contains `_MATH_LOD_LOW` compile-time branches for terrain micro-detail. This violates the ideal continuous-weight mandate in principle, but the runtime contract for a terrain-wide quality property and material binding was not proven in this pass.

Solution: Did not mutate `TerrainMaster.shader` blindly. Recorded it as residual risk for a targeted terrain-material contract pass where `_H8GlobalQualityWeight` binding, MX350 cost, and high-tier detail path can be verified together.

Rejected Alternatives: Removing the binary variant now was rejected because it could force extra terrain texture sampling on MX350. Adding an unbound property and declaring compliance was rejected as fake compliance.

Scalability potential: Low/Middle/High/Ultra fix path requires a continuous quality property plus dynamic micro-detail weight, not a cosmetic shader keyword rename.

Hardware Impact: No runtime change. Avoided speculative terrain shader regression without proof.

## Decision 028: Micro-Detail Pass Build Blocker

Problem: After the terrain-damage, particle, and sargassum shader/compute pass, the CPU/process gate allowed one narrow compile check, but `dotnet build Assembly-CSharp.csproj --no-restore -v quiet /clp:ErrorsOnly -maxcpucount:1` failed before domain compile on MSBuild circular `ResolveProjectReferences` in `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj`.

Solution: Recorded compile as blocked by existing project graph. Did not edit Unity editor package project files or generated package references from the 13GPU domain.

Rejected Alternatives: Repeated build retries were rejected because the same external project graph blocker already reproduced. Editing Unity package/editor project references was rejected as cross-domain interference and not relevant to HLSL-only changes.

Scalability potential: No runtime change.

Hardware Impact: No runtime change. Compile proof remains blocked externally; static shader/compute proof is limited to diff/pattern/JSON checks.

## Decision 029: Kelp Non-GPUI Shadow/Depth Motion Contract

Problem: `Hecton_KelpMaster.shader` forward pass already consumed `_H8GlobalQualityWeight`, but shadow and depth passes still carried the older raw motion path. They read vertex position, normal, UV height, `_Time`, sway params, AUP offset, prop-wash and submarine wash data before shadow/depth deformation, so malformed data could poison shadow caster/depth output even when the forward pass was safe.

Solution: Added `_H8GlobalQualityWeight` to shadow and depth passes, finite ingress guards for position/normal/UV/time/sway/AUP values, finite `HectonKelpSafeNormalize`, and smooth motion/interaction gates matching the forward pass. Shadow/depth deformation remains a shader-side visual fake.

Rejected Alternatives: Disabling kelp shadow/depth deformation was rejected because it would visibly desync silhouettes from forward motion. CPU-side material or mesh validation was rejected because the shader is the final cheap barrier and hot flora presentation must not add readback or upload routes.

Scalability potential: Low keeps bounded, cheap kelp silhouettes and depth; Middle restores moderate fake sway; High/Ultra keep richer motion and prop-wash response continuously without changing gameplay truth or instance ownership.

Hardware Impact: Estimated 4-12 us avoided only during malformed kelp shadow/depth payload frames by preventing NaN propagation and bad shadow bias/depth deformation. Normal valid frames pay scalar finite checks; no profiler-backed saving claim.

## Decision 030: Coral Non-GPUI Shadow Normal Guard

Problem: `Hecton_CoralMaster.shader` forward pass already had the ripple/quality gates in baseline, but its shadow pass sanitized only `positionOS` and still transformed raw `input.normalOS` through `TransformObjectToWorldNormal` and shadow bias.

Solution: Added finite normal fallback and non-zero length guard before the shadow bias path. This keeps malformed coral mesh payloads from poisoning shadow caster output.

Rejected Alternatives: Dropping coral shadows or trusting authoring data was rejected. The fix is local, zero-buffer, and does not change material or draw ownership.

Scalability potential: Low/Middle/High/Ultra all keep deterministic coral shadow casters. Quality scaling remains in the forward ripple/parallax path; invalid shadow normals fail closed.

Hardware Impact: Estimated 2-6 us avoided only during malformed coral shadow payload frames. Main gain is deterministic shadow/depth safety, not normal-frame speed.

## Decision 031: Procedural Bio Low-Variant Varying Contract

Problem: `Hecton_ProceduralBio.shader` low LOD branch read `input.positionWS` for ambient, but `positionWS` was wrapped in `#if !defined(_MATH_LOD_LOW)`. The low variant could therefore compile with a missing varying. The shader also had no continuous `_H8GlobalQualityWeight` shaping for low-tier matcap/emission and high-tier blend sharpness.

Solution: Moved `positionWS` outside the `_MATH_LOD_LOW` conditional, added `_H8GlobalQualityWeight` helpers, sanitized vertex color, used continuous quality to damp low matcap/emission and high triplanar sharpness. Kept the low shader path instead of forcing triplanar sampling on weak hardware.

Rejected Alternatives: Removing `_MATH_LOD_LOW` in this pass was rejected because it can force additional texture samples on MX350 without a terrain/flora material contract review. Leaving the missing varying was rejected because it is a real low-variant compile contract violation.

Scalability potential: Low keeps the matcap fake but with continuous energy shaping; Middle blends out excessive cheap matcap; High/Ultra sharpen triplanar projection smoothly in the high variant without binary visual jumps inside the active variant.

Hardware Impact: No normal-frame saving claim. Estimated 3-10 us avoided only by preventing bad low-variant fallback/debug churn; primary gain is compile-contract and visual scalability hygiene.

## Decision 032: Non-GPUI Flora Build Gate Respect

Problem: After the non-GPUI flora pass, compile verification was required. A 30 second wait dropped CPU to `34`, but active compiler processes remained: `dotnet` PID `8372` and `VBCSCompiler` PID `31136`.

Solution: Did not launch `dotnet build`. Kept verification to `git diff --check`, targeted text scans, SHA-256 hashes, and documented the existing prior MSBuild project graph blocker.

Rejected Alternatives: Launching a competing build would violate the shared-host rule and interfere with another active agent.

Scalability potential: No runtime change.

Hardware Impact: Prevented additional host contention on the shared low-end target.

## Decision 033: Sargassum Cut-Mask Compute Fail-Closed Writes

Problem: `Hecton_SargassumCutMask.compute` trusted authored texel size, scroll offset, recovery, and stamp payloads before normalized UV sampling and mask writes. A malformed stamp could push NaN/Inf into radius math or leave an over-strength cut mask.

Solution: Added finite guards for texel size, scroll offset, recovery, stamp vectors, radius, strength, sampled current value, and final output. The kernel derives a fallback texel size from the real result texture dimensions and caps stamp iteration to the existing fixed capacity.

Rejected Alternatives: CPU-side readback validation was rejected because the compute kernel is the last cheap write barrier. Editing `SargassumCutManager` was rejected in this pass because its runtime ownership is broader and not necessary for the confirmed shader-local defect.

Scalability potential: Low keeps cheap cut-mask recovery deterministic; Middle/High/Ultra can keep more active sargassum cuts without malformed stamps corrupting the visual mask.

Hardware Impact: Estimated 2-8 us avoided only during malformed cut-mask stamp frames. Normal frames pay scalar finite checks and keep the same dispatch shape.

## Decision 034: Sargassum Facade Quality And Dimension Contract

Problem: `Hecton_SargassumDampingFacade.compute` trusted density/cut world rects, drift offset, output texture dimensions, and pow exponents/scales. `SargassumCrestDampingController` sent fixed wave/oil facade scale, so the visual fake did not consume continuous `GlobalQualityWeight`.

Solution: The compute kernel now clamps to the shared valid wave/oil output dimensions, sanitizes rects/drift, clamps pow exponents/scales, and writes finite outputs. The owner now resolves `HomeostasisBrain.GlobalQualityWeight` and scales only visual wave/oil facade intensity through a smooth continuous curve.

Rejected Alternatives: Crest shader mutation and physical wave damping simulation were rejected. The facade is a deterministic visual fake; gameplay truth, save identity, DTO layout, and ownership route remain unchanged.

Scalability potential: Low keeps readable but cheaper wave damping/oil film masks; Middle blends facade intensity smoothly; High/Ultra spend visual budget on stronger sargassum wave/oil presentation without adding simulation.

Hardware Impact: Estimated 3-9 us avoided only during malformed facade dispatches. Normal-frame impact is visual budget shaping, not a profiler-backed CPU saving.

## Decision 035: Vegetation Wake-Trail Compute Stamp Contract

Problem: `Hecton_VegetationWakeTrailSim.compute` looped over raw `_HectonWakeTrailStampCount` and used raw stamp vectors, simulation time, scroll offset, texel size, damping, curl, diffusion, wave, fade, and sampled pixels before noise, velocity, and displacement math. The C# owner buffer capacity is 4, but the shader did not enforce it.

Solution: Added a shader-local capacity cap of 4, finite guards for all stamp fields and runtime parameters, fallback texel size from real texture dimensions, finite sample defaults, and fail-closed velocity/displacement output. The existing shader fake remains the presentation path.

Rejected Alternatives: CPU wake simulation, GPU readback validation, and editing the dirty `FloraInteractionManager.cs` quality-resolution path were rejected. Dirty owner code remains a residual target for a later no-collision pass.

Scalability potential: Low keeps bounded wake cues with cheap diffusion; Middle/High/Ultra can use richer stamp energy/curl from the existing owner without malformed inputs corrupting the full wake texture.

Hardware Impact: Estimated 4-14 us avoided only during malformed wake-trail dispatches. Normal frames pay scalar finite checks; no profiler-backed saving is claimed.

## Decision 036: Sargassum/Wake Pass Build Gate Respect

Problem: Compile verification was required after the sargassum/wake pass, but the shared-host rules forbid build launch while CPU is over 50% or any `dotnet`/`csc`/`VBCSCompiler` process is active. Initial CPU was `100` with `VBCSCompiler` PID `31136`; after 30 seconds CPU was `26`, but the compiler process remained active.

Solution: Did not launch `dotnet build`. Kept verification to JSON parse, `git diff --check`, targeted hot-path scan, SHA-256 hashes, and recorded the existing prior MSBuild project graph blocker.

Rejected Alternatives: Launching a competing build while `VBCSCompiler` was active was rejected because it violates project policy and can interfere with other agents.

Scalability potential: No runtime change.

Hardware Impact: Prevented additional build contention on the shared low-end target.
