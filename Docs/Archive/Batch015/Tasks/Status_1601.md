# Status 1601 - ORBITAL_CELESTIAL_AND_LIGHTING_DIRECTOR

Date: 2026-06-01
Status: STATIC VERIFIED / RUNTIME VISUAL PENDING
Domain: Echelon 7 Atmosphere & Celestial / 01_ORBIT sky-lighting
Prompt Source: Docs/Tasks/CURRENT_BATCH.md <AGENT_PROMPT id="1601">
Task Count: 20

## Mandates Read

- REND_URP_Graphics_HotPath_Optimization_HLOD
- REND_Shader_Noir_Aesthetics_Dithering_Fog
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First
- OPT_Performance_Budgets_FrameTime_VRAM_Limits
- OPT_Zero_GC_Policy_AllocFree_Mandate
- DATA_Runtime_Struct_Layout_ARM64
- ARCH_Execution_Phases
- STRM_Async_Asset_Upload_Texture_Settings

## Loop 0 - Bootstrap

- [x] Extract prompt 1601 from CURRENT_BATCH.md. DOD: regex block extraction from disk, not IDE memory. Rejected: relying on chat prompt copy. Estimate: 700 us.
- [x] Read AGENTS.md, TASTE.md, Docs index, domain roster, relevant mandates. DOD: authority spine scanned before coding. Rejected: direct shader edits before mandate selection. Estimate: 1100 us.

## Tasks

- [x] Task 01: EXHAUSTIVE_CELESTIAL_SCRIPT_INQUISITION. DOD: `rg` scan over `Scripts/Prologue`, `Scripts/Environment`, and legacy `_PROLOGUE_CONTENT/Scripts` for `Transform.Rotate`, `RotateAround`, `.Rotate(`, `Update`, `FixedUpdate`, `LateUpdate`. Result: no active rotate/update loop found. Offender class is transform presentation debt in `OrbitalRelativityDirector.ApplyPresentation` lines 711-739 and prefab mesh `GasGiant_Aegir`. Rejected: deleting disabled `PlanetRotation.cs`; it already self-disables in `OnEnable`. Estimate: 2400 us.
- [x] Task 02: TEXTURE_ASSET_METADATA_AUDIT. DOD: PNG header dimension read plus `.meta` import scan. Aegir textures are 4096x2048 source but clamped to 2048 import. `Assets/_Project/Art/TEXTURES/Aegir_storms.png.meta` has Standalone BC7 (`textureFormat: 25`) but mipmaps still enabled. Prologue duplicates have mipmaps enabled and no Standalone override. `ring.png` has mipmaps off and Standalone BC7. Rejected: raw binary texture rewrite. Estimate: 3100 us.
- [x] Task 03: HLSL_RAY_INTERSECTION_MATH_PLANNING. DOD: planned ray-sphere solve using `b = dot(oc, rd)`, `c = dot(oc, oc)-r2`, discriminant branch, one `sqrt` only after confirmed hit; UV uses normalized hit normal via `rsqrt(dot(p,p))`, fast atan2 polynomial or stripe projection for low quality. Rejected: mesh sphere, raymarch atmosphere. Estimate: 1800 us.
- [x] Task 04: CBUFFER_LAYOUT_EXTRACTION_AND_ALIGNMENT. DOD: 64-byte DTO layout selected: `float4 SunDirection`, `float4 PlanetCenterRadius`, `float4 RingPlaneInner`, `float4 OrbitScalars`. Matches 16-byte HLSL lanes; no managed refs; size multiple-of-16 and multiple-of-8. Rejected: mixed scalar layout with implicit padding. Estimate: 1500 us.
- [x] Task 05: TELEMETRY_AND_REPORTING_ARCHITECTURE. DOD: report path remains `Status_1601.md`, `Rationale_1601.md`, and final `LOG_1601.md`; no useless JSON proof unless a later automated validator strictly requires it. Rejected: large `Docs/Reports/*.json` dump contradicting current user instruction. Estimate: 900 us.
- [x] Task 06: N_BODY_SIMULATION_ANNIHILATION. DOD: disabled `GasGiant_Aegir.prefab` root and MeshRenderer; changed `OrbitalRelativityDirector.ApplyPresentation` to stop moving legacy celestial transforms and keep planet/cloud/Aegir renderers disabled. Rejected: deleting YAML components and GUID-churning prefab structure. Estimate: 3200 us.
- [x] Task 07: EPHEMERIS_DATA_COORDINATOR_IMPLEMENTATION. DOD: static source search found no `EphemerisTableDTO`; used cached `ICelestialRuntimeSnapshotReadModel` as existing precomputed celestial owner route and sanitized snapshot directions into shader DTO. Rejected: direct `static_data.h8bin` read from graphics director. Estimate: 2600 us.
- [x] Task 08: ZERO_ALLOCATION_CBUFFER_BRIDGE. DOD: added explicit 64-byte `CelestialParametersDTO`; `ApplyPresentation` uploads four `Vector4` lanes by cached shader IDs during late presentation with no `new` keyword in the modified method. Rejected: per-frame property-name strings or temporary arrays. Estimate: 2900 us.
- [x] Task 09: AEGIR_SKYBOX_SHADER_MATERIALIZATION. DOD: created `Assets/_Project/Art/Shaders/Sky/Hecton_AegirSky.shader` plus `MAT_AegirSky_Master.mat`; shader draws Aegir via ray-sphere hit, samples BC7 `Aegir_storms`, and quality-gates flow-map UV distortion. Rejected: physical mesh gas giant and cubemap-only background. Estimate: 7800 us.
- [x] Task 10: PROCEDURAL_RING_AND_SHADOW_MATH. DOD: shader ring plane intersection and planet shadow projection implemented with squared-radius inner/outer tests and darkness scalar. Rejected: Unity shadow maps and ring mesh caster. Estimate: 4600 us.
- [x] Task 11: CONTINUOUS_QUALITY_ATMOSPHERE_SCALING. DOD: shader consumes `_H8GlobalQualityWeight` continuously for star density, flow contribution, rim scatter, atmosphere tint, and ring/aegir blend; no binary quality switch or variant pragma. Rejected: low/ultra keyword split. Estimate: 3100 us.
- [x] Task 12: PROCEDURAL_STARFIELD_GENERATION. DOD: `StarField` uses hashed cell projection and triangle-wave twinkle from `_Time.y`, scaled by continuous quality. Rejected: sine shimmer and texture atlas stars; shader scan confirms zero `sin(`. Estimate: 3600 us.
- [x] Task 13: HARD_SCI_FI_LIGHTING_ENFORCEMENT. DOD: `01_ORBIT.unity` and bootstrap enforce black ambient, skybox clear, cold blue hard directional light at 5.5 intensity, hard shadows, zero bounce. Rejected: ambient fill and soft cinematic area-light imitation. Estimate: 2200 us.
- [x] Task 14: LENS_FLARE_AND_BLOOM_CONFIGURATION. DOD: bootstrap enables URP camera post-processing and global Bloom profile with threshold/intensity/scatter/max-iteration count scaled by `GlobalQualityWeight`. Rejected: separate lens-flare object spam, obsolete `skipIterations`, and unconditional high-quality bloom. Estimate: 3400 us.
- [x] Task 15: BATCHED_COMPILATION_AND_SYNTAX_ASSERTION. DOD: dotnet build suppressed by current operator order plus CPU contention (`CPU_LOAD=100`, existing `dotnet` PID 25280); static assertions passed for shader forbidden calls, variant loops, hotpath allocation markers, scene YAML, asmdef JSON, and Unity console has no current 1601 shader/C# error after helper rename. Rejected: launching build into loaded host. Estimate: 4100 us.
- [x] Task 16: MOCK_EPHEMERIS_DRIFT_TEST. DOD: added `OrbitalSkyEphemerisDrift1601EditTests.cs` using `HectonCelestialEngine.EvaluateAnalyticalOrbitSnapshotForSmoke` to assert deterministic repeat, finite normalized directions, 0..1 eclipse scalar, and nonzero gas giant drift over 300 seconds. Rejected: custom ephemeris owner. Estimate: 4200 us.
- [x] Task 17: SHADER_ALU_INSTRUCTION_AUDIT. DOD: `rg` scan of `Hecton_AegirSky.shader` found zero `pow(`, `sin(`, `normalize(`, shader loops, or shader variant pragmas. Rejected: Unity shader compiler run during CPU contention. Estimate: 1700 us.
- [x] Task 18: ZERO_GC_COMPILATION_HOT_PATH_VERIFICATION. DOD: text scan of `ApplyPresentation`, `BuildCelestialParameters`, and `ApplyEclipseLighting` found no `new`, `.ToString`, `string.Format`, or interpolated string markers. Rejected: managed arrays or property-name strings in presentation. Estimate: 2600 us.
- [x] Task 19: ECLIPSE_TRIGGER_MATH_VERIFICATION. DOD: `EclipseOcclusion01` flows through `CelestialParametersDTO.SunDirection.w` to shader and into `ApplyEclipseLighting`; key directional light fades smoothly with cached `Tick(float)` delta, clamped floor, no extra truth owner. Rejected: per-frame scene search, direct `GlobalRegistry` hot polling, and `Time.deltaTime` inside tick-owned presentation. Estimate: 4100 us.
- [x] Task 20: AUTOMATED_METRIC_VALIDATOR_REPORT. DOD: machine JSON report intentionally suppressed by current operator order; evidence appended to `Status_1601.md`, `Rationale_1601.md`, and final `LOG_1601.md` with SHA-256 hashes for shader/material/scripts/test. Rejected: unused JSON dump. Estimate: 1800 us.

## Verification Ledger

- dotnet build: NOT RUN. User forbade build after minor edits; CPU contention check returned `CPU_LOAD=100` and existing `dotnet` PID 25280, so build is not authorized.
- prompt re-extract after Task 03: OK, length 19788.
- prompt re-extract before Task 06: OK after using `id="1601"[^>]*` tag matcher.
- Unity Console: initial shader helper collision fixed (`FastAtan2` renamed to `H8AegirFastAtan2`). Latest read has no 1601 shader/C# entry; remaining entries are unrelated `GeologyForgeGenerator.cs` job disposal and MCP refresh warning.
- Shader static ALU scan: PASS for new shader; zero `pow(`, `sin(`, `normalize(` matches.
- Shader variant/loop scan: PASS; zero `multi_compile`, `shader_feature`, `for(`, or `while(` in `Hecton_AegirSky.shader`.
- N-body static scan: PASS for existing assigned Prologue/Environment/legacy lanes; zero `Transform.Rotate`, `RotateAround`, `.Rotate(`, `Update`, `FixedUpdate`, `LateUpdate` matches after purge.
- Hotpath marker scan: PASS for `ApplyPresentation`, `BuildCelestialParameters`, `ApplyEclipseLighting`.
- Scene static config: PASS; `01_ORBIT.unity` has black ambient, assigned `MAT_AegirSky_Master`, directional intensity 5.5, camera post-processing enabled, bootstrap skybox material assigned.
- Texture import: PASS; `Aegir_storms.png.meta` has `enableMipMap: 0`, maxTextureSize 2048, Standalone `textureFormat: 25` BC7.
- Physical Aegir prefab: PASS; root inactive and MeshRenderer disabled.
- Asset refresh: `compile=none` refresh timed out waiting for editor readiness after 60s; console after timeout did not reproduce the 1601 shader error.
- Runtime/visual proof: not executed because host CPU is saturated and current order forbids heavyweight verification.

## APEX Integrator Continuation

- [x] Hot dependency gate converted into code: added `OrbitalApexIntegrator1601EditTests.cs` with Roslyn syntax-tree checks for hot methods, presentation phase, DataVault lock release, build process strings, and shader forbidden calls. DOD: C# test source, not JSON or chat assertion. Rejected: markdown-only proof. Estimate: 5200 us.
- [x] Presentation phase tightened: `OnEnable` and `ResetRuntimeState(true)` now call `QueueOrbitalPresentation()`; the only direct `ApplyPresentation()` call remains in `LateFrameTick`. DOD: `rg` shows `ApplyPresentation` invocation only at `LateFrameTick` and method declaration. Rejected: cold direct shader upload during enable/reset. Estimate: 1800 us.
- [x] Queue methods flattened: `QueueCapsuleAuthorityLock`, `QueueOrbitalPresentation`, `QueueShaderGlobalClear`, and `QueueRuntimeAuthorityRelease` no longer attempt registration or touch `GlobalRegistry`; dispatcher registration stays in cold enable/hot-swap routes. DOD: static scan of these methods passes for `GlobalRegistry`, `GetComponent`, `TryRegisterUpdateLane`, allocation, and string markers. Estimate: 2100 us.
- [x] Drift test compile namespace fixed: `OrbitalSkyEphemerisDrift1601EditTests.cs` now matches existing test namespace pattern with `using Hecton8.Core`. DOD: Unity console no longer reports 1601 test errors after script compile; current compile wall is unrelated DropPod `TryRegisterLate`. Estimate: 900 us.
- [x] Tick-time discipline tightened: `ApplyEclipseLighting` no longer reads `Time.deltaTime`; `Tick(float)` caches sanitized `_presentationDeltaTime`, and `LateFrameTick` presentation consumes that phase-owned value. DOD: static scan of `OrbitalRelativityDirector.cs` finds zero `Time.deltaTime` and zero `Time.fixedDeltaTime`; Roslyn APEX test now guards this. Rejected: Unity global time in tick architecture. Estimate: 1200 us.
- [x] APEX guard widened: hot-source test now includes `ApplyPresentation`, `BuildCelestialParameters`, `ApplyEclipseLighting`, and queue methods, and rejects any hot `GlobalRegistry.` expression, not only `GlobalRegistry.Get<T>()`. DOD: source guard encodes the architecture rule directly. Estimate: 1400 us.
- [x] Celestial shader upload dirty-gated: Aegir 64-byte DTO globals now upload only when the DTO changes or after clear/reset. DOD: `ApplyPresentation` calls `UploadCelestialGlobalsIfDirty`; direct `SetGlobalVector` calls moved out of the presentation body; APEX test guards the dirty gate. Rejected: uploading unchanged celestial vectors every late frame. Estimate: 1900 us.
- [x] Orbit bloom made continuous: bootstrap bloom now uses `bloomWeight = quality * quality`, zero volume weight at quality 0, fixed false high-quality filtering, and continuous threshold/intensity/scatter/max-iteration scaling. DOD: static scan finds no `quality >` in `PrologueOrbitSceneBootstrap.cs`; APEX test guards continuous bloom. Rejected: always-on minimum bloom and binary high-quality filtering. Estimate: 1500 us.
- [x] Cold camera/light budget normalized: orbit bootstrap now sets exact camera far clip, exact key light intensity, and `LightShadowResolution.FromQualitySettings` instead of preserving oversized scene values or forcing `VeryHigh`. DOD: APEX test guards no `Mathf.Max(camera.farClipPlane)`, no `Mathf.Max(light.intensity)`, and no `LightShadowResolution.VeryHigh`. Estimate: 1100 us.
- [x] Aegir shader ALU/texture pass reduced: removed `_AegirFlowTex`, removed the flow texture sample, removed branch `flowWeight >`, and removed ring-lane `sqrt`; procedural band drift now uses one band texture sample and continuous scalar drift. DOD: shader scan passes; standalone `sqrt(` count is 1; material no longer binds `_AegirFlowTex`. Estimate: 2300 us.
- [x] Legacy presentation shader globals dirty-gated: packed orbit distance/radius/speed/heat/whiteout/edge/mathLOD into 32-byte `PresentationShaderGlobalsDTO`; `ApplyPresentation` no longer calls `Shader.SetGlobalFloat` directly. DOD: static scan passes, APEX test guards DTO size, upload method, clear invalidation, and zero direct float upload from presentation. Estimate: 1700 us.
- [x] Quality and eclipse scalar sanitized: `BuildCelestialParameters` and orbit bloom now use finite-safe `ResolveQuality01`; `EclipseOcclusion01` is saturated before entering `SunDirection.w`. DOD: direct `math.saturate(HomeostasisBrain.GlobalQualityWeight)` removed from Prologue Space runtime sources, APEX test guards finite fallback. Estimate: 900 us.
- [x] Dead transform orbit fake purged: removed unused `ResolveOrbitalWindowOffset`, `ResolveGasGiantBackdropPosition`, and obsolete serialized orbit arc knobs after shader-impostor migration. DOD: source scan finds no old helper/field names and no `planetSphere.localPosition`/`gasGiantBackdrop.localPosition`. Estimate: 800 us.
- [x] Ring mask squared-radius path flattened: `Hecton_AegirSky.shader` now computes `ringInnerSq`/`ringOuterSq` once and uses `HardRingMaskSq` for ring draw and ring shadow. DOD: shader scan has `HardRingMaskSq`, no `innerRadius * innerRadius`/`outerRadius * outerRadius`, standalone `sqrt(` count remains 1. Estimate: 600 us.

## APEX Verification Ledger

- Unity script compile: requested once after Unity reported a concrete 1601 test compile error. Latest console has no 1601 source error; current external errors are `Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs` unresolved `_jobPinVault` / `JobPin*` symbols, outside domain.
- dotnet build: not launched.
- CPU throttle: latest host check returned `CPU_LOAD=20` with existing `dotnet` PIDs 23720 and 31512 still running; no additional compile/build commands were launched.
- Hot-path static scan: `Tick`, `LateFrameTick`, queue methods, `ApplyPresentation`, `BuildCelestialParameters`, and `ApplyEclipseLighting` passed for cold dependency and managed allocation markers.
- Lock scan: one `TryAcquireWriteLock`, one `ReleaseWriteLock`, release inside `finally`.
- Tick-time scan: `OrbitalRelativityDirector.cs` contains no `Time.deltaTime` or `Time.fixedDeltaTime`; eclipse fade consumes `_presentationDeltaTime`.
- Bandwidth scan: `ApplyPresentation` contains no direct `Shader.SetGlobalVector`; celestial vector globals are behind `_celestialParametersUploaded` plus epsilon comparison.
- Bloom scan: `PrologueOrbitSceneBootstrap.cs` contains no `quality >` branch; bloom intensity is `OrbitBloomFullIntensity * bloomWeight`.
- Camera/light scan: `PrologueOrbitSceneBootstrap.cs` contains no forced `LightShadowResolution.VeryHigh`, no `Mathf.Max` preservation of far clip or light intensity.
- Shader ALU scan: `Hecton_AegirSky.shader` has no `_AegirFlowTex`, no `flowWeight >`, no forbidden calls/variants, and one standalone `sqrt(` for the confirmed ray-sphere hit.
- Presentation bandwidth scan: `ApplyPresentation` contains no direct `Shader.SetGlobalFloat` or `Shader.SetGlobalVector`; legacy float globals and celestial vectors are both behind dirty-gated DTO upload methods.
- Quality input scan: runtime Prologue Space sources no longer contain direct `math.saturate(HomeostasisBrain.GlobalQualityWeight)`; finite fallback route is `math.select(1f, quality, math.isfinite(quality))`.
- Dead orbit transform scan: `OrbitalRelativityDirector.cs` no longer contains `ResolveOrbitalWindowOffset`, `ResolveGasGiantBackdropPosition`, `orbitalArcRadius01`, `hectonOrbitTurns`, or `orbitPresentationFadeDistanceMeters`.
- Ring mask scan: `Hecton_AegirSky.shader` uses `HardRingMaskSq` with precomputed `ringInnerSq`/`ringOuterSq`; standalone `sqrt(` count remains 1.
- Latest host throttle check: `CPU_LOAD=46`, existing `dotnet` PID 31512; build/test-run not launched.
- Latest Unity console read: no 1601 source errors; current external compile wall is `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs(1054,13)` missing `Hecton8.Physics.BakeMesh`, plus MCP WebSocket closure exception.
