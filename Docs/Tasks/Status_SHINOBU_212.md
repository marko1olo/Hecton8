# Status_SHINOBU_212

Agent: SHINOBU_212
Role: OFFLINE_HLOD_IMPOSTOR_BAKER
Domain: Echelon 2 World Generation / Rendering HLOD impostors
Task count: 20
Status: IMPLEMENTED - COMPILE PENDING CPU GATE

First 20 Minutes moment: world load / swim visibility.
Route impact: removes distant giant geometry cost on the Copper Wire route without changing gameplay truth.
Proof required: Unity import, Console clean, editor bake smoke, generated atlas import settings, Frame Debugger/Profiler capture, GCMonitor hot-path proof.
Parked work rejected: runtime capture and gameplay-state netcode hashing for visual LOD state.

Relevant mandates read:
- REND_URP_Graphics_HotPath_Optimization_HLOD
- REND_GPU_Sovereignty
- GPU_Compute_Kernels_Kernels_Optimization_MX350
- DATA_Runtime_Struct_Layout_ARM64
- OPT_Zero_GC_Policy_AllocFree_Mandate
- MATH_AUP_Determinism_Sync
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First
- STRM_Async_Asset_Upload_Texture_Settings
- TOOL_Designer_Facades_CSV_Binary_Bridge

## Loop 0 - Prompt / Sanitation

- [x] Extract SHINOBU_212 XML block | DOD: CLI regex extraction from CURRENT_BATCH.md, not truncated MCP read | Rejected: relying on chat summary | Estimate: 70 us
- [x] Read domain and global authority docs | DOD: Docs/Actual Domains of Project.txt + GLOBAL_AUTHORITY_BOUNDARIES.md + FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md | Rejected: guessing domain from prompt only | Estimate: 120 us
- [x] Verify no stale SHINOBU_212 status/rationale | DOD: Test-Path returned missing for both files | Rejected: overwriting old state blindly | Estimate: 20 us

## Loop 1 - Tasks 01-05

- [x] Task 01 REALTIME_IMPOSTOR_INQUISITION | DOD: static scan of Rendering/Environment found no runtime Camera.Render/ReadPixels impostor capture; old editor baker replaced | Rejected: gameplay RenderTexture capture controller | Estimate: 45 us avoided per far object per state-change burst, profiler pending
- [x] Task 02 UNITY_BILLBOARD_RENDERER_PURGE | DOD: first-party YAML scan found no BillboardRenderer component; terrain tree metadata reported only | Rejected: deleting unrelated `Renderer BillboardRenderer` field in legacy World/ImpostorSystem because it is not Unity BillboardRenderer | Estimate: 12 us avoided per legacy billboard submission, profiler pending
- [x] Task 03 CS1612_CAPTURE_STATE_ANNIHILATION | DOD: `CalculateCaptureAnglesJob` and `GenerateMockCaptureTargetJob` use raw public fields plus `UnsafeUtility.AsRef` pointer iteration | Rejected: property-backed capture DTOs | Estimate: 18 us per 16-view angle build, benchmark pending Unity import
- [x] Task 04 ARM64_MAPPING_LAYOUT_ASSERTION | DOD: `ImpostorConfigDTO` explicit 16 bytes, `OctahedralImpostorInstance` explicit 32 bytes, editor validator installed | Rejected: sequential `Pack=4` GPU payload | Estimate: alignment safety; runtime delta profiler pending
- [x] Task 05 EMERGENCY_MOCK_CAPTURE_BENCHMARK | DOD: Burst `GenerateMockCaptureTargetJob` creates dense deterministic point cloud for angle/bounds stress | Rejected: waiting for final wreck art | Estimate: 65,536 point mock target, stopwatch menu installed

## Loop 2 - Tasks 06-10

- [x] Task 06 HEMISPHERICAL_CAMERA_ORCHESTRATOR | DOD: `CalculateCaptureAnglesJob` outputs camera position, view matrix, projection matrix via Fibonacci distribution | Rejected: managed angle list / hand-authored views | Estimate: 16 views in <25 us target, Unity benchmark pending
- [x] Task 07 AUTOMATED_RENDER_TEXTURE_CAPTURE | DOD: Editor-only camera renders albedo + normal/depth into RTs, command buffers clear/dispatch without scene view mutation | Rejected: runtime capture camera | Estimate: removes runtime capture cost completely; editor capture time pending GPU
- [x] Task 08 THE_DEAR_LIE_ATLAS_PACKING | DOD: `PackImpostorAtlas.compute` packs albedo RGB + depth alpha and normal X/Z into atlas grid | Rejected: one file per view | Estimate: one material bind / two atlas samples per runtime draw
- [x] Task 09 DEPTH_BASED_PIXEL_DILATION | DOD: `DilateImpostorEdges.compute` nearest-valid depth expansion for albedo and normal atlases | Rejected: mip bleeding acceptance / CPU pixel loops | Estimate: mip halo avoidance; compute time reported per bake
- [x] Task 10 ASYNCHRONOUS_ASSET_SERIALIZATION | DOD: `AsyncGPUReadback` + `ImageConversion.EncodeNativeArrayToPNG` + unsafe `FileStream.Write(ReadOnlySpan<byte>)`; BC7 importer and quad mesh generated | Rejected: `Texture2D.ReadPixels`, `EncodeToPNG`, managed byte arrays | Estimate: avoids editor main-thread readback stall; exact us report pending bake

## Loop 3 - Tasks 11-15

- [x] Task 11 PROCEDURAL_VIEW_INTERPOLATION_SHADER | DOD: standalone `Hecton_HLOD_Impostor.shader` and updated include select two of 16 views and blend continuously by `GlobalQualityWeight` | Rejected: dither-only binary low/high path | Estimate: two atlas samples per channel; saved geometry dominates
- [x] Task 12 AUP_DEPTH_RECONSTRUCTION | DOD: depth alpha is sampled from albedo-depth atlas and pushes `SV_Depth`; AUP camera-relative CPU helper added | Rejected: absolute world float helper only | Estimate: fog/DOF correctness pending visual smoke
- [x] Task 13 ROLLBACK_NETCODE_EXCLUSION_FENCE | DOD: rollback fence validator/report confirms no HLOD DTO/matrix leaf in StateRingBuffer descriptors | Rejected: hashing visual LOD state | Estimate: desync risk removed, no frame cost
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS | DOD: TempJob NativeArrays use `UninitializedMemory`; no `MemClear`, no `ClearMemory` in SHINOBU baker path | Rejected: zero-filled scratch texture buffers | Estimate: avoids MB-scale zero init during bake
- [x] Task 15 TELEMETRY_BAKE_REPORT_GENERATOR | DOD: bake writes `Docs/Reports/IMPOSTOR_BAKE_REPORT.json` with object count, atlas, bytes, GPU pack microseconds, VRAM warning | Rejected: chat-only reporting | Estimate: report overhead editor-only

## Loop 4 - Tasks 16-20

- [x] Task 16 PROCEDURAL_IMPOSTOR_FORGE_WINDOW | DOD: UI Toolkit window with prefab/folder fields, sliders, profile dropdown, progress bar, bake button | Rejected: IMGUI one-off menu only | Estimate: editor-only
- [x] Task 17 CSV_IMPOSTOR_PROFILES_INGESTOR | DOD: `impostor_generation_profiles.csv` parser reads NativeArray bytes into FixedString records without garbage strings in parser | Rejected: string.Split CSV | Estimate: editor parser avoids line/cell allocation
- [x] Task 18 LIVE_CAPTURE_PREVIEW_GIZMO | DOD: SceneView preview uses angle job and draws capture vectors before bake | Rejected: baking to discover bad angles | Estimate: avoids failed multi-second bakes
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | DOD: `LOD_Distance_Scanner` writes `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` from prefab/YAML + bounds scan | Rejected: subjective horizon claims | Estimate: editor-only
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: self-audit/static scans performed; RenderTextures released in finally/callback release; BC7 importer code present | Rejected: declaring Unity runtime proof without import | Estimate: no runtime cost

## Verification

- [x] Static scan: no runtime impostor capture in Rendering/Environment
- [x] Static scan: no BillboardRenderer usage in first-party assets
- [ ] Compile/import check: pending
- [x] Report append: `Docs/AgentLogs/LOG_SHINOBU_212.md`

## Compile Gate

- [x] CPU/csc gate checked before build | Result: CPU sample 100%, no csc/dotnet process | Decision: build not launched under >50% CPU rule | Timestamp: 2026-05-20
- [x] CPU/csc gate rechecked after implementation | Result: CPU sample 100%, no csc/dotnet process | Decision: compile/import remains pending by rule | Timestamp: 2026-05-20

## Loop 5 - Strict Self-Read

- [x] Re-read baker/editor source for forbidden capture calls | DOD: rg returned no `ReadPixels`, `GetPixels`, `EncodeToPNG`, `Camera.Render`, managed `byte[]`, `File.ReadAllBytes`, `ToArray`, or `MemClear` in SHINOBU path | Rejected: assuming code review from memory | Estimate: static only
- [x] Re-read shader handoff | DOD: removed `UsePass` wrapper risk and made `Hecton_HLOD_Impostor.shader` standalone | Rejected: relying on ShaderLab pass-name normalization | Estimate: import risk reduction, no runtime delta
- [x] Re-read reports/logs | DOD: final report appended to LOG and self-audit XML embedded | Rejected: chat-only report | Estimate: none

## Loop 6 - Ultra Polish Mandate Pass

- [x] Re-read prompt/log/ledger after polish mandate | DOD: status, rationale, XML block, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, global authority doc, and SHINOBU architecture doc reopened from disk | Rejected: relying on compressed chat memory | Estimate: 90 us static recall
- [x] Harden Burst jobs | DOD: both SHINOBU jobs now use `CompileSynchronously = true`, `FloatMode.Fast`, `FloatPrecision.Standard`, and `[NoAlias]` on output arrays | Rejected: implicit Burst defaults and alias-conservative compilation | Estimate: 3-8 us saved per editor rig build, profiler pending
- [x] Remove renderer-owned persistent NativeArray upload cache | DOD: `_uploadedInstances`, `Allocator.Persistent`, and `NativeMemorySentinel` are gone from `HectonOctahedralImpostorRenderer`; upload writes source arrays or direct `GraphicsBuffer.LockBufferForWrite` | Rejected: private renderer upload cache outside Vault law | Estimate: avoids one persistent native heap allocation and one CPU memcpy per HLOD bind
- [x] Convert Forge persistent NativeArrays | DOD: editor window profile/preview caches are managed cold UI arrays; preview job uses short-lived `TempJob` records and disposes immediately | Rejected: hidden persistent native state in tool window | Estimate: editor-only, removes H-Phi exception
- [x] Remove hot `GlobalRegistry.ScalabilityTier` dependency from SHINOBU renderer | DOD: Tick quality path reads continuous `HomeostasisBrain.GlobalQualityWeight`; culling enum adapter derives from that scalar | Rejected: binary low/high tier branch in renderer hot path | Estimate: branch/service read removed; profiler pending
- [x] Write strict self-audit artifact | DOD: `Docs/Reports/SHINOBU_212_SELF_AUDIT.xml` parses as XML and contains 20-task reconciliation, layout math, scalability curve, H-Phi/Vault status, job/no-alias graph, compile guard, and Dear Lie complexity | Rejected: shallow XML inside chat/log only | Estimate: no runtime cost
- [x] Static polish gates | DOD: SHINOBU scan finds no `_uploadedInstances`, `Allocator.Persistent`, `GlobalRegistry.ScalabilityTier`, missing Burst compile flags, `ReadPixels`, `Camera.Render`, managed PNG bytes, `MemClear`, `ClearMemory`, `IntegerSlider`, or `UsePass`; only static validator text intentionally contains `ReadPixels` as a search token | Rejected: visual inspection only | Estimate: no runtime cost
- [x] Compile gate rechecked | DOD: CPU sample 100%, compiler process count 0 | Rejected: violating >50% CPU build ban | Estimate: protects parallel agent iteration

## Loop 7 - Runtime Allocation / Shader Cost Pass

- [x] Remove runtime fallback mesh/material allocation | DOD: `HectonOctahedralImpostorRenderer` no longer calls `new Mesh`, `new Material`, `Shader.Find`, or `new[]`; missing baked mesh/material fails closed | Rejected: lazy runtime fallback asset creation | Estimate: removes first-draw managed allocations and material clone risk
- [x] Remove Unity time reads from SHINOBU renderer | DOD: Tick uses dispatcher `deltaTime` accumulation and a local tick counter; scans find no `Time.time` or `Time.frameCount` in renderer | Rejected: presentation code reading Unity global time in tick path | Estimate: deterministic tick surface; profiler pending
- [x] Harden editor tuning DTO layout | DOD: `HlodImpostorBakeSettings` and `HlodImpostorProfileRecord` are explicit 96-byte structs with `FixedString64Bytes` and padding; validator checks size/offsets | Rejected: string-backed bake settings and implicit profile record layout | Estimate: editor-only, removes managed string from recipe DTO
- [x] Collapse low-quality shader interpolation cost | DOD: `Hecton_HLOD_Impostor.shader` uses `smoothstep(0.22, 0.55, GlobalQualityWeight)` and skips secondary atlas samples below the gate | Rejected: always sampling both atlas frames at q=0.1 | Estimate: two texture samples skipped per surviving impostor pixel on survival quality
- [x] Expand runtime capture scanner | DOD: static archaeology now treats `RenderWithShader` as forbidden in runtime Rendering/Environment directories alongside `Camera.Render`, `ReadPixels`, and `EncodeToPNG` | Rejected: only scanning exact `Camera.Render` token | Estimate: no runtime cost
- [x] Compile gate rechecked after Loop 7 | DOD: CPU sample 100%, compiler process count 0 | Rejected: launching build under explicit >50% CPU ban | Estimate: protects parallel agent iteration

## Loop 8 - Boundary / AUP Link Pass

- [x] Remove concrete MapMagic bridge read from SHINOBU renderer | DOD: `ResolveGlobalFloatingOffset()` now uses `HectonFloatingOrigin.CurrentTotalOffset`; renderer scan finds no `HectonMapMagicVegetationBridge` | Rejected: direct world-generation bridge dependency for a presentation-only HLOD renderer | Estimate: no measurable frame delta expected; removes compile-wall/coupling risk
- [x] Re-scan renderer hot-path tokens | DOD: no `Shader.SetGlobal`, `SetGlobal`, `Time.*`, runtime fallback mesh/material/shader allocation, `QualityFlags`, `GlobalRegistry.ScalabilityTier`, `Allocator.Persistent`, `private NativeArray`, `MaterialPropertyBlock`, or renderer material mutation tokens remain in SHINOBU renderer | Rejected: trusting previous scan after code changed | Estimate: static proof only
- [x] Re-scan runtime capture directories | DOD: `Assets/_Project/Scripts/Rendering` and `Assets/_Project/Scripts/Environment` return no `Camera.Render`, `RenderWithShader`, `ReadPixels`, or `EncodeToPNG` matches | Rejected: wildcard scan with PowerShell path error | Estimate: no runtime capture cost
- [x] Re-run diff whitespace gate | DOD: `git diff --check` reports only LF-to-CRLF working-copy warnings on touched files, no whitespace errors | Rejected: skipping final local hygiene after docs edits | Estimate: static only
- [x] Compile gate rechecked after Loop 8 | DOD: CPU sample 100%, compiler process count 0 | Rejected: launching build/import under explicit >50% CPU ban | Estimate: protects parallel agent iteration

## Loop 9 - SRP Batcher / Material Churn Pass

- [x] Move impostor dynamic scalar uniforms into `UnityPerMaterial` CBUFFER | DOD: `Hecton_HLOD_Impostor.shader` and legacy `Hecton_OctahedralImpostor.shader` now declare `_HectonImpostorTimeSeconds`, `_HectonImpostorFadeOutSeconds`, `_HectonUseVisibleMatrixStream`, and `_GlobalFloatingOffset` inside `CBUFFER_START(UnityPerMaterial)` before including `Hecton_Impostor.hlsl` | Rejected: loose per-material uniforms outside CBUFFER | Estimate: SRP Batcher compatibility risk reduction, profiler pending
- [x] Remove legacy quality-flag fallback residue | DOD: `rg` finds no `_HectonImpostorQualityFlags` / `QualityFlags` in SHINOBU renderer or both impostor shaders | Rejected: silent fallback shader retaining stale binary-ish quality flag | Estimate: no runtime flag branch/path debt
- [x] Gate static material data refresh | DOD: renderer now refreshes atlas textures, atlas grid, and depth scale only when material/data are dirty or changed; `Tick` no longer reads ScriptableObject atlas fields every frame on steady state | Rejected: cached compares still paying SO property access and Vector4 construction per tick | Estimate: removes cold metadata polling from steady-state Tick, profiler pending
- [x] Gate floating-origin material write | DOD: renderer writes `_GlobalFloatingOffset` only on material change or origin-shift/value change | Rejected: per-frame material vector write for a value that changes only at origin shifts | Estimate: saves one material vector upload per active SHINOBU renderer on non-shift frames, profiler pending
- [x] Re-run shader/code static gates after Loop 9 | DOD: self-audit XML parses; both shaders report `CBUFFER_ABI_OK=True`; runtime capture scan is empty; hot DTO/job property scan is empty; forbidden renderer/shader residue scan is empty | Rejected: relying on pre-CBUFFER scans | Estimate: static proof only
- [x] Re-run diff whitespace gate after Loop 9 | DOD: `git diff --check` reports only LF-to-CRLF working-copy warnings, no whitespace errors | Rejected: leaving shader patch unverified | Estimate: static only
- [x] Compile gate rechecked after Loop 9 | DOD: CPU sample 100%, compiler process count 0 | Rejected: launching build/import under explicit >50% CPU ban | Estimate: protects parallel agent iteration

## Loop 10 - Renderer Rebind / Validator Compile-Risk Pass

- [x] Re-extract SHINOBU_212 XML block | DOD: CLI regex extraction from `CURRENT_BATCH.md` returned the full `<AGENT_PROMPT id="SHINOBU_212">` with 20 tasks | Rejected: relying on compacted chat | Estimate: 65 us static recall
- [x] Fix indirect args stale-cache risk | DOD: `EnsureIndirectArgsBuffer` now resets `_argsMesh` and `_lastArgsInstanceCount` after new args-buffer allocation and unlocks the write lock in `finally` | Rejected: assuming old mesh/count cache remains valid after `_argsBuffer` release/reallocation | Estimate: prevents zero/stale indirect draw args after buffer recreation; profiler pending
- [x] Fail closed on missing atlas payload | DOD: `ApplyStaticDataToMaterialIfNeeded` now returns `false` when `HectonOctahedralImpostorData`, albedo-depth atlas, or normal-depth atlas is missing, and `Tick` returns before draw | Rejected: drawing with stale material atlas state from a previous baked payload | Estimate: prevents wrong horizon impostor draw; no intentional frame-cost claim
- [x] Reset release-side renderer state | DOD: `ReleaseResources` resets args mesh/count, instance counters, matrix upload counters, visible-stream state, bounds override, and static payload validity | Rejected: leaving stale state behind released GPU buffers | Estimate: removes rebind hazard after disable/destroy cycles
- [x] Fix static validator shadowing compile risk | DOD: `ScanBillboardAssets` local file array renamed from `files` to `paths`, leaving the `StringBuilder files` output parameter unshadowed | Rejected: accepting CS0136 risk in editor validator | Estimate: compile-risk removal, editor-only
- [x] Re-run static gates after Loop 10 | DOD: self-audit XML parses; runtime capture scan is empty; renderer/shader forbidden-token scan is empty; hot DTO/job property scan is empty; `git diff --check` reports only LF-to-CRLF warnings | Rejected: visual inspection only | Estimate: static proof only
- [x] Compile gate rechecked after Loop 10 | DOD: CPU sample 100%, compiler process count 0 | Rejected: launching build/import under explicit >50% CPU ban | Estimate: protects parallel agent iteration

## Loop 11 - NaN / Shader Payload Vaccination Pass

- [x] Re-extract SHINOBU_212 XML block | DOD: CLI regex extraction returned the full block and `Task \d\d:` count is 20 | Rejected: relying on stale task memory | Estimate: 65 us static recall
- [x] Harden impostor DTO entry points | DOD: `OctahedralImpostorInstance.Create`, `ToUniverseBounds`, `ImpostorConfigDTO.Create`, `ShouldUseImpostor`, and `ResolveContinuousEnterDistanceMeters` now clamp or replace non-finite center, size, depth scale, distance, fade, and quality values; invalid quality falls back to minimum-survival cost | Rejected: letting NaN payload reach GPU buffers | Estimate: correctness hardening; profiler pending
- [x] Harden Burst bake jobs | DOD: `CalculateCaptureAnglesJob` sanitizes bounds center/extents/padding/near clip before matrix generation; `GenerateMockCaptureTargetJob` sanitizes center/extents/shaped point data before writing mock points | Rejected: trusting imported prefab bounds | Estimate: prevents invalid editor bake matrices; profiler pending
- [x] Harden shader runtime path | DOD: `Hecton_Impostor.hlsl`, active shader, and legacy shader now use finite fallbacks for view normalization, atlas grid, visible matrix stream, instance payload, quality, samples, lighting, fog, and `SV_Depth` | Rejected: clipping after NaN already contaminated depth/color | Estimate: prevents far-horizon NaN propagation; profiler pending
- [x] Harden compute pack/dilation path | DOD: `PackImpostorAtlas.compute` and `DilateImpostorEdges.compute` sanitize atlas dimensions, source samples, masks, normals, depth, and dilation output | Rejected: assuming replacement shader captures cannot emit non-finite pixels | Estimate: editor-only robustness
- [x] Static gates after Loop 11 | DOD: targeted scans show no raw `saturate(_HectonGlobalQualityWeight)`, `Mathf`, `double.Is*`, raw `BoundsCenter +`, raw `Center + shaped`, or raw `math.max(Extents...)` in the SHINOBU files; sample texture reads are wrapped by finite guards; `git diff --check` reports only LF-to-CRLF warnings | Rejected: visual inspection only | Estimate: static proof only
- [x] Compile gate rechecked after Loop 11 | DOD: CPU sample 100%, compiler process count 0 | Rejected: launching Unity import/build under explicit >50% CPU ban | Estimate: protects parallel agent iteration

## Loop 12 - Reversed-Z Depth Bias Law Pass

- [x] Re-read status/rationale and SHINOBU prompt before patch | DOD: `Status_SHINOBU_212.md`, `Rationale_SHINOBU_212.md`, and the current XML block were reopened from disk | Rejected: patching from chat memory | Estimate: 70 us static recall
- [x] Re-check local render mandate for reversed-Z bias | DOD: `REND_URP_Graphics_HotPath_Optimization_HLOD.txt` Section10 states reversed-Z bias must be added, not subtracted | Rejected: accepting prior shader sign because it was already finite-guarded | Estimate: prevents far-horizon depth sign regression; profiler pending
- [x] Patch active and legacy impostor shader depth sign | DOD: `Hecton_HLOD_Impostor.shader` and `Hecton_OctahedralImpostor.shader` now add `depthOffset` under `UNITY_REVERSED_Z` | Rejected: runtime mesh fallback, physics depth proxy, or keeping the wrong sign behind a branch | Estimate: avoids incorrect occlusion/fog/DoF ordering on reversed-Z targets
- [x] Static gate after Loop 12 | DOD: targeted scan finds both reversed-Z branches use `deviceDepth + depthOffset` and no `deviceDepth - depthOffset` remains in the two impostor shaders | Rejected: visual inspection only | Estimate: static proof only
