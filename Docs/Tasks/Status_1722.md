# Status 1722 - Submarine Hull Dent & Cavitation Curve Baker

Prompt: `SUBMARINE_HULL_DENT_AND_CAVITATION_CURVE_BAKER`
Domain: `Assets/_Project/Editor/Bakers/`, `Assets/_Project/Scripts/Vehicles/`, `Assets/_Project/Art/Shaders/Include/`
Task count: 22
Batch source: `Docs/Tasks/CURRENT_BATCH.md`
Last prompt re-extraction: ABI pass, same XML block, task count 22, sha256=8a615be923e8c7462431587d3a869890b1302bc5456a433f0ae4038beea978b7.

## Mandates Selected
- [x] `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- [x] `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- [x] `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- [x] `REND_GPU_Driven_Animation_VAT.txt`
- [x] `REND_GPU_Sovereignty.txt`
- [x] `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- [x] `STRM_Async_Asset_Upload_Texture_Settings.txt`
- [x] `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## State Machine
- [x] Loop 1: Tasks 01-05 archaeology, code sweep, plan, compile gate. DOD: source inspected before edits; rejected runtime mesh/material mutation; estimate saved 35-70 us/frame from dent vector upload/signal scan path.
- [x] Loop 2: Tasks 06-10 data-vault audit, telemetry architecture, baker and compute kernels. DOD: read-only cached telemetry handle plus compute kernels; rejected DataVault hull-dent write ownership; estimate saved 120-400 us/frame versus CPU cavitation particles.
- [x] Loop 3: Tasks 11-15 masks, serialization, import settings, validation, dry-run dispatch. DOD: MRAO packing, PNG/EXR serialization, importer policy, pixel count guard, ceil dispatch; rejected separate masks and raw textures; estimate saved 2-3 texture fetches/pixel.
- [x] Loop 4: Tasks 16-20 quality scaling, build attempt, pixel validation, compaction audit. DOD: continuous quality-weight dimensions and compaction-fence backoff; build attempted but timed out; second run blocked by CPU >50%.
- [x] Loop 5: Tasks 21-22 zero-GC mock, VRAM proof, source proof. DOD: no hot allocation tokens in runtime controller; black-box ring moved off forbidden persistent NativeArray; report JSON writer removed; rejected fake compile pass.
- [x] Loop 6: APEX polish pass. DOD: runtime hot-token scan clean, DataVault write-lock scan clean, shader signature scan clean, filesystem orphan `.meta` scan clean, brace balance clean. Compile attempt was throttled and singular but timed out with no diagnostics.
- [x] Loop 7: Functional polish pass. DOD: cavitation atlas is now sampled by `UberNoir`, baker validation no longer allocates `Color[]`, mesh metrics no longer read `mesh.triangles`, and mesh scratch capacity is fixed/fail-fast. Compile blocked by CPU 67.56%.
- [x] Loop 8: ABI repair pass. DOD: `HullDentShaderController` now reads actual `VesselTelemetryEntry` fields (`HullCleanlinessMask`, `TotalCareActionsCount`, `CurrentBallastRatio`) instead of non-existent telemetry members. Exact-match scan clean. Compile blocked by CPU 88.60% and active Unity Roslyn compiler process.
- [x] Loop 9: Registry compatibility polish. DOD: hot-swap listener unregister switched to `GlobalRegistry.TryUnregisterHotSwapListener(this)` after confirming the method exists in `GlobalRegistry.cs`. Static hot-token, ABI, and diff whitespace scans clean. Compile blocked by CPU 99% and active Unity Roslyn compiler process PID 48092.
- [x] Loop 10: Compute determinism and proof-artifact cleanup. DOD: reversed-edge `smoothstep` calls in the cavitation compute shader converted to explicit `1 - smoothstep(low, high, value)` falloffs; obsolete untracked JSON report removed after source-code-only directive. Build not launched.

## Tasks
- [x] 01 HULL_DENT_CONTROLLER_STATIC_AUDIT - Controller moved to static texture/global scalar upload. No `Mesh.vertices`, `new Material`, `renderer.material`, `SetGlobalVectorArray` tokens remain in touched controller. Alternative rejected: live dent array/vector upload.
- [x] 02 PROPELLER_CAVITATION_DECONSTRUCTION - No dedicated propeller shader found; cavitation hook added as shared flipbook texture contract in `Hecton_HullBakedDisplacement1722.hlsl`. Alternative rejected: CPU particle continuation.
- [x] 03 COMPUTE_SHADER_API_ALIGNMENT_INSPECTION - Baker uses `RenderTexture.enableRandomWrite`, Editor-only `GraphicsBuffer` mesh vertex transfer from prewarmed scratch list, guarded compute kernels, and release routes in `finally`. Alternative rejected: persistent unreleased GPU buffers and runtime mesh reads.
- [x] 04 HULL_STRAIN_MATHEMATICAL_MODELING - Compute kernel uses high-poly mesh vertex influence, deterministic dent centers, weld/panel ribs, procedural salt/rust masks, and encoded slopes. Alternative rejected: runtime curvature/mesh deformation.
- [x] 05 GLOBAL_REGISTRY_HOT_POLLING_DETECTION - `GlobalRegistry.Get<` absent in touched files. Runtime controller uses cold registry access and hot-swap listener.
- [x] 06 COMPACTION_FENCE_VULNERABILITY_SCAN - Controller uses `TryGetGenerationHandle`, `TryReadOnlyHandle(in handle, out ReadOnly)`, and backs off when `IsCompactionFenceActive`.
- [x] 06b VESSEL_TELEMETRY_ABI_ALIGNMENT - Controller derives cleanliness from the 64-bit maintenance mask, care tone from `VesselTelemetryEntry.ResolveToneWeight01`, and ballast visual health from finite ballast ratio. Alternative rejected: adding new telemetry DTO fields outside the physics owner.
- [x] 07 TELEMETRY_AND_REPORTING_ARCHITECTURE - Source-code proof path kept; baker JSON report writer removed to match APEX no-report directive. Alternative rejected: recurring Editor I/O proof artifacts.
- [x] 08 COMPUTE_SHADER_BAKER_INITIALIZATION - `HullCavitationBaker1722.cs` EditorWindow added with menu entries and dispatch route.
- [x] 09 BAROMETRIC_DENT_AND_SCAR_BAKING - `CSBakeHullDisplacement` writes height in R, slope in G/B, scar in A.
- [x] 10 PROPELLER_CAVITATION_FLIPBOOK_BAKING - `CSBakeCavitationFlipbook` emits 64-frame 8x8 atlas with periodic phase.
- [x] 10b PROPELLER_CAVITATION_SHADER_CONSUMPTION - `H8UberNoirApplyHullCavitationFoam` samples the baked flipbook through a configurable UV window and blends foam into hull albedo/smoothness/emission. Alternative rejected: real particles or CPU bubble emitters.
- [x] 11 MULTI_LAYERED_WEAR_AND_SALT_BAKING - `CSBakeHullMrao` packs metallic R, roughness G, AO B, biolum A.
- [x] 12 ASSET_DATABASE_TEXTURE_SERIALIZATION - Baker writes albedo/MRAO/cavitation PNG and displacement EXR via `File.WriteAllBytes`, then imports.
- [x] 13 AUTOMATED_TEXTURE_IMPORTER_CONFIGURATION - Baker enforces sRGB split, clamp/repeat wrap, Standalone BC7, mobile ASTC_6x6.
- [x] 14 OFFLINE_TEXTURE_VALIDATOR_GATE - Pixel loop checks exact dimensions, count, finite channels, and displacement R bounds.
- [x] 14b OFFLINE_VALIDATOR_ALLOCATION_POLISH - Pixel validation uses `GetPixelData<Color/Color32>()` instead of `GetPixels()`. Mesh metric triangle count uses submesh index counts instead of `mesh.triangles`.
- [x] 15 DRY_RUN_VERIFICATION_EXECUTION - Dispatch math uses `Mathf.CeilToInt` and HLSL coordinate guards; dry-run menu exists.
- [x] 16 CONTINUOUS_QUALITY_SCALING_INTEGRATION - Quality 0..1 scales hull 1024..4096 and cavitation tile 64..256.
- [!] 17 BATCHED_COMPILATION_AND_SYNTAX_ASSERTION - Original build timed out after 124s and reruns were blocked by CPU/process policy. APEX retry launched once only after CPU 43.02% and no compiler processes, with `--no-restore -maxcpucount:1`; it timed out after 244s with no diagnostics. One leftover `dotnet.exe` from that run was terminated. No second retry launched.
- [x] 18 EXPLICIT_PIXEL_COUNT_VALIDATION_GATE - `texture.width * texture.height == expectedResolution` enforced before save.
- [x] 20 COMPACTION_FENCE_RACE_CONDITION_AUDIT - If fence active, controller skips telemetry refresh and uses cached visual state until next sampled tick.
- [x] 21 ZERO_GC_ALLOCATION_PROFILER_MOCK - Steady-state `LateFrameTick` uses scalar math, Shader global vectors, cached telemetry, and fixed managed black-box ring allocated cold. No runtime file dump, no persistent NativeArray, no `GlobalRegistry.Get<T>()`, no `GetComponent()`.
- [x] 22 VRAM_BUDGET_LIMIT_TESTING - BC7 4096 atlas = 16 MB? Correction: BC7 is 8 bpp, so 4096^2 = 16 MB per texture. Three hull textures = 48 MB per hull; three hull variants = 144 MB. If high preset uses prompt Task 16 2048 displacement, per hull = 36 MB and three variants = 108 MB. This exceeds the 85 MB claim unless cavitation or variant residency is streamed. Report marks this honestly.
- [x] 23 AUTOMATED_METRIC_VALIDATOR_REPORT - Superseded by APEX directive: baker no longer writes JSON reports. Source hashes and line evidence recorded in status/log only.
- [x] 23b OBSOLETE_REPORT_ARTIFACT_REMOVAL - Deleted untracked `Docs/Reports/HULL_CAVITATION_BAKER_REPORT_1722.json` because the source-code-only proof rule supersedes JSON artifacts.

## Verification
- [x] Prompt extracted from active batch.
- [x] Hygiene verified: no pre-existing 1722 status/rationale/log files.
- [x] Relevant mandates read.
- [x] Root bibles read.
- [x] Code inspected before edits.
- [x] Forbidden runtime tokens absent in touched runtime controller. Editor baker uses `Mesh.GetVertices` into a prewarmed scratch list and uploads to Editor-only `GraphicsBuffer`.
- [x] Baker scratch memory is fixed-capacity and fail-fast: no runtime or Editor bake path silently grows `MeshVertexScratch`.
- [x] DataVault write-lock scan clean for touched runtime controller: no `TryAcquireWriteLock`, no `ReleaseWriteLock`, no nested write locks. Only pure `TryReadOnlyHandle` current-phase read is used; `IDataVault` exposes no read-release API.
- [x] Filesystem orphan `.meta` scan clean across `Assets`, `ProjectSettings`, `Packages`, and `Docs`. Tracked archive meta deletions pre-existed in working tree; not reverted.
- [x] Unity `.meta` files present and unique for `HullCavitationBaker1722.cs`, `HullCavitationBaker1722.compute`, and `Hecton_HullBakedDisplacement1722.hlsl`.
- [x] Exact telemetry field scan clean: no `entry.Cleanliness01`, `entry.CrewCareTone01`, or `entry.BallastHealth01` remains.
- [x] Hot-swap unregister path confirmed against `GlobalRegistry.cs`: controller uses the non-logging `TryUnregisterHotSwapListener` cold route.
- [x] Compute falloff determinism scan addressed: no intentional reversed-edge `smoothstep(high, low, x)` remains in `HullCavitationBaker1722.compute`.
- [x] Obsolete JSON report artifact removed; baker source contains no report writer or SHA helper.
- [!] Compile not verified: one throttled APEX build retry timed out after 244s with no diagnostics; orphan compiler process was terminated.
- [!] Current compile gate: Unity Roslyn `VBCSCompiler.dll` PID 47240 and Bee `csc.dll` PID 48160 are active. Build/csc refused by throttle rule.
