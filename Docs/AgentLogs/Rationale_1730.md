# Rationale 1730 - Lightmap & Reflection Probe Baking Automator

## Decision 001 - Domain File Missing

Problem: The prompt mandates `Docs/Actual Domains of Project.txt`, but that file is absent under `C:\hades\Hecton8\Docs` and no matching path was found by `rg`.
Solution: Treat the extracted XML role plus explicit writable directories as the active domain boundary: editor lighting automation, rendering audit, shader include audit only.
Rejected Alternatives: Editing scene/runtime surfaces outside the XML domain would be undocumented cross-domain expansion. Waiting for a missing file would leave the batch idle.
Scalability potential: Low keeps baked silhouettes and static probes; Middle/High/Ultra gain denser offline probes and sharper static HDR without runtime ownership drift.
Hardware Impact: i3/MX350 gains are from avoided runtime GI/probe refresh; exact runtime microseconds are PENDING PROFILER.

## Decision 002 - Mandate Selection

Problem: Lighting bake work touches editor tooling, rendering budgets, texture import settings, zero-GC runtime claims, and telemetry proof.
Solution: Read eight mandates: abyssal lighting, URP hot path, GPU sovereignty, async texture import, performance budgets, zero-GC, authoring bridge, telemetry.
Rejected Alternatives: Reading unrelated AI/physics mandates would add noise and increase risk of off-domain changes.
Scalability potential: Mandates define continuous quality lanes: compact baked/proxy, middle richer local baked data, high/ultra denser offline visual overkill.
Hardware Impact: Prevents MX350 fill-rate/probe-refresh debt; exact savings remain PENDING STATIC SCAN/PROFILER.

## Decision 003 - Editor-Only Authority

Problem: The task requires lightmap/probe generation but forbids runtime GI and per-frame reflection refresh.
Solution: Implement `LightmapBakerEngine.cs` as `EditorWindow` under `Assets/_Project/Editor/Lighting/`; use `#if UNITY_EDITOR` by folder/asmdef isolation and UnityEditor APIs only.
Rejected Alternatives: Runtime MonoBehaviour baker, runtime texture compression, scene-time probe `RenderProbe`, and player-build file writes all violate the batch and runtime budget laws.
Scalability potential: `_H8GlobalQualityWeight` will change offline bake resolution/probe density only, not gameplay truth or DTO layout.
Hardware Impact: Runtime path removes dynamic GI/probe refresh admission. Low-end benefit estimated as avoiding full extra cubemap renders; exact microseconds PENDING PROFILER.

## Decision 004 - Shader Contract From Existing Assets

Problem: The baker must not invent texture channels or lighting data contracts that shaders cannot consume.
Solution: Use existing static GI channels: Unity lightmaps for `TerrainMaster.shader`, dense custom light probe grid compatibility from `Hecton_CustomLightProbeGrid.hlsl`, and MRAO channel order from `Hecton_MraoAtlasLit.shader`.
Rejected Alternatives: New runtime GI buffer, guessed packed lighting atlas, or per-frame fish lighting pass would break one fact/one owner and add hot-path debt.
Scalability potential: Low uses lower atlas/probe budget; Middle raises samples; High/Ultra spend offline bake time on sharper HDR and denser probes without changing runtime truth.
Hardware Impact: i3/MX350 avoids a compensating runtime lighting pass. Estimated saved floor: 25 us/frame; profiler proof still required after Unity execution.

## Decision 005 - Probe Grid Math LOD

Problem: Dynamic fish need stable local lighting, but realtime probe updates are prohibited.
Solution: Generate a static LightProbeGroup with 5 m open-water cells and 2 m near-structure cells, capped by a continuous `_H8GlobalQualityWeight` probe budget.
Rejected Alternatives: Manual sparse probes are too easy to miss fish routes; realtime probe rendering violates the directive; per-fish lights are a frame-time tax.
Scalability potential: Low stays sparse but readable; Middle increases probe count; High/Ultra keeps the same route and burns offline bake time for visual density.
Hardware Impact: i3/MX350 receives baked SH samples instead of runtime cubemap/GI work. Estimated avoided suspicious work: 40 us/frame pending profiler.

## Decision 006 - Hot Registry Audit Scope

Problem: Lighting automation must not introduce hot dependency polling into rendering.
Solution: Keep `LightmapBakerEngine` in editor assembly and run a narrow `rg` sweep for runtime `GlobalRegistry.Get<` under rendering scripts.
Rejected Alternatives: Adding a runtime lighting registry resolver would create cold identity calls inside hot rendering flow.
Scalability potential: All quality tiers use the same baked data route; only offline asset density changes.
Hardware Impact: Avoids lookup churn and hidden allocations. Estimated avoided floor: 5 us/frame on low-end silicon.

## Decision 007 - Compaction Fence Non-Interference

Problem: Lighting work must not create a new render-time DataVault reader that can race the compaction fence.
Solution: Keep the baker completely editor-only and verify existing render bridges already back off on `IsCompactionFenceActive` and wrap handle resolution in mutation/read guards.
Rejected Alternatives: Runtime baked-light registry bridge, cached native aliases across frames, or Burst job lighting uploads would require new pinning and fence proof with no need.
Scalability potential: Low/Middle/High/Ultra all consume serialized Unity lighting assets; quality changes are offline asset density only.
Hardware Impact: No new runtime DataVault read, no stale pointer exposure, 0 us/frame added on i3/MX350.

## Decision 008 - Editor-Local Proof Over Runtime Evidence Debt

Problem: The latest directive rejects bloated JSON reports and binary telemetry dumps; stale disk artifacts would create false proof.
Solution: Keep `BakeReport` as editor-local status and source-visible validation counters only; proof is the compiling source, Unity `validate_script` diagnostics, forbidden-token scans, and generated asset import code.
Rejected Alternatives: Runtime report parsing, JSON telemetry dumps, and stale source-hash reports were rejected as I/O debt and false authority.
Scalability potential: Source-visible quality profile maps continuous weight to atlas/probe resolutions for every tier.
Hardware Impact: No report file write; 0 us/frame runtime cost.

## Decision 009 - Progressive GPU Lightmapper Route

Problem: The target scenes need GI quality without runtime GI.
Solution: Force baked GI and Progressive GPU lightmapper settings through UnityEditor APIs; set static renderers to contribute GI and lights to baked mode.
Rejected Alternatives: Runtime Realtime GI, mixed lights that keep runtime cost, or shader fake AO as the primary GI source.
Scalability potential: Low 1024 atlases/256 reflections; Middle larger samples; High/Ultra 4096 atlases/1024 reflections with the same gameplay route.
Hardware Impact: Removes runtime GI admission. Estimated avoided floor: 100 us/frame on MX350-class hardware pending profiler.

## Decision 010 - Static Reflection Atlas Route

Problem: Per-frame ReflectionProbe refresh is forbidden and separate unmanaged reflection outputs inflate runtime fetch complexity.
Solution: Force probes to baked/ViaScripting, bake with `Lightmapping.BakeReflectionProbe`, import as BC6H cubemaps, and pack baked cubemaps into one scene-level `CubemapArray` atlas asset.
Rejected Alternatives: `ReflectionProbeRefreshMode.EveryFrame`, runtime `RenderProbe()`, and uncompressed standalone HDR cubemaps were rejected as runtime and VRAM debt.
Scalability potential: Low uses 256 px cubemaps; Middle/High scale upward; Ultra reaches 1024 px while preserving the same static atlas route.
Hardware Impact: Removes cubemap render work from MX350 frame path. Estimated avoided floor: 100 us/frame; BC6H reduces reflection texture memory by 87.5% versus RGBAHalf.

## Decision 011 - Seam Stitching As Bake-Time Contract

Problem: Base walls cannot show lightmap seam scars, but shader-side seam hiding would add runtime instructions.
Solution: Enable Unity seam stitching and q-scaled padding in the Progressive bake settings, then block UV overlap before writing assets.
Rejected Alternatives: Runtime dither/fog seam cover, duplicate geometry decals, or post-process darkening would hide symptoms while burning frame time.
Scalability potential: Low uses 2 px padding; Middle/High/Ultra scale padding up to 8 px and sample counts without changing runtime code.
Hardware Impact: 0 us/frame runtime. The cost is offline bake time only.

## Decision 012 - BC6H Import Enforcement

Problem: Raw HDR lightmaps and cubemaps can explode MX350 VRAM.
Solution: Force importer `sRGBTexture=false`, `mipmapEnabled=true`, `wrapMode=Clamp`, `CompressedHQ`, and `TextureImporterFormat.BC6H` on generated EXR/HDR assets.
Rejected Alternatives: RGBAHalf raw assets, sRGB import, repeat wrap, or runtime compression all violate the static-data budget.
Scalability potential: Low/Middle/High/Ultra all use BC6H; quality weight changes resolution only.
Hardware Impact: A 4096 atlas is 16 MiB BC6H instead of 128 MiB RGBAHalf. Five 4K atlases are roughly 80 MiB, under the 110 MiB proof ceiling.

## Decision 013 - Offline UV Gate

Problem: Overlapping lightmap UVs create black spots and invalid bake artifacts.
Solution: Validate UV2 range and approximate cell overlap before saving; abort with fatal report and `Debug.LogError` on violation.
Rejected Alternatives: Letting Unity bake corrupt atlases or covering artifacts with runtime effects.
Scalability potential: Same validator applies to every tier; only atlas size and sample count change.
Hardware Impact: 0 us/frame runtime, prevents broken assets entering builds.

## Decision 014 - Mental Dry Run And Cleanup Guard

Problem: A massive deep-sea base bake can fail if overlapping UVs, low padding, or interrupted GPU bake state is ignored.
Solution: Execution trace: scene opens, realtime GI is disabled, static renderers/lights are marked baked, UV2 is validated, probes are generated, Progressive GPU bake runs, assets are imported, then scenes are saved. Added editor-window cleanup that reflects `Lightmapping.isRunning` and calls `Lightmapping.Cancel()` on disable.
Rejected Alternatives: Starting a bake without UV validation, relying on runtime fog to hide black spots, or leaving editor bake work dangling on window close.
Scalability potential: Low keeps 1024 atlases with small samples; Middle raises samples; High/Ultra use 4096 atlas, larger samples, and denser probes. All routes stay offline.
Hardware Impact: 0 us/frame runtime; avoids corrupted baked assets entering MX350 path.

## Decision 015 - Continuous Quality Scalar

Problem: Binary quality switches are forbidden.
Solution: `_H8GlobalQualityWeight` feeds smoothstep q into atlas resolution, reflection resolution, sample counts, bounces, padding, AO, per-renderer lightmap scale, and probe budget.
Rejected Alternatives: Low/Ultra enum presets, hardcoded 4K-only outputs, or changing runtime gameplay truth by quality tier.
Scalability potential: Low = 1024 lightmaps/256 reflection survival; Middle = increased samples/probes; High = 4096/1024; Ultra = max offline visual overkill with the same runtime asset route.
Hardware Impact: Runtime cost remains static; low-end VRAM survives by lower generated asset resolution.

## Decision 016 - Build Gate Block

Problem: The batch requests `dotnet build`, but the AGENTS gate forbids build when CPU exceeds 50% or another compiler process exists.
Solution: Latest sample found CPU 100% with Unity asset import workers, shader compilers, and Roslyn compiler server active. No build launched. Unity `validate_script` and `git diff --check` passed for the touched source.
Rejected Alternatives: Launching another build would violate the explicit CPU/compiler mandate and create false verification.
Scalability potential: Build proof is independent of quality tier and must be retried only under valid host conditions.
Hardware Impact: Avoided saturating host CPU; no runtime impact.

## Decision 017 - Compaction Race Response

Problem: If the defragmenter raises the compaction fence one instruction before rendering reads shader globals, stale native aliases would corrupt lighting.
Solution: This agent added no runtime DataVault path. Existing `GlobalShaderDispatcher` and `HectonShaderGlobalDataVaultBridge` check `IsCompactionFenceActive` before resolving handles and retry next tick through cached slot invalidation.
Rejected Alternatives: Holding NativeArray aliases across frames or adding baked-light native pointers into render jobs.
Scalability potential: Same backoff behavior for all quality tiers.
Hardware Impact: 0 us/frame added; prevents undefined memory reads on low-end hardware.

## Decision 018 - Zero-GC Runtime Scope

Problem: The baker uses editor allocations, but runtime steady-state GI must stay 0B managed allocations.
Solution: Scope the 0B claim to player runtime lighting/reflection sampling: serialized lightmaps, baked probes, and static probe groups are consumed by Unity without this editor type entering the player assembly.
Rejected Alternatives: Runtime `Lightmapping`, runtime `ReflectionProbe.Render`, runtime JSON parsing, or runtime DataVault lighting uploads.
Scalability potential: Low/Middle/High/Ultra alter baked asset resolution only; runtime sampling contract remains static.
Hardware Impact: 0B managed allocation and 0 us/frame added in steady-state GI/reflection path.

## Decision 019 - VRAM Budget Proof

Problem: HDR lighting can exceed the MX350 1800 MiB ceiling if stored raw.
Solution: Use BC6H proof math: 4096x4096 = 16,777,216 pixels; BC6H stores 16 bytes per 4x4 block; 1,048,576 blocks = 16 MiB per atlas; five atlases = 80 MiB.
Rejected Alternatives: RGBAHalf raw at 128 MiB per atlas, five atlases = 640 MiB; too much budget for static lighting.
Scalability potential: Low uses smaller atlases; Middle/High/Ultra remain within BC6H compression.
Hardware Impact: Five 4K atlases save roughly 560 MiB VRAM versus RGBAHalf raw.

## Decision 020 - Final Proof Route

Problem: Final acceptance requires proof, but current user directive forbids generated JSON report files.
Solution: Use source patches plus Unity `validate_script` and static scans as proof; no `LIGHTMAP_BAKER_REPORT_1730.json` is emitted or retained.
Rejected Alternatives: No-op "done" report, stale prose, generated JSON, or claiming compile success under a blocked CPU/compiler gate.
Scalability potential: Tier-relevant bounds remain explicit in `BakeQualityProfile.FromWeight`.
Hardware Impact: 0 us/frame runtime; avoids report I/O and preserves source-level audit path for the next integrator.

## Decision 021 - Static Candidate And Dry-Run Discipline

Problem: The editor baker was too aggressive: dry-run still mutated scene lighting settings, and the static renderer pass could mark every `MeshRenderer` as GI/static, including authored dynamic objects.
Solution: Dry-run now executes UV validation, input audit, and probe math only; bake mutation is after the dry-run branch. Renderer/light mutation now requires `gameObject.isStatic` or existing static authoring flags. Lighting asset copying uses `File.Copy` instead of loading full HDR files into managed byte arrays.
Rejected Alternatives: Global scene-wide static marking was rejected because it can break gameplay objects and produce false UV failures on dynamic meshes. `File.ReadAllBytes` was rejected for baker outputs because 4K EXR/HDR copies do not need managed mirrors.
Scalability potential: Low keeps only authored static lighting in the bake; Middle/High/Ultra increase offline density without converting dynamic actors into baked geometry.
Hardware Impact: Prevents runtime/design regressions and removes editor-side peak managed memory during generated lightmap/probe asset copies.

## Decision 022 - UI Toolkit Facade Hygiene

Problem: The new editor baker used IMGUI `OnGUI()`, while `AGENTS.md` forbids `OnGUI()` and nearby lighting tools already use UI Toolkit.
Solution: Replaced the baker facade with `CreateGUI()`, `Slider`, `SliderInt`, `Toggle`, `Button`, and `Label` UI Toolkit controls. Bake commands still call the same dry-run/bake methods; runtime and bake algorithms were not refactored.
Rejected Alternatives: Leaving editor-only IMGUI was rejected because the file is new and under our control. Rewriting unrelated old IMGUI editor windows was rejected as off-domain churn.
Scalability potential: Low/Middle/High/Ultra bake settings remain continuous through `_H8GlobalQualityWeight`; the facade change only prevents editor architecture drift.
Hardware Impact: 0 us/frame runtime. Editor allocation is cold UI-only and excluded from player builds.
