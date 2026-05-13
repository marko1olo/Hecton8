# Rationale - DIEGETIC_DAMAGE_HOLOGRAPHER

Status: PENDING VERIFICATION.

## Decision 0 - Mandate Selection

Problem: Cockpit damage hologram crosses diegetic UX, GPU compute, damage events, telemetry, and MX350 budget constraints.
Solution: Read the registry mandates for diegetic physical UI, zero-GC UI streaming, MX350 compute kernels, GPU ownership, zero-GC policy, frame/VRAM budgets, crash telemetry, and hull damage feedback before code changes.
Rejected Alternatives: Treating it as a normal Canvas HUD was rejected because the prompt explicitly forbids Unity UI Canvas and requires Graphics.DrawMeshInstancedIndirect. A CPU-driven per-frame mesh rebuild was rejected because it violates zero-GC and MX350 frame budget policy.
Scalability potential: Low uses a static dashboard warning icon and no compute; Middle uses 512-point capped point cloud; High uses active scan/flicker with flooding tint; Ultra can spend saved cycles on denser glow, extra scan layers, and higher-frequency visual sync without changing gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain is avoiding GameObject cube instantiation and CPU readbacks; estimated hot-path CPU savings versus naive CPU mesh/canvas path: 150-500 us PENDING VERIFICATION.

## Decision 1 - Cockpit Runtime Ownership

Problem: Damage hologram needs VISUAL_SYNC execution and cockpit anchoring without adding another manager in a codebase already running many agents.
Solution: Extend VehicleSubOsCockpitRuntime, which already owns IUpdatable, ILateFrameTickable, and IRenderable registration through GlobalRegistry. Damage signal snapshots are consumed in Tick, compute/draw executes in Render.
Rejected Alternatives: A standalone MonoBehaviour singleton was rejected because it creates a new dependency path and violates the prompt's singleton-eradication directive. A Canvas overlay was rejected because the prompt forbids 2D Canvas for this system.
Scalability potential: Low tier runs a fixed seven-point warning glyph; Middle and High use capped compute mapping; Ultra can swap in denser LOD3 vertices while the point cap remains fixed at 512 unless the mandate changes.
Hardware Impact: Avoids an additional registry lane and GameObject point spawning; estimated i3/MX350 savings versus per-point GameObject cubes: 300-900 us CPU and unbounded GC avoided, PENDING VERIFICATION.

## Decision 2 - Missing LOD3 Asset Handling

Problem: The prompt requires a low-poly submarine proxy mesh LOD3, but static project search found no submarine LOD3 mesh asset under Assets/_Project.
Solution: Add a serialized damageProxyMeshLod3 field and one-time GPU upload path capped to 512 vertices. Until art wires the asset, a 32-vertex local-space fallback hull proxy preserves compile/runtime behavior without hot-path mesh generation.
Rejected Alternatives: Runtime procedural mesh generation was rejected because it is unnecessary cold complexity and risks allocation churn. Blocking the entire feature was rejected because compute/render/telemetry can be implemented now against the serialized contract.
Scalability potential: Low uses static warning; Middle uses fallback/LOD3 capped at 512; High/Ultra can consume a real LOD3 mesh without code changes.
Hardware Impact: Fallback proxy costs 32 vertex checks x 16 dents per visual sync, effectively below 10 us GPU on MX350; real LOD3 remains capped to avoid >512 append pressure.

## Decision 3 - GPU Append And Indirect Draw

Problem: Hull dents are already published as a global shader array, but cockpit UX needs a local diegetic diagnostic without CPU readback, Canvas sprites, or marker GameObjects.
Solution: Use Hecton_DamageHologram.compute to scan capped local proxy vertices against _HectonHullDents[16], append float4(local position, severity), then copy the append counter into an indirect indexed draw args buffer. VehicleSubOsCockpitRuntime.Render draws the point cubes through Graphics.DrawMeshInstancedIndirect in the existing render lane.
Rejected Alternatives: CPU distance scans were rejected because they burn main-thread budget for presentation. GameObject cube markers were rejected because transforms and renderers would violate zero-GC and frame-budget mandates. Canvas health markers were rejected because the prompt forbids 2D Canvas for this cockpit system.
Scalability potential: Low disables compute and draws a fixed seven-point warning glyph. Middle uses the 32-point fallback proxy. High uses a real LOD3 proxy up to 512 vertices. Ultra can increase material glow, scanline layers, and proxy density only if the 512 point cap is explicitly raised.
Hardware Impact: Expected i3/MX350 gain versus CPU marker path is 300-900 us CPU and unbounded GC avoided. GPU cost is bounded by 512 vertices x 16 dents plus one indirect cube draw. Owned shader import now has 0 filtered console entries; profiler/driver timing remains PENDING VERIFICATION because global compile and scene ownership are not clean.

## Decision 4 - Impact, Flood, And Low Tier Coupling

Problem: The hologram must react to impact and flooding without binding UX directly to physics, room GameObjects, or a new data-owner.
Solution: Read HighSpeedImpactSignal snapshots for a 0.5 second flicker timer and read RoomWaterLevels through IHabitatGraphService sequence changes. Low tier bypasses compute and uploads a fixed seven-point warning glyph into the same draw buffer, so the dashboard still communicates damage state without extra rendering architecture.
Rejected Alternatives: Direct room-object lookups were rejected because they cross domain ownership and would scale with scene complexity. Coroutines/Animators were rejected because they allocate or schedule outside the deterministic cockpit tick. A single low-tier dot was rejected as too ambiguous for a warning icon.
Scalability potential: Low shows the fixed warning glyph; Middle tints scanline/damage points by room flood level; High keeps flicker/flood tied to signal truth; Ultra can add stronger material bloom or more scan layers without changing the data path.
Hardware Impact: Low tier avoids one compute dispatch and 512 x 16 dent tests, saving roughly 8-35 us GPU on MX350-class hardware. Room water upload is limited to 32 floats and only on sequence changes, avoiding per-frame room scans.

## Decision 5 - Hologram Blackbox And Capacity Clamp

Problem: The prompt requires a hard 512 point VRAM budget and blackbox visibility into HoloDamagePoints, while the existing cockpit dump only covered older radar/power fields.
Solution: Keep MaxDamageHologramPoints as the single capacity for proxy clamp and append buffer allocation, copy the append counter to indirect args with GraphicsBuffer.CopyCount, and extend the fixed 300-frame cockpit telemetry entry with hologram count, proxy count, flicker, flood, and flags. A mirror dump named Dump_DIEGETIC_DAMAGE_HOLOGRAPHER.bin is written from the same ring on blackbox dump.
Rejected Alternatives: CPU readback for exact visible count was rejected because it stalls the GPU and violates the prompt's CopyCount instruction. Debug.Log telemetry was rejected because the blackbox mandate requires fixed-size postmortem state on disk.
Scalability potential: Low writes seven warning points; Middle and High stay inside 512 appended points; Ultra remains bounded until the cap is intentionally changed and measured.
Hardware Impact: Normal frame cost is a fixed telemetry struct write and one GPU counter copy after dispatch. Estimated main-thread savings versus CPU readback/marker reporting is 120-400 us on i3/MX350-class hardware; blackbox file I/O is crash/manual-dump path only.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat audit found avoidable shader/C# divisions and required proof that no owned hot path added managed loops, string formatting, scene search, Canvas, or CPU readback.
Solution: Replaced shader range/falloff divisions with `rcp()` multiplication in Hecton_DamageHologram.compute and Hecton_DamageHologramInstanced.shader. Replaced the hologram flicker timer division with `DamageHologramFlickerSecondsInv`. Re-ran scoped audits on VehicleSubOsCockpitRuntime and the two damage hologram shaders for `sqrt`, `normalize`, `foreach`, `string.Format`, `.ToString()`, LINQ materializers, `Find`, GameObject markers, Instantiate, Canvas, Debug.Log, and `$"`; hits were false positives on variable names like `normalized`, not prohibited calls in the new damage path.
Rejected Alternatives: Leaving scalar divisions was rejected because the polish mandate explicitly asks for reciprocal multiplication. A LUT was rejected for dent distance because 16 dynamic dent centers and proxy vertex positions make LUT cache pressure worse than a bounded dot product. CPU readback was rejected again because `GraphicsBuffer.CopyCount` already gives the indirect draw count.
Scalability potential: Low remains a seven-point warning glyph with zero compute dispatch. Middle/High keep 512-point capped append mapping. Ultra can buy stronger material response with saved CPU/GPU budget without changing truth ownership.
Hardware Impact: Removes three shader divides and one C# divide from the owned hologram path. Estimated MX350 gain is small per frame (<5 us) but deterministic and free; the larger saved cost remains avoiding CPU marker rebuild/readback (120-900 us depending on naive alternative).
Verification: Unity MCP validate_script on Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs passed with 0 diagnostics after polish. `dotnet build Hecton8.Core.csproj` failed with 132 errors from unrelated missing assemblies/namespaces/types, including Hecton8.Environment.Fluids, Hecton8.Core.Scheduling, Hecton8.Core.Memory.Layout, Hecton8.Physics.CCD, Hecton8.Audio.Propagation, MacroSwarm, BrineLayerSample, and IGroundRadarService. Unity console read remains blocked because the Unity session is not ready for read_console.

## Continuation Recheck - Import And Runtime Hardening

Problem: Follow-up review found non-visual defects: new `.cs.meta` files were missing MonoImporter sections, staged room-water levels could miss the first GPU upload if the buffer was created after the flood sequence changed, compute kernel lookup would throw if the imported shader asset was broken or stale, and compute mode was rewriting the indirect args header every frame. The draw path also used Unity 6 `RenderMeshIndirect`, while the prompt explicitly named `Graphics.DrawMeshInstancedIndirect`.
Solution: Added MonoImporter blocks to the two new C# metas, uploaded the staged 32-float room-water array immediately when `_damageRoomWaterBuffer` is created, resolved the compute kernel through `HasKernel(DamageHologramKernelName)` before `FindKernel`, changed the damage hologram draw submission to `Graphics.DrawMeshInstancedIndirect`, cached damage indirect args writes by mesh/instance count, and replaced compute shader reciprocal divisions with literal constants.
Rejected Alternatives: Letting Unity regenerate metas was rejected because it would churn GUIDs and break any references. Waiting for the next flood sequence was rejected because stale blue tint can persist indefinitely in a quiet scene. Keeping `RenderMeshIndirect` was technically valid, but rejected for prompt compliance.
Scalability potential: Low remains a seven-point no-compute glyph; Middle/High get correct first-frame flood tint; Ultra keeps the same bounded draw path and gains no new runtime branch.
Hardware Impact: Removes a per-frame `LockBufferForWrite`/args-header rewrite on stable compute frames after the first header upload, estimated 5-25 us CPU submission saved on low-end hardware. Adds one cold 32-float upload on buffer creation and one cheap cold `HasKernel` check.
Verification: Unity MCP validate_script on VehicleSubOsCockpitRuntime passed with 0 diagnostics after this continuation patch. Unity console currently reports unrelated `H8BinaryWorldPager` `_workerThread` and `WorkerShutdownJoinMilliseconds` errors; filter for VehicleSubOsCockpitRuntime and Diegetic returned 0 entries. Scoped shader scan now finds no `sqrt`, `normalize`, or ` / ` patterns in the two damage hologram shaders.

## Continuation Recheck - Assembly Coupling Trim

Problem: The new `Hecton8.UI.Diegetic` assembly referenced `Hecton8.Core.Contracts` even though its current files only need the local diegetic contracts assembly. In a 20-agent workspace, unused asmdef references increase compile fragility.
Solution: Removed the unused `Hecton8.Core.Contracts` reference from `Hecton8.UI.Diegetic.asmdef`, leaving only `Hecton8.UI.Diegetic.Contracts`.
Rejected Alternatives: Keeping the reference "for later" was rejected because speculative dependencies violate the isolation objective.
Scalability potential: Runtime cost is 0 either way; compile isolation is tighter and less likely to inherit unrelated contract churn.
Hardware Impact: Runtime 0 us. Editor compile graph is marginally smaller; exact compile-time gain unmeasured.
Verification: JSON asmdef syntax remains valid by inspection. Unity C# validation for the runtime remains 0 diagnostics; current console blockers are outside this diegetic UI assembly.

## Continuation Recheck - Sparse Proxy Visual Safety

Problem: The compute shader suppressed the cyan scanline whenever any active dent existed. With a sparse fallback/LOD3 proxy, a dent can be valid but not close enough to a proxy vertex, producing an empty hologram exactly when the cockpit should communicate subsystem activity.
Solution: Removed the `hasDamage` early-return branch. The shader now appends severity points for matched dent vertices, and still appends the cyan scanline sentinel for unmatched vertices. This is a diegetic diagnostic fake, not damage truth mutation. Replaced remaining owned damage-path reciprocal divisions in `VehicleSubOsCockpitRuntime` with literal constants (`DamageHologramFlickerSecondsInv`, `ButtonTravelSecondsInv`, `Hash24Inv`).
Rejected Alternatives: Increasing proxy density was rejected because it spends VRAM/GPU budget to hide a presentation logic flaw. CPU fallback markers were rejected because they violate the indirect GPU draw mandate. Suppressing the scanline during damage was rejected because it creates a false-negative visual when capped proxy vertices miss the dent radius.
Scalability potential: Low still uses the seven-point no-compute warning glyph. Middle/High keep the capped compute path and gain visual liveness even with sparse proxy data. Ultra can layer stronger glow/scan styling on top without changing the bounded append contract.
Hardware Impact: Runtime cost is one removed branch and the same scanline append predicate already used by the idle path. Estimated low-end impact is neutral to slightly faster in shader branch flow; visual reliability improves without raising the 512-point cap.
Verification: Prompt re-extracted from `CURRENT_BATCH.md`. Unity MCP `validate_script` on `VehicleSubOsCockpitRuntime.cs` passed with 0 diagnostics after this patch. Console filters for `VehicleSubOsCockpitRuntime` and `Hecton_DamageHologram` returned 0 entries. Current unfiltered Unity errors are unrelated: `H8MacroDatabaseService.ReadRootNodeOffsetIfOpen` duplicate member, `GlobalDataVault.ElapsedMillisecondsSince` missing helper, and shader errors in `Hecton_OrbitalDropReentryPlasma.shader` / `HectonVisorUberPost.shader`. Scoped scan found no owned damage-shader `hasDamage`, `sqrt`, `normalize`, CPU readback, Canvas, GameObject marker, or `Instantiate` path. Remaining slash-pattern hits in `VehicleSubOsCockpitRuntime` are legacy status text and button grid math outside the damage hologram path.

## Integration Requirement - Cockpit Asset Owner

Problem: Static search found no prefab or scene entry for `VehicleSubOsCockpitRuntime` under `Assets`. The runtime can auto-resolve the new compute shader/material through `AssetDatabase` in editor, but player builds require those references to be serialized by a real cockpit prefab/scene owner.
Solution: Keep the runtime fields serialized (`damageHologramCompute`, `damageHologramMaterial`, `damageProxyMeshLod3`) and document the integration requirement instead of inventing a synthetic scene dependency. The fallback proxy still protects development/runtime behavior when the LOD3 mesh is missing after the component is actually owned by a prefab/scene.
Rejected Alternatives: Creating a fake cockpit prefab was rejected because it would be an unauthorized presentation root with no guaranteed camera/power/layout ownership. Runtime `Resources.Load` was rejected because it hides integration errors and introduces a global asset path dependency. Shader.Find cannot solve compute shader inclusion.
Scalability potential: Low/Middle/High/Ultra behavior remains unchanged once the cockpit owner serializes the assets. The unresolved integration point is build inclusion, not the runtime algorithm.
Hardware Impact: Runtime 0 us. This is asset ownership hygiene; no frame-time change.
Verification: `rg` over `Assets` for `VehicleSubOsCockpitRuntime`, `damageHologramCompute`, `damageHologramMaterial`, and `damageProxyMeshLod3` found only script references, no prefab/scene serialization.

## Continuation Recheck - Hologram Shader Color Discipline

Problem: `Hecton_DamageHologramInstanced` computed proper red/yellow/cyan/blue colors, then multiplied the final RGB by red `_BaseColor.rgb` and by alpha while using `Blend SrcAlpha One`. This crushed cyan scanline and flooding blue into a dim red-biased output and double-applied alpha.
Solution: Keep `_BaseColor.a` as the material alpha scalar, output the computed color directly, and let `Blend SrcAlpha One` apply alpha once. Transform cube normals through `_HectonDamageHologramLocalToWorld` so rim intensity follows the diegetic dashboard anchor instead of the default mesh object transform.
Rejected Alternatives: Raising material alpha or glow was rejected because it hides incorrect color math and would still break cyan/blue semantics. Switching to premultiplied blend was rejected because existing material intent and Unity transparent queue already use `SrcAlpha One`.
Scalability potential: Low warning glyph becomes more legible without extra points. Middle/High scanline and flood tint read correctly at the same 512-point cap. Ultra can add glow/bloom downstream without fighting red-biased shader output.
Hardware Impact: Runtime cost is neutral; one RGB multiply is removed and one matrix-vector normal transform replaces Unity object normal transform. Estimated MX350 delta is below measurement noise, with higher visual contrast for the same draw call.
Verification: Scoped owned-file scan found no damage-shader `sqrt`, `normalize`, `hasDamage`, CPU readback, Canvas, GameObject marker, or `Instantiate` path. Remaining slash-pattern hits in `VehicleSubOsCockpitRuntime` are legacy status text and button grid math outside the hologram path.

## Continuation Recheck - Low Tier Warning Truthfulness

Problem: The MX350 fallback uploaded the red/yellow seven-point warning glyph once and kept it visible whenever powered, even with no known dents, flood, or impact. That is a cockpit false alarm and weakens the diagnostic language.
Solution: Keep the same seven-point buffer and indirect draw path, but switch glyph contents by state. Clean low-tier state uploads a cyan idle diagnostic line. Known dents, flooding, or active impact flicker upload the red/yellow exclamation glyph. Upload happens only when the state changes or the buffer is recreated.
Rejected Alternatives: Always showing the warning was rejected as false-positive UX. Running compute on low tier was rejected by the MX350 fallback mandate. Allocating separate idle/warning buffers was rejected because the existing seven-point staging array is sufficient.
Scalability potential: Low now communicates "system alive" without pretending damage exists. Middle/High remain compute-mapped. Ultra can add richer warning treatment downstream without changing low-tier data ownership.
Hardware Impact: Runtime cost is one boolean state check per visual render and a seven-Vector4 upload only when the low-tier state changes. GPU remains seven instances, no compute dispatch, no extra buffers.
Verification: Unity MCP `validate_script` on `VehicleSubOsCockpitRuntime.cs` passed with 0 diagnostics. Console filters for `VehicleSubOsCockpitRuntime` and `Hecton_DamageHologram` returned 0 entries after refresh/import. Current unfiltered Unity blocker is unrelated `HectonUnderwaterVisuals` interface mismatch plus entry-point errors. Scoped owned scan found no `foreach`, LINQ materializers, string formatting, scene search, `Instantiate`, `Debug.Log`, CPU readback, or Canvas path in the owned hologram files.

## Continuation Recheck - Low Tier Blackbox Disambiguation

Problem: After the low-tier glyph became state-aware, `HoloFlags` could say "low tier" but not whether the seven visible points represented cyan idle diagnostics or an active warning. A crash dump could misread idle liveness as damage presentation.
Solution: Add flag bit 32 when low tier is active and `IsLowTierDamageWarningActive()` is true. This records the semantic glyph state without CPU readback or extra telemetry fields.
Rejected Alternatives: Adding another telemetry struct field was rejected because one bit in the existing fixed-size flag word is enough. CPU readback of point severity was rejected because the state is already known before upload.
Scalability potential: Low dumps are now precise enough for postmortem review; Middle/High telemetry remains unchanged. Ultra has no added path.
Hardware Impact: One boolean check when recording telemetry. Estimated impact below 1 us CPU; no GPU or VRAM change.
Verification: Prompt re-extracted from `CURRENT_BATCH.md`. Unity MCP `validate_script` on `VehicleSubOsCockpitRuntime.cs` passed with 0 diagnostics. Console filters for `VehicleSubOsCockpitRuntime` and `Hecton_DamageHologram` returned 0 entries. `git diff --check` reports only existing LF-to-CRLF warnings.

## Continuation Recheck - Editor Wiring And Contract Surface

Problem: No cockpit prefab/scene owner currently serializes `VehicleSubOsCockpitRuntime`, and the diegetic contracts assembly had a read-model interface that no runtime implemented. This left future integration more likely to wire assets manually or inspect private fields.
Solution: Added cold editor `Reset`/`OnValidate` asset resolution so adding or validating the component auto-fills the owned radar/damage compute/material references in editor. Implemented `IDiegeticDamageHologramReadModel` on `VehicleSubOsCockpitRuntime` and exposed bounded read-only hologram state through the contract.
Rejected Alternatives: Runtime `Resources.Load` was rejected because it hides build inclusion mistakes and creates a global asset path dependency. A singleton read model was rejected because the prompt forbids new manager ownership. Leaving the contract unused was rejected because it weakens isolation.
Scalability potential: Runtime behavior is unchanged. Low/Middle/High/Ultra consumers can query the same contract without branching into implementation internals.
Hardware Impact: Runtime 0 us for editor auto-wiring. Contract properties are simple field reads and one flag mask; estimated below 1 us if queried. No allocation, no buffers, no draw change.
Verification: Unity MCP `validate_script` on `VehicleSubOsCockpitRuntime.cs` passed with 0 diagnostics after adding the contract interface. A later Unity refresh timed out twice, briefly reported idle, then `read_console` and `validate_script` failed due MCP ping/disconnect instability. `git diff --check` reports only existing LF-to-CRLF warnings.
