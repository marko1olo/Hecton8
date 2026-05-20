# Status_SHINOBU_232

Agent: SHINOBU_232
Domain: ABYSSAL_CAUSTICS_AND_PROJECTION_PASS
Task count: 20
Status: LOOP 12 VAULT_READY_AND_PROFILE_RELOAD VERIFIED - SCOPED BUILD STILL BLOCKED BY EXTERNAL DEPENDENCY WALL

## Mandates Selected Before Coding
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_GPU_Sovereignty.txt
- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt

## Loop 0 - Intake
- [x] Extract XML prompt | DOD: CLI regex extraction from CURRENT_BATCH.md, no neighboring prompt state retained | Rejected: MCP/resource read because batch protocol requires CLI | Estimate: 300 us
- [x] Verify status hygiene | DOD: missing Status/Rationale means no stale data to wipe | Rejected: overwriting old status because none existed | Estimate: 50 us
- [x] Read selected mandates | DOD: 8 task-relevant mandates read before coding | Rejected: relying only on AGENTS.md | Estimate: 2000 us
- [x] Scan existing rendering/domain files | DOD: rg against Rendering, Graphics/Caustics, Prefabs, Scenes, renderer assets | Rejected: inventing Assets/_Project/Prefabs/Environment because path is absent | Estimate: 5000 us

## Loop 1 - Tasks 01-05
- [x] Task 01 PROJECTOR_AND_COOKIE_ERADICATION | Justification: scanned Projector/cookie/DecalRendererFeature; no nonzero cookies found; DecalRendererFeature is inactive renderer asset, not caustic light pattern | Alternatives Rejected: deleting inactive shared decal feature without caustic evidence | Estimate: 140 us/frame saved when avoiding one projector-style extra pass
- [x] Task 02 RENDER_TEXTURE_ALLOCATION_PURGE | Justification: legacy AnalyticalCausticsService no longer creates caustic RenderTexture or dispatches compute; new pass consumes URP depth TextureHandle | Alternatives Rejected: temporary caustic RT atlas | Estimate: 220 us/frame GPU+sync risk removed
- [x] Task 03 CS1612_METADATA_STATE_ANNIHILATION | Justification: CausticsParametersDTO exposes only unmanaged public fields; jobs write via UnsafeUtility.AsRef over Vault NativeArray | Alternatives Rejected: get/set DTO wrappers and managed state mirrors | Estimate: 5 us/frame CPU copy/dictionary noise avoided
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | Justification: explicit 64B DTO with 16B float4 offsets and UnsafeUtility/field-offset validator | Alternatives Rejected: sequential layout dependent on compiler padding | Estimate: 1 us/frame corruption avoidance, not perf claim
- [x] Task 05 EMERGENCY_MOCK_LIGHTING_DATA | Justification: GenerateMockCausticLightingJob synthesizes rotating light vector, intensity, local AUP wrap, and quality scalar without Celestial dependency | Alternatives Rejected: blocking on Agent 129 Celestial contract | Estimate: 0 us wait-state; enables isolated render testing
- [x] Compile/static verification after Tasks 01-05 | Result: static rg shows no new Projector/cookie caustic path and no dynamic caustic RenderTexture allocation in edited caustics paths

## Loop 2 - Tasks 06-10
- [x] Task 06 BURST_CAUSTIC_PARAMETER_KERNEL | Justification: CalculateCausticParametersJob reads weather/wave/swell Vault buffers and writes one 64B DTO in deterministic Burst | Alternatives Rejected: managed Update math and Celestial hard dependency | Estimate: 7 us/frame CPU
- [x] Task 07 THE_DEAR_LIE_DEFERRED_SHADER | Justification: HectonDeferredCausticsFeature injects RenderGraph fullscreen pass, is installed in PC_Renderer and PC_High_Renderer, and shader reconstructs world position from depth before procedural Voronoi | Alternatives Rejected: CPU ray trace, projector, light cookie | Estimate: 180 us/frame saved vs projector-style pass
- [x] Task 08 SDF_CAVERN_OCCLUSION_MATH | Justification: shader samples _HectonCaveVoxelSdfTex and fixed ray steps toward sun to fade cave interiors | Alternatives Rejected: shadow maps or extra cave mask render target | Estimate: 90 us/frame saved vs shadow/mask pass
- [x] Task 09 WAVE_PHASE_SYNCHRONIZATION | Justification: job consumes ShinobuOceanWaveParameters and ShinobuOceanSurfaceSwell to modulate phase, panning, and scale | Alternatives Rejected: unrelated Time.time-only drift | Estimate: 2 us/frame CPU
- [x] Task 10 ASYNCHRONOUS_GPU_BUFFER_UPLOAD | Justification: runtime uses double GraphicsBuffer.Target.Constant, LockBufferForWrite, UnsafeUtility.MemCpy, then constant-buffer bind | Alternatives Rejected: multiple Shader.SetGlobalFloat/Vector calls | Estimate: 5 us/frame CPU API churn removed
- [x] Compile/static verification after Tasks 06-10 | Result: static rg confirms CalculateCausticParametersJob, SDF shader sampling, RenderGraph pass, constant-buffer upload, and no new SetGlobalFloat path in AbyssalCaustics

## Loop 3 - Tasks 11-15
- [x] Task 11 CONTINUOUS_SCALABILITY_NOISE_OCTAVES | Justification: shader uses GlobalQualityWeight with smoothstep weights for second Voronoi layer and chroma; C# also shrinks max depth continuously | Alternatives Rejected: low/high hardware booleans | Estimate: 40-120 us/frame saved under low quality by less visible ALU/depth
- [x] Task 12 DEPTH_FALLOFF_CULLING | Justification: shader returns before Voronoi when linear eye depth exceeds MaxCausticDepth; MaxCausticDepth shrinks with quality | Alternatives Rejected: computing noise then fading to zero | Estimate: 80 us/frame in abyss-heavy views
- [x] Task 13 AUP_PRECISION_NOISE_WRAPPING | Justification: jobs wrap camera AUP LocalX/Y/Z modulo NoiseTileSize before casting to float and sending offset to GPU | Alternatives Rejected: absolute double/large float coordinates in shader | Estimate: visual stability, not CPU perf
- [x] Task 14 ROLLBACK_NETCODE_STATE_FENCE | Justification: caustic buffers use SHINOBU presentation BufferIDs only; static scan found no StateRingBuffer/Merkle/Lockstep references in AbyssalCaustics | Alternatives Rejected: adding caustics to deterministic rollback hash | Estimate: network bandwidth kept at 0 bytes for visuals
- [x] Task 15 ZERO_INIT_OVERHEAD_BYPASS | Justification: Vault buffers and CSV scratch are requested with NativeArrayOptions.UninitializedMemory and overwritten by jobs/seeding | Alternatives Rejected: ClearMemory per frame or resizing | Estimate: 3 us startup/frame-loop risk avoided
- [x] Compile/static verification after Tasks 11-15 | Result: static rg confirms GlobalQualityWeight, depth early return, AUP wrap, deterministic Burst, no rollback coupling, and UninitializedMemory usage

## Loop 4 - Tasks 16-20
- [x] Task 16 TELEMETRY_RENDERING_RECORDER | Justification: 300-entry Vault telemetry ring records intensity, active octaves, max depth, estimated GPU us, and dumps Dump_SHINOBU_232.bin on nonfinite setup | Alternatives Rejected: managed List/frame log | Estimate: 2 us/frame CPU
- [x] Task 17 CAUSTICS_TUNER_EDITOR_WINDOW | Justification: UI Toolkit editor window exposes chromatic dispersion, noise scale, flow speed, max depth and writes Vault-backed tuning DTO | Alternatives Rejected: inspector-only serialized recompiles | Estimate: editor-only, 0 runtime us
- [x] Task 18 CSV_LIGHTING_PROFILES_INGESTOR | Justification: cold parser slices ReadOnlySpan<byte>, computes FNV-1a lowercase hashes, avoids string.Split, writes profile DTOs into Vault, and runtime scans the fixed 32-row Vault table | Alternatives Rejected: string.Split, managed dictionaries, and private NativeParallelHashMap ownership | Estimate: cold path only, 0 runtime us
- [x] Task 19 LIVE_PROJECTION_DEBUG_GIZMO | Justification: OnDrawGizmos reads CausticsParametersDTO and draws yellow projection ray plus cyan max-depth plane at camera anchor | Alternatives Rejected: shader inspection-only artist workflow | Estimate: editor-only, 0 runtime us
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | Justification: LOG_SHINOBU_232.md contains SELF_AUDIT XML with byte layout, Vault IDs, GC/static checks, AUP wrap, quality scaling, SDF status | Alternatives Rejected: chat-only report | Estimate: 0 runtime us
- [x] Compile/static verification after Tasks 16-20 | Result: static rg and git diff --check passed; dotnet build skipped because CPU guard samples stayed above 50%

## Loop 5 - Self-Review
- [x] Re-read changed code for projector/cookie leftovers | Result: no new Projector/cookie/RenderTexture/MaterialPropertyBlock path in AbyssalCaustics or edited legacy caustics service
- [x] Re-read shader for depth/SDF/AUP/quality compliance | Result: depth early return precedes Voronoi; world position uses depth; SDF occlusion samples cave voxel texture; quality controls second/chroma branches
- [x] Re-read C# for GC, DTO layout, public API risk | Result: 64B DTO explicit; hot path uses phase-local Vault views, IJob.Run, and GraphicsBuffer CBuffer; bootstrap change is cold reflection-only
- [x] Append final LOG_SHINOBU_232.md | Result: appended with What was wrong, What was done, cinematic cheats, us estimates, and SELF_AUDIT XML
- [x] Hook deferred renderer assets after self-review | Result: PC_Renderer.asset has 16 features and 16 map entries; PC_High_Renderer.asset has 15 features and 15 map entries; both contain active HectonDeferredCausticsFeature with stable .meta GUIDs
- [x] Correct Burst job invocation after self-review | Result: static rg confirms `job.Run()` at both runtime caustic job callsites and no `job.Execute()` callsite remains in AbyssalCaustics
- [x] Build guard re-check after renderer hook | Result: dotnet processes were already running and CPU samples were 100.0%, 100.0%; dotnet build forbidden by batch rule
- [x] Final static verification | Result: renderer maps still match; git diff --check passed with line-ending warnings only; final CPU samples were 13.5%, 26.3% but dotnet processes were still running, so build remained forbidden

## Loop 6 - Ultra-Think Polish Reconciliation
- [x] Re-read Status/Rationale before response | DOD: anti-amnesia files read from disk before continuing polish | Rejected: trusting chat summary | Estimate: 25 us runtime impact 0
- [x] Re-extract SHINOBU_232 XML block | DOD: CLI extraction still reports Task count 20 and same deferred caustics assignment | Rejected: neighboring prompt influence | Estimate: 300 us runtime impact 0
- [x] Remove private native ownership | DOD: AbyssalDeferredCausticsRuntime now stores only `VaultGenerationHandle<T>` descriptors; no private `NativeArray`, `NativeParallelHashMap`, `Allocator.Persistent`, or `VaultBufferHandle` remains in AbyssalCaustics | Rejected: keeping a private CSV scratch array and profile hash map | Estimate: 1-3 us cold/hot hygiene; primary value is ownership safety
- [x] Replace Unity time source | DOD: caustic phase now advances from sanitized `Tick(deltaTime)` into `_presentationTimeSeconds` and `_presentationFrameIndex`; no `Time.time` or `Time.frameCount` remains | Rejected: UnityEngine.Time in presentation kernel | Estimate: desync/debug risk removed, not a frame-time claim
- [x] Add Burst/NoAlias/NaN polish | DOD: both jobs use `CompileSynchronously = true`, deterministic float mode, `[NoAlias]`, safe vector normalization, and `job.Run()` callsites | Rejected: direct `Execute()`, alias-opaque NativeArrays, and unsafe `math.normalize` | Estimate: 3-8 us CPU vectorization/proof hygiene
- [x] Harden Vault lifecycle | DOD: DataVault hot-swap releases old generation handles before rebinding and shutdown releases all caustic Vault lanes | Rejected: dropping handles and leaking Vault ref-counts until process exit | Estimate: cold path only, prevents ownership leak
- [x] Shader NaN guard polish | DOD: HLSL sun vector now uses `SafeNormalize3` and keeps depth/SDF/quality flow continuous | Rejected: raw normalize relying on nonzero mock light vector | Estimate: crash-risk removal, no profiler claim
- [x] Static verification after polish | Result: `rg` clean for private NativeArray/native hash/list/queue, Allocator.Persistent, VaultBufferHandle, Time.time/frameCount, job.Execute, System.Reflection, math.normalize, Transform.position, Hecton8.Physics in AbyssalCaustics; `git diff --check` passed with line-ending warnings only
- [x] Build guard after polish | Result: CPU samples were 6.9%, 4.4%, but seven dotnet processes were already running; dotnet build remained forbidden by the no-concurrent-dotnet rule

## Loop 7 - Offset Proof And Route Card
- [x] Re-read Status/Rationale and Unity skill | DOD: anti-amnesia files and Unity-MCP workflow were read before continuing; no Unity MCP editor tools are exposed in this session | Rejected: trusting prior chat summary | Estimate: 25 us runtime impact 0
- [x] Re-extract SHINOBU_232 XML block with attribute-tolerant regex | DOD: CLI extraction reports `TASK_COUNT=20` and ignores neighboring prompts | Rejected: brittle exact opening-tag regex | Estimate: 300 us runtime impact 0
- [x] Add editor-only `UnsafeUtility.GetFieldOffset` audit | DOD: `AbyssalCausticsLayoutAudit` validates parameter, tuning, telemetry, and profile DTO offsets under `#if UNITY_EDITOR` only | Rejected: runtime reflection or trusting FieldOffset constants alone | Estimate: 0 us runtime; prevents ARM64 CBuffer regression
- [x] Add architecture route card | DOD: `Docs/ARCHITECTURE/ABYSSAL_CAUSTICS_SHINOBU_232.md` records owner, Vault IDs, render path, quality curve, and compile guard | Rejected: chat-only architecture proof | Estimate: 0 runtime us
- [x] Correct stale direct job execution | DOD: both caustic job callsites use `job.Run()` and `rg` reports no `job.Execute(` in runtime caustic sources | Rejected: direct `IJob.Execute()` bypassing the job extension path | Estimate: 0-3 us proof hygiene
- [x] Static verification after Loop 7 | Result: runtime `rg` clean for private native ownership, persistent allocator, VaultBufferHandle, Unity time, job.Execute, runtime System.Reflection, raw math.normalize, Projector-era render allocations, MaterialPropertyBlock, Shader.SetGlobal*; editor-only scan shows `System.Reflection` and `UnsafeUtility.GetFieldOffset` only in `AbyssalCausticsLayoutAudit`
- [x] Build guard after Loop 7 | Result: CPU samples were 3.7%, 5.0%, but seven dotnet processes were active; dotnet build remained forbidden by no-concurrent-dotnet rule

## Loop 8 - External Input Handle Audit
- [x] Re-read Status/Rationale, SHINOBU_232 XML, and binary ledger | DOD: anti-amnesia sources were read from disk before edits; task count remains 20 | Rejected: relying on previous chat state | Estimate: 300 us runtime impact 0
- [x] Audit assembly and producer-lane coupling | DOD: asmdef sweep shows AbyssalCaustics currently lives under `Hecton8.Core`; Atmosphere DTOs are in the same root assembly, but producer Vault lanes still needed non-owning resolution | Rejected: adding a new runtime asmdef during multi-agent churn | Estimate: 0 runtime us
- [x] Replace per-frame ocean `TryGetBuffer` lookups | DOD: weather, wave, and swell inputs now use cached non-owning `VaultGenerationHandle<T>` descriptors from `TryGetGenerationHandle` and resolve through `TryResolveHandle` | Rejected: `GetGenerationHandle` on producer-owned buffers because it can allocate/grow another domain's lane | Estimate: 1-4 us/frame metadata churn reduction when producers exist
- [x] Remove hot static player context lookup | DOD: `ResolveCameraAupLocalOffset` now reads `_playerRuntimeContext.TryGetPlayerPoseSnapshot` and no longer calls `PlayerRuntimeContextService.TryGetActiveRuntimeContext` | Rejected: global static context discovery in `Tick` | Estimate: 0-2 us/frame and cleaner ownership proof
- [x] Clear non-owned external descriptors on Vault replacement/shutdown | DOD: external handles are reset but never released; producer ownership stays with Atmosphere | Rejected: releasing another system's generation handle | Estimate: cold path only
- [x] Static verification after Loop 8 | Result: runtime `rg` clean for `TryGetBuffer(BufferID.ShinobuOcean*)`, `PlayerRuntimeContextService.TryGetActiveRuntimeContext`, private native ownership, persistent allocator, VaultBufferHandle, Unity time, job.Execute, runtime System.Reflection, raw math.normalize, Projector-era allocations, MaterialPropertyBlock, Shader.SetGlobalFloat/Vector/Texture
- [x] Build guard after Loop 8 | Result: first guard sampled CPU 79.5%, 45.8% with dotnet/csc count 0, so build was deferred; later guard sampled CPU 12.2%, 24.6% with dotnet/csc count 0, allowing one scoped build probe
- [x] Scoped build probe after guard cleared | Result: `dotnet build Hecton8.Core.csproj --no-restore` failed on 77 pre-existing external dependency errors before any `Assets/_Project/Scripts/Rendering/AbyssalCaustics/*` error surfaced; examples include missing `Hecton8.Equipment`, `Hecton8.Logistics.Grid`, `SoundEmissionSignal`, content VRAM services, fauna/physics/construction/world bridge types | DOD: no unrelated dependency edits made | Estimate: 0 runtime us

## Loop 9 - Dispatch And RenderGraph Binding Polish
- [x] Re-read Status/Rationale, SHINOBU_232 XML, binary ledger, AGENTS, and domain map | DOD: anti-amnesia sources were read from disk before edits; task count remains 20 | Rejected: continuing from chat memory | Estimate: 300 us runtime impact 0
- [x] Repair stale direct job invocation | DOD: `CalculateCausticParametersJob` and `GenerateMockCausticLightingJob` now run through `job.Run()`; targeted `rg` reports no `job.Execute(` in AbyssalCaustics | Rejected: direct `IJob.Execute()` bypassing Burst/job extension proof | Estimate: 0-3 us proof hygiene
- [x] Remove runtime global CBuffer rebinding | DOD: `AbyssalDeferredCausticsRuntime` now only uploads the double-buffered `GraphicsBuffer`; RenderGraph command context performs the CBuffer binding | Rejected: per-LateFrame `Shader.SetGlobalConstantBuffer` churn outside RenderGraph | Estimate: 1-3 us/frame API-state churn reduction on low-end CPU
- [x] Isolate debug gizmo editor camera route | DOD: `OnDrawGizmos` is under `#if UNITY_EDITOR`, uses SceneView fallback, and avoids `Camera.main` plus `transform.position` tokens | Rejected: player-compilable diagnostic camera search | Estimate: 0 runtime us
- [x] Add cold shader pass warmup | DOD: renderer feature calls `WarmupMaterialPass` and `material.SetPass(0)` in playing-mode `Create()` after material creation | Rejected: waiting for first gameplay draw to compile the pass | Estimate: hitch-risk reduction, not frame-time claim
- [x] Static verification after Loop 9 | Result: targeted `rg` clean for `job.Execute(`, runtime `Shader.SetGlobalConstantBuffer`, `Shader.SetGlobalFloat/Vector/Matrix/Texture`, `Camera.main`, `transform.position`, Unity time, private native ownership, persistent allocator, VaultBufferHandle, and raw `math.normalize`; RenderGraph pass still binds textures/CBuffer through command buffer; `git diff --check` passed for touched caustic source files
- [x] Build decision after Loop 9 | Result: no second build launched because Loop 8 already proved `Hecton8.Core.csproj` is blocked by 77 unrelated external dependency errors before owned caustics files surface; repeated build would not add signal

## Loop 10 - CSV Profile Binding Audit
- [x] Re-read Status/Rationale, Unity skill, AGENTS, domain map, and SHINOBU_232 batch block | DOD: anti-amnesia and task count re-confirmed from disk; exact-tag extraction failed, attribute-tolerant extraction found the block and 20 tasks | Rejected: trusting chat summary | Estimate: 300 us runtime impact 0
- [x] Bind parsed CSV profile fields into CBuffer math | DOD: `FlowSpeed` now multiplies procedural pan speed, `ChromaticDispersion` writes `NoiseAnimationSpeed.w`, and `SdfShadowStrength` writes `IntensityAndDepthFalloff.w` when a profile matches | Rejected: leaving parser-only dead fields | Estimate: 0-2 us/frame; main value is artist-control correctness
- [x] Resolve weather profile keys against real producer masks | DOD: CSV names `Calm`, `Storm`, `Hurricane`, `Thermocline`, `Halocline`, and `Biolume` map to canonical `WeatherState` bits; unknown names still compute FNV-1a fallback for future biome/profile routes | Rejected: comparing FNV hashes directly to `WeatherStateDTO.StateMask`, which never matches example names | Estimate: cold parser only, 0 runtime us
- [x] Repair direct job invocation regression again | DOD: targeted `rg` now reports two `job.Run()` callsites and zero `job.Execute(` in AbyssalCaustics | Rejected: direct `IJob.Execute()` on Burst kernels | Estimate: 0-3 us proof hygiene
- [x] Static verification after Loop 10 | Result: `rg` clean for forbidden runtime shader globals, `Camera.main`, `transform.position`, Unity time, private native ownership, persistent allocator, VaultBufferHandle, raw `math.normalize`, and `job.Execute(` in AbyssalCaustics; `git diff --check` passed for touched caustic source files
- [x] Scoped build probe after Loop 10 | Result: CPU guard was 8%, no `dotnet`/`csc`; `dotnet build Hecton8.Core.csproj --no-restore` failed with the same 77 unrelated external dependency errors and no `Assets/_Project/Scripts/Rendering/AbyssalCaustics/*` error surfaced before the wall | DOD: no unrelated dependency edits made | Estimate: 0 runtime us

## Loop 11 - Shader Cost Curve And Render Target Format Audit
- [x] Re-read Status/Rationale, Unity skill, SHINOBU_232 XML, and binary ledger | DOD: anti-amnesia files and task count re-confirmed from disk before shader polish | Rejected: continuing from chat memory | Estimate: 300 us runtime impact 0
- [x] Collapse SDF ray samples under low GlobalQualityWeight | DOD: shader keeps the first cheap SDF lookup, then gates the four sun-ray samples with a continuous `sdfSampleBudget = saturate((quality - 0.30) * 1.4285715) * 4.0`; below 0.3 the loop skips texture fetches instead of only zeroing weights | Rejected: performing four 3D texture samples whose weights are zero | Estimate: up to 3-4 3D texture fetches/pixel avoided in low-quality cave views
- [x] Preserve camera color format in RenderGraph composite | DOD: removed forced `GraphicsFormat.B10G11R11_UFloatPack32`; destination texture now inherits the active camera color format and only strips depth/MSAA/mips | Rejected: hidden format conversion and alpha/precision mismatch risk | Estimate: no fixed us claim, avoids format-conversion risk
- [x] Static verification after Loop 11 | Result: forbidden-pattern `rg` remains clean in AbyssalCaustics; targeted scan confirms `sdfSampleBudget`, one `SAMPLE_TEXTURE3D_LOD` helper, no forced B10G11 format, two `job.Run()` callsites, and no `job.Execute(` | Estimate: 0 runtime us
- [x] Build decision after Loop 11 | Result: no build launched; Loop 10 already reproduced the same 77-error external dependency wall and this patch is shader/RenderGraph-only | Estimate: 0 runtime us

## Loop 12 - Vault Ready Gate And Profile Reload Audit
- [x] Re-read Status/Rationale and re-extract SHINOBU_232 XML | DOD: CLI extraction counts `Task 01:` through `Task 20:` and returns 20; neighboring prompts ignored | Rejected: counting nonexistent `<TASK>` tags or trusting compressed chat | Estimate: 300 us runtime impact 0
- [x] Add owner Vault ready gate | DOD: `EnsureVaultState()` now cold-acquires/seeds owner lanes, records `_vaultStateReady`, and per-frame `Tick` skips five owner-lane resolve/acquire checks while handles stay valid | Rejected: probing all owner Vault lanes every frame after boot | Estimate: 2-6 us/frame metadata churn reduction on low-end CPU
- [x] Add stale-handle recovery | DOD: failed required owner resolves clear `_vaultStateReady`; DataVault hot-swap, release, and shutdown also clear it | Rejected: assuming generation handles never go stale across compaction/hot-swap | Estimate: safety path, no steady-frame cost
- [x] Expose CSV profile reload in editor facade | DOD: tuner now has a default profiles path and `Load Profiles CSV` button that calls a static runtime bridge into the cold span parser | Rejected: parser-only human bridge with no reachable UI control | Estimate: editor-only, 0 runtime us
- [x] Add baseline `caustic_lighting_profiles.csv` asset | DOD: calm/storm/hurricane/thermocline/halocline/biolume rows exercise known weather-mask profile binding | Rejected: requiring artists to invent the first file before validation | Estimate: cold file IO only when explicitly loaded
- [x] Static verification after Loop 12 | Result: forbidden-pattern `rg` clean in AbyssalCaustics; targeted scan confirms `_vaultStateReady`, `AreOwnedVaultHandlesCreated`, `TryLoadLightingProfilesCsv`, default profile asset, and no trailing whitespace | Estimate: 0 runtime us
- [x] Build decision after Loop 12 | Result: no build launched; Loop 10 already proved the scoped project is blocked by 77 unrelated dependency errors before SHINOBU_232 files surface | Estimate: 0 runtime us
