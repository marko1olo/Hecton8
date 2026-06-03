# Agent 1723 Rationale

Status hygiene: fresh file created; no previous Rationale_1723.md existed.

## Mandates Selected Before Coding
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- GPU_Compute_Warp_Sizing_Mobile.txt
- STRM_Async_Asset_Upload_Texture_Settings.txt
- TOOL_Procedural_Wreckage_Generator.txt

## Decision 01 - RB-109 Runtime Material Clone Removal
Problem: `WreckMaterialRegistry.ModuleBatch.EnsureRuntimeMaterial()` cloned a material with `new Material(runtimeShader)` and copied source properties. That path created one managed material object per BRG module batch and hid the real shader/material contract behind runtime substitution.
Solution: Runtime batches now hold only authored shared `Material` assets accepted by `ResolveSharedIndirectMaterial()`. BRG material handles are re-registered only when the shared asset reference changes. No runtime material ownership, clone, copy, shader repair, or destroy branch remains.
Rejected Alternatives: Kept-out alternatives were `new Material(source)`, `Object.Instantiate(material)`, `CopyPropertiesFromMaterial`, and runtime `Shader.Find` repair. Those preserve visual output by spending managed allocation and batch fragmentation; that is RB-109, not a fix.
Scalability potential: Low tier uses the same shared material route with lower baked texture sizes. Middle tier keeps shared materials and larger static masks. High and Ultra spend the saved runtime allocation/batch overhead on sharper offline corrosion textures, not runtime clones.
Hardware Impact: On i3/MX350, removing material clone/copy avoids cold runtime spikes during wreck publish and prevents material identity explosion. Estimated avoided cost is one managed material allocation plus property copy per active module batch, with steady-state player allocation at 0 B for the removed path.

## Decision 02 - Two-Slot Shared Wreck Material Pool
Problem: The prompt requires a static material pool equivalent to `MAT_Wreckage_Tier_0` and `MAT_Wreckage_Tier_1`. The existing registry exposed three legacy fallback materials and per-module overrides.
Solution: Added `wreckageTierSharedMaterials` with exactly two active slots. Essential maps to slot 0; Detail and Clutter map to slot 1. Per-module material overrides and old fallback fields are now legacy fallback routes only when the shared pool slot is empty. Publish builds a zero-allocation active module bitmask. The existing explicit `forceSingleDrawBatch` remains the only route allowed to collapse module contracts into one draw. When force-single-draw is disabled, duplicate active authored material references fail closed because this shader binds `_HectonWreckMatrices` and `_HectonWreckAges` on the material object.
Rejected Alternatives: Runtime material clones were rejected as RB-109. Auto-collapsing duplicate material contracts into one draw was rejected because different module meshes would be drawn through one mesh contract. Allowing separate BRG batches to share one material was also rejected because the last `SetBuffer` call would win and corrupt earlier batches.
Scalability potential: Low/Middle can assign one or two pooled materials. High/Ultra can assign richer materials/textures to the same two slots without increasing material identity count.
Hardware Impact: On MX350, the pool caps authored hot material variety when configured. This reduces SetPass/material state churn risk; exact draw count still depends on the existing `forceSingleDrawBatch` and BRG batch topology.

## Decision 03 - MRAO Contract Alignment
Problem: `Hecton_WreckIndirectLit.shader` previously documented and decoded `_MaskMap` as R Metallic, G AO, B Smoothness, A Emission. The batch prompt and existing `Hecton_MraoAtlasLit.shader` use MRAO: R Metallic, G Roughness, B AO, A Emission.
Solution: The new compute baker emits strict MRAO. The wreck indirect shader now decodes G as roughness, B as AO, and derives smoothness as `(1 - roughness) * _Smoothness`.
Rejected Alternatives: Reusing `HectonCoreLitDecodePackedMaskV1()` was rejected for this shader because it would silently feed roughness into AO and AO into smoothness. Changing the shared core decoder was rejected because other shaders may still depend on the old packed-mask contract.
Scalability potential: Low tier uses the same channel layout with smaller masks. Ultra tier can increase mask resolution without changing shader code or texture fetch count.
Hardware Impact: MRAO keeps the fragment path at one mask fetch for four material signals. On MX350, this avoids separate metallic/roughness/AO/emission textures and keeps the memory bus under control.

## Decision 04 - Offline Compute Baker Architecture
Problem: Material decay must be visual overkill without runtime pixel work. Runtime texture generation would violate the active player executable boundary.
Solution: `ChemicalRustBaker1723.cs` is an EditorWindow only. It dispatches `ChemicalRustBaker1723.compute` into a linear UAV target, reads back static PNG assets, applies TextureImporter settings, explicitly releases transient `RenderTexture` GPU state, then leaves runtime with pre-authored textures only.
Rejected Alternatives: CPU pixel loops and runtime procedural materials were rejected. CPU loops make 4K masks editor-slow; runtime generation violates zero-GC/player determinism. Temporary compute buffers were not added because the kernels write directly to UAV render textures and do not need persistent buffers.
Scalability potential: GlobalQualityWeight continuously drives albedo size 1024-4096, MRAO size 512-2048, and shader detail periods. Low, Middle, High, and Ultra are separate static outputs, not runtime branches.
Hardware Impact: Cheap devices consume smaller static assets. High-end devices consume higher-resolution static assets. The player pays texture fetch cost only; editor bake time is outside frame budget.

## Decision 05 - Hydraulic Drip And Corrosion Math
Problem: The rust must tile on X/Y and still suggest downward gravity streaking from joints/rivets. Naive non-periodic noise would seam on panels; per-pixel simulation is too expensive.
Solution: The compute shader uses periodic value-noise FBM, seam/rivet masks, curvature sampling, fixed fourth-power pitting via explicit multiplies, and a fixed 24-step downward streak accumulator. Each sample wraps with `frac()` so edges tile. The accumulator samples source pixels above the current UV, so stains travel downward from joints instead of pulling from lower pixels. Coordinate guards prevent out-of-bounds writes on ceil-rounded dispatch groups.
Rejected Alternatives: Sobel-on-texture postprocess, particle/fluid simulation, and shader `pow` for fixed exponent pitting were rejected. Sobel needs extra passes/textures. Fluid simulation wastes budget on fake chemistry that can be replaced by a deterministic directional mask. `pow(x, 4)` was replaced by two multiplications after FBM evaluation to avoid an unnecessary scalar math path.
Scalability potential: Low tier uses the same math at lower resolution. Ultra tier increases resolution and detail period through GlobalQualityWeight.
Hardware Impact: The algorithm is offline and amortized. Runtime cost is zero beyond sampling baked textures.

## Decision 06 - Importer, Validation, And Black Box
Problem: Generated masks entering the build uncompressed or with wrong color space would waste VRAM or corrupt PBR interpretation. A failed bake needs a proof artifact, not a console-only explanation.
Solution: The baker uses first-party `ProceduralTextureBaker.TryEnforceTextureImportSettings` to force Repeat wrap, sRGB for albedo, linear for MRAO, BC7 Standalone, ASTC_6x6 Android/iPhone, mipmaps, streaming mips, non-readable import, and audited platform settings. It validates pixel count, metallic coverage, roughness span, and AO span. The obsolete binary dump path was removed under the source-only proof directive.
Rejected Alternatives: Trusting Unity defaults, relying on manual inspector setup, or keeping duplicate local importer/audit methods was rejected. Defaults are not a contract and duplicated import logic drifts under multiple agents.
Scalability potential: Compression and wrap rules are identical across tiers; only static resolution changes with quality.
Hardware Impact: BC7/ASTC plus packed channels reduce texture memory and fetch pressure on MX350-class hardware.

## Decision 07 - Data Vault And GlobalRegistry Audit
Problem: The registry touches `GlobalDataVault` for BRG metadata. If compaction starts during handle acquisition, stale native access would corrupt rendering.
Solution: Existing code already checks `IsCompactionFenceActive` before/after generation handle resolution and again before write-lock use. The write lock is released in `finally`; no new job or pointer pass was added. `GlobalRegistry.Get<` was not found in `WreckMaterialRegistry.cs`, `SlowTick` no longer invokes cold resolver/component cache routes, no longer runs the general registration refresh during normal published-wreck steady-state, `TryRegisterLateFrameTick` requires real late-frame work, and Dispatcher/DataVault/Player availability is cached in cold lifecycle or hot-swap handlers instead of polling service locators during resource retry.
Rejected Alternatives: Adding a job, direct pointer cache, or hot `GlobalRegistry` fallback was rejected. The current route is cold owner-phase acquisition and fail-closed retry.
Scalability potential: Backoff behavior is identical on low and high tiers; quality only affects offline texture output.
Hardware Impact: No new runtime data-vault pressure was added. Existing compaction-safe path remains unchanged.

## Decision 08 - Compilation Gate
Problem: The batch asks for one `dotnet build`, but host load stayed above the strict threshold: 100 percent earlier, then 51, 74, 77, 74, 63, 87, 100, 97, 100, 100, 100, 97, and latest recheck 100 percent. Earlier checks found active `dotnet` processes; the latest check found one `dotnet` process and zero `csc` processes. `Assets/_Project/Editor/Hecton8.Project.Editor.asmdef` covers `Assets/_Project/Editor/Bakers`, but generated csproj files are stale and do not list `ChemicalRustBaker1723.cs`; Unity solution regeneration is required before dotnet can prove it.
Solution: Build was not launched. Static source sweeps and line-number evidence were produced instead.
Rejected Alternatives: Starting another compiler under 100 percent CPU was rejected by explicit batch rule.
Scalability potential: Not applicable to runtime quality; this is a host verification gate.
Hardware Impact: Avoided stealing CPU from other active agents/builds and avoided compounding compile contention.
