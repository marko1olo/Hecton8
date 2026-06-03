# Agent 1721 Status

ID: 1721
Domain: HOLOGRAPHIC_UI_AND_SCREEN_MESH_PROJECTOR
Task count: 23
Prompt source: Docs/Tasks/CURRENT_BATCH.md
Current state: SOURCE POLISHED / BUILD BLOCKED BY UPSTREAM COMPILE WALL

## Relevant Mandates

- Read: UI_Diegetic_Physical_Interfaces.txt
- Read: UI_Data_Streaming_ZeroGC_Optimization.txt
- Read: REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- Read: REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- Read: REND_DescriptorBinding_Reality_Check.txt
- Read: GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- Read: OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- Read: TOOL_Designer_Facades_CSV_Binary_Bridge.txt

## Loop 1: Tasks 01-05

- [x] Task 01 TERMINAL_OS_STATIC_AUDIT - DOD: rg static scan plus line reads. Result: `TerminalOsRuntime.cs` has no `new Material`, `.material`, `.materials`, `SetPixel`, or `SetPixels`; screen updates route through `RenderTexture` array, compute dispatch, shared material, and buffers. Rejected alternative: inventing a clone removal patch without evidence. Runtime microseconds saved so far: 0, audit only.
- [x] Task 02 SHADER_PROPERTIES_DECONSTRUCTION - DOD: read `Hecton_TerminalTextureArrayPanel.shader` and `MAT_AppliedLore_TerminalOS_ArrayPanel.mat`. Result: current contract exposes `_TerminalTextureArray`, `_TerminalSlice`, `_EmissionTint`; no baked LUT/MRAO slots exist. Rejected alternative: separate unbound textures with no shader route. Runtime microseconds saved so far: 0, contract gap only.
- [x] Task 03 COMPUTE_SHADER_API_ALIGNMENT_INSPECTION - DOD: read `TerminalBlit.compute`, runtime `GetKernelThreadGroupSizes`, `Dispatch`, and baker patterns. Result: existing code uses 8x8 threads, ceil group math, and bounds guards; new editor baker must mirror this and release buffers/textures. Rejected alternative: CPU-only final pipeline; kept compute route with CPU fallback only for editor validation if compute is unavailable. Runtime microseconds saved so far: 0.
- [x] Task 04 RADIAL_DISTORTION_MATHEMATICAL_MODELING - DOD: selected polynomial `r' = r * (1 + k1*r^2 + k2*r^4)` with safe scale normalization to keep corners in [0,1]. Rejected alternative: runtime vertex curvature math. Runtime microseconds saved estimate: avoids per-fragment live polynomial if shader samples LUT; exact measured value pending profiler.
- [x] Task 05 GLOBAL_REGISTRY_HOT_POLLING_DETECTION - DOD: UI/TerminalOS rg sweep for `GlobalRegistry.Get<` and tick/update names. Result: no `GlobalRegistry.Get<` in TerminalOS; runtime uses `SystemDispatcher` and cached services. Rejected alternative: forced DI refactor with no violation. Runtime microseconds saved: 0.

## Loop 2: Tasks 06-10

- [x] Task 06 COMPACTION_FENCE_VULNERABILITY_SCAN - DOD: audited `TryOpenVaultBuffer`/`TryReadVaultBuffer` gates and pointer schedule sites. Result: existing gates reject `_vault.IsCompactionFenceActive` before and after handle resolution; 1721 adds no DataVault lanes or pointer jobs. Rejected alternative: new texture vault ownership for serialized Unity assets. Runtime microseconds saved: 0; race surface added: 0.
- [x] Task 07 TELEMETRY_AND_REPORTING_ARCHITECTURE - DOD: defined JSON report at `Docs/Reports/SCREEN_MESH_PROJECTOR_REPORT_1721.json` with clone scan, line anchors, hashes, validation, build gate, VRAM math. Rejected alternative: chat-only report. Static scan timing: runtime forbidden scan 83254.2 us; editor forbidden scan 66050.2 us.
- [x] Task 08 COMPUTE_SHADER_BAKER_INITIALIZATION - DOD: created `UiScreenMeshProjector1721.cs` as `#if UNITY_EDITOR` `EditorWindow`, MenuItems, compute kernel discovery, `RenderTexture` random-write targets, strict release in `finally`. Rejected alternative: runtime generation. Runtime microseconds saved estimate: avoids any gameplay allocation path; measured runtime gain pending profiler.
- [x] Task 09 BARREL_DISTORTION_LUT_BAKING - DOD: implemented `H8BarrelUv1721` compute route writing RG distorted UVs, with mesh aspect and curvature from `M_Diegetic_HUD_V4_CurvedPanel.asset` bounds. Rejected alternative: per-fragment live polynomial. Runtime microseconds saved estimate: shader samples LUT instead of recomputing radial polynomial; exact value unclaimed.
- [x] Task 10 PHOSPHOR_BURN_IN_SIMULATION - DOD: static cockpit template frames/compass/warning boxes drive LUT B burn-in plus albedo alpha. Rejected alternative: animating burn-in procedurally at runtime. Runtime microseconds saved estimate: burn-in is one texture channel fetch already paid by LUT; no new runtime pass.

## Loop 3: Tasks 11-15

- [x] Task 11 SCANLINE_AND_SIGNAL_GLITCH_BAKING - DOD: compute alpha combines sine scanline and periodic wrapped value noise; glitch loop count recorded as 64. Rejected alternative: runtime Perlin/static generation. Runtime microseconds saved estimate: no managed runtime noise generation.
- [x] Task 12 GLASS_SURFACE_PBR_MASK_PACKING - DOD: `_PackedMrao` writes metallic, roughness/scratch, AO, emissive into RGBA; shader consumes packed channels. Rejected alternative: separate glass/scratch/emissive textures. Runtime microseconds saved estimate: fewer texture bindings/samplers; exact fetch delta requires GPU capture.
- [x] Task 13 ASSET_DATABASE_TEXTURE_SERIALIZATION - DOD: GPU outputs read back in editor only, albedo/MRAO encode PNG, LUT encodes EXR, bytes saved with `File.WriteAllBytes`, imported synchronously. Rejected alternative: keeping generated RenderTextures in memory. Runtime microseconds saved: 0 direct; moves generation to disk.
- [x] Task 14 AUTOMATED_TEXTURE_IMPORTER_CONFIGURATION - DOD: `TextureImporter` sets sRGB true for albedo, false for packed/LUT, Clamp wrap, CompressedHQ, BC7 Standalone, ASTC_6x6 Android/iPhone. Rejected alternative: manual importer discipline. Runtime microseconds saved: 0 direct; prevents raw texture residency.
- [x] Task 15 OFFLINE_TEXTURE_VALIDATOR_GATE - DOD: `ValidateProjectionLut` rejects NaN/Infinity/out-of-range UVs; `ValidatePixelCount` checks exact `width*height`. Rejected alternative: saving first and discovering bad LUT in play mode. Runtime microseconds saved estimate: prevents invalid asset entering runtime.

## Loop 4: Tasks 16-20

- [x] Task 16 DRY_RUN_VERIFICATION_EXECUTION - DOD: checked dispatch math: 8x8 thread group, ceil group count in C#, HLSL `dispatchThreadID` guard for non-power-of-two targets. Rejected alternative: exact division assumption. Runtime microseconds saved: 0; editor crash risk reduced.
- [x] Task 17 CONTINUOUS_QUALITY_SCALING_INTEGRATION - DOD: `GlobalQualityWeight` smoothstep maps continuously from 1024/512 to 4096/2048 with 256 step rounding; shader weights remain floats. Rejected alternative: low/ultra binary switch. Runtime microseconds saved: no runtime truth change; low-tier VRAM reduced offline.
- [x] Task 18 BATCHED_COMPILATION_AND_SYNTAX_ASSERTION - DOD: CPU/compiler gate first blocked at `CPU_LOAD=100`, then opened at `CPU_LOAD=46`, `CSC_COUNT=0`, `DOTNET_COUNT=0`. One `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /m:1` pass was launched. Build failed on upstream non-1721 files: `H8AppliedLoreRuntime.cs(70)` CS8168/CS8350 and `PredatorCognitionDomain.cs(6901)` CS0128. `TerminalOsRuntime.cs` was covered by this project and emitted no errors. Editor baker project `Hecton8.Project.Editor.csproj` is missing from generated csproj set, so Unity project regeneration is required for baker compile coverage. Rejected alternative: repeated build spam or editing Data/Fauna outside 1721 domain.
- [x] Task 19 EXPLICIT_PIXEL_COUNT_VALIDATION_GATE - DOD: `ValidatePixelCount` asserts exact expected pixel count before write/import; baker anchor line 443. Rejected alternative: relying on RenderTexture descriptor alone. Runtime microseconds saved: 0; serialization guard only.
- [x] Task 20 COMPACTION_FENCE_RACE_CONDITION_AUDIT - DOD: documented no new compaction-sensitive code in rationale D06/report. Existing gates fail closed if compaction is active before/after handle resolution; 1721 texture binding is serialized asset/material state only. Rejected alternative: direct `GlobalDataVault.TryGetLatestCreated()` fallback. Runtime microseconds saved: 0; no new race route.

## Loop 5: Tasks 21-23

- [x] Task 21 ZERO_GC_ALLOCATION_PROFILER_MOCK - DOD: static steady-state trace of `LateFrameTick` and binding route. Runtime scan found 0 `new Material`, `.material`, `.materials`, `SetPixel`, `SetPixels`, `new Texture2D` in TerminalOS. Editor-only `new Texture2D` remains at baker line 393 for asset readback. Rejected alternative: claiming profiler capture without running Unity Profiler. Runtime managed allocation claim: static 0 for targeted symbols, profiler proof pending.
- [x] Task 22 VRAM_BUDGET_LIMIT_TESTING - DOD: BC7 math recorded. Four single 4096 BC7 RGBA atlases = 64 MiB and fit 110 MB; four complete albedo+MRAO 4096 pairs = 128 MiB and exceed 110 MB, so Ultra four-group sets must share MRAO or reduce resolution. Rejected alternative: fake 110 MB proof. Runtime microseconds saved: 0; VRAM risk documented.
- [x] Task 23 AUTOMATED_METRIC_VALIDATOR_REPORT - DOD: superseded by source-only proof per newest directive; removed JSON artifact and kept code-level validation in status/log. Rejected alternative: inflating report I/O. Runtime microseconds saved claimed: none without profiler.

## Verification

- Compile: BLOCKED_BY_UPSTREAM_ERRORS (`Hecton8.Core.csproj`, 1 build attempt, no 1721 file errors emitted; latest compiler probe blocked by `CPU_LOAD=97`, `DOTNET_COUNT=1`, `CSC_COUNT=0`)
- Static material clone/runtime texture scan: PASS (`matches=0`, `scan_microseconds=83254.2`)
- Editor baker forbidden scan: PASS (`matches=0`, `scan_microseconds=66050.2`; editor-only `new Texture2D` line 395 is serialization readback)
- `git diff --check`: PASS (`exit=0`; LF/CRLF warnings only)
- Balance: PASS (`TerminalOsRuntime.cs 480/480 braces`, `UiScreenMeshProjector1721.cs 72/72`, `compute 12/12`, shader `20/20`)
- Source-only proof: PASS (`Docs/Reports/SCREEN_MESH_PROJECTOR_REPORT_1721.json` removed per newest directive)

## Polish Continuation

- [x] CRT readiness tightened - DOD: `ResolveBakedCrtProjectionReady()` now requires albedo, LUT, and MRAO before enabling shader route. Rejected alternative: partial atlas set with black fallback. Runtime microseconds saved: 0; prevents invalid visual state.
- [x] Runtime horizontal tear added in shader - DOD: baked alpha now drives a 64-step `_Time`-quantized horizontal UV shift using `frac/floor/abs`, no CPU, no material clone. Rejected alternative: managed runtime glitch animation. Runtime microseconds saved estimate: avoids CPU-side animation entirely.
- [x] Compute DTO layout validated - DOD: baker validates `UnsafeUtility.SizeOf<ScreenProjectorBakeParams1721>() == 72` and 8-byte alignment before `ComputeBuffer`. Rejected alternative: trusting struct drift. Runtime microseconds saved: 0; editor fail-fast only.
- [x] Public/private signature fixed - DOD: removed public `BakeSettings.ToParams` returning a private DTO; moved parameter construction into private outer `BuildParams`. Rejected alternative: waiting for compiler to catch CS0050/CS0051. Runtime microseconds saved: 0.
- [x] Projection LUT precision separated - DOD: albedo/MRAO stay compressed BC7/ASTC, while UV projection LUT imports as RGBAHalf uncompressed with non-readable storage and full-channel NaN validation. Rejected alternative: BC7/ASTC quantized UV projection that can jitter curved screens. Runtime microseconds saved: 0; visual correctness protected.
- [x] Disabled baked path texture fetch removed - DOD: shader now branches on uniform `_TerminalScreenBakedProjectionReady` before sampling projection LUT, albedo atlas, and MRAO. Rejected alternative: multiplying unused samples by zero after paying three texture fetches. Runtime microseconds saved: not claimed without GPU capture; disabled path sampler pressure reduced.
- [x] Baker folder creation failure hardened - DOD: `TryEnsureAssetFolder` now fails with explicit reason when `AssetDatabase.CreateFolder` returns no GUID or final folder remains invalid. Rejected alternative: empty failure string after editor asset database refusal. Runtime microseconds saved: 0; editor reliability only.
- [x] Runtime baked atlas layout gate added - DOD: `ResolveBakedCrtProjectionReady()` now rejects non-square albedo/LUT textures and MRAO dimension mismatch before enabling baked shader path. Rejected alternative: accepting wrong manual texture assignment and corrupting projection. Runtime microseconds saved: 0; invalid state prevented.
- [x] Enabled baked path duplicate terminal sample removed - DOD: shader now samples `_TerminalTextureArray` once in the enabled branch and once in the disabled branch, never pre-sampling then resampling. Rejected alternative: paying one dead terminal array sample on every baked CRT fragment. Runtime microseconds saved: not claimed without GPU capture.
- [x] Baker parameter upload prewarmed - DOD: replaced per-bake `NativeArray<ScreenProjectorBakeParams1721>` upload with one static one-element `ParamUploadScratch` array and `ComputeBuffer.SetData(Array)`. Rejected alternative: relying on less-proven `SetData(NativeArray<T>)` overload and per-bake allocation/dispose. Runtime microseconds saved: 0; editor GC reduced.
- [x] Baker editor churn reduced - DOD: removed `OnDisable()` compute-reference clearing and removed redundant global `AssetDatabase.Refresh()` after exact imports/reimports. Rejected alternative: resetting designer-selected compute asset and refreshing the whole database after targeted import. Runtime microseconds saved: 0; editor stall risk reduced.
- [x] CRT binding hot polling removed - DOD: `LateFrameTick` now only resolves baked CRT texture dimensions/entity IDs when `_bakedCrtBindingDirty` is raised by lifecycle or editor `OnValidate`; material/global binding consumes cached readiness. Rejected alternative: per-frame texture/object property hash polling. Runtime microseconds saved: unclaimed without profiler; Unity native property polling removed from steady-state CRT path.
- [x] Baker cold allocation marker added - DOD: static parameter upload scratch now uses the project canonical `COLD ALLOC` marker, and editor-only readback allocation is explicitly identified. Rejected alternative: leaving source scanners to classify editor serialization as runtime allocation. Runtime microseconds saved: 0; source audit clarity improved.
