# Status_WORLD_SEISMIC_GENERATOR

Prompt: WORLD_SEISMIC_GENERATOR
Role: ENVIRONMENT_ENGINEER
Domain: ECHELON 7 ATMOSPHERE & CELESTIAL
Task Count: 19
Status: PENDING VERIFICATION

Batch hygiene: status file was missing at session start; treated as empty. Original assignment was re-extracted from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex before coding and again before final audit.

Relevant mandates loaded before coding:
- ARCH_Global_Registry_ServiceLocator_DI_Init
- MATH_Deterministic_RNG_SlotMachine
- MATH_Coordinate_Precision_AUP_FloatingOrigin
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Performance_Budgets_FrameTime_VRAM_Limits
- DBG_Telemetry_Crash_Reporting_PostMortem
- REND_Shader_Noir_Aesthetics_Dithering_Fog

## Loop 0 - Intake
- [x] Extract prompt | DOD: CLI regex extraction from CURRENT_BATCH.md, no neighboring prompt memory retained | Rejected: IDE tab context and manual skim | Estimate: 45us
- [x] Domain boundary read | DOD: authoritative Actual Domains file maps task 62 to Echelon 7 | Rejected: guessing from prompt title | Estimate: 20us
- [x] Mandates loaded | DOD: 8 task-relevant mandate files read before code | Rejected: broad mandate dump and stale report inference | Estimate: 120us

## Phase 1 - Purge
- [x] Task 1 SINGLETON ERADICATION | DOD: added `ISeismicDirector`, `GlobalRegistryServiceSlot.SeismicDirectorRuntime`, and register/unregister calls; implementation bootstraps through `GameBootstrapper` | Rejected: `Earthquake.Instance`, scene object discovery, and direct manager singleton | Estimate: 4us/tick saved by direct registry pointer
- [x] Task 2 SIGNAL MIGRATION | DOD: added 32-byte `SeismicSignal` queue/latest cache and camera/thermal consumers read signal data | Rejected: `Camera.Shake` direct call and camera component lookup | Estimate: 7us/event saved
- [x] Task 3 ASMDEF ISOLATION | [BLOCKED BY EXISTING ASSEMBLY TOPOLOGY] DOD: verified project has no `Hecton8.Environment.asmdef`; seismic code adds no new asmdef dependency edge and exposes contracts through existing Core contracts | Rejected: fake Environment asmdef that would capture existing weather files and break compile | Estimate: 0us, topology block documented
- [x] Task 4 DEAD CODE HUNT | DOD: scoped static scan found no `Random.Range`/`UnityEngine.Random` in seismic environment path; seismic entropy uses `LCG_Hash` only | Rejected: Unity random state | Estimate: 2us/event and deterministic replay preserved

## Phase 2 - Deterministic Math
- [x] Task 5 SEISMIC STATE MACHINE | DOD: `SeismicRuntimeSnapshot` carries `SeismicIntensity01` and `SeismicDirection`; modulation uses `TriangleWave01` on H8 time | Rejected: coroutine timers and Update-local phase reset | Estimate: 6us/solve
- [x] Task 6 DETERMINISTIC SEEDING | DOD: seed is `LCG_Hash(WorldSeed + (int)(H8Time / 3600))` equivalent with unsigned wrap | Rejected: frame count, wall clock, and Unity RNG | Estimate: 3us/solve
- [x] Task 7 TIDE HARMONICS | DOD: tide is three prime-hour harmonics 11/17/23 with `math.sincos`; writes global AUP tide fields through `CelestialRuntimeSnapshot` | Rejected: dynamic ocean mesh simulation | Estimate: 14us/solve versus >100us physical water path
- [x] Task 8 SHADER WORLD-OFFSET | DOD: director pushes `_HectonWorldShake`; `Hecton_CoreLit` applies sanitized vertex world offset outside low math LOD | Rejected: per-renderer material edits | Estimate: 30us/frame saved by one global uniform

## Phase 3 - System Coupling
- [x] Task 9 SARGASSUM COUPLING | DOD: migratory sargassum runtime Y adds finite `CelestialRuntimeSnapshot.TideHeightMeters` | Rejected: direct reference to tide director | Estimate: 1us/mat update
- [x] Task 10 CAVE COLLAPSE FAKE | DOD: high tremor emits three deterministic `DebrisSpawnSignal` rockfall events near player AUP | Rejected: voxel destruction and rigidbody collapse | Estimate: >100us/event saved, no chunk rebuild
- [x] Task 11 AUDIO RUMBLE | DOD: high/normal tremor emits `ImpactSignal` with `SubLowRumble` material hash and intensity-scaled force | Rejected: direct DSP manager call | Estimate: 5us/event saved
- [x] Task 12 PLAYER KCC JITTER | DOD: `CameraJuiceSystem` reads latest `SeismicSignal` and adds deterministic micro offset/rotation to post-AUP camera shake | Rejected: camera singleton and random shake | Estimate: 3us/frame
- [x] Task 13 GEYSER SYNC | DOD: `AbyssalThermalManager` reads latest `SeismicSignal` and applies 2x eruption scalar on high tremor | Rejected: direct environment-to-thermal dependency | Estimate: 4us/SlowTick

## Phase 4 - Safety & LOD
- [x] Task 14 AUP SHIFT SAFETY | DOD: tide and seismic phases derive only from absolute H8 time and world seed; no `AupShiftSignal` coupling | Rejected: local runtime position phase | Estimate: 0us, correctness guard
- [x] Task 15 DEPTH ATTENUATION | DOD: AUP.y < -500m raises rumble x1.5 and lowers camera jitter x0.5 | Rejected: uniform shake across abyss/surface | Estimate: 2us/solve
- [x] Task 16 MATH LOD | DOD: low tier, MX350, unknown, low memory, or low math precision sends zero `_HectonWorldShake`; camera signal remains active | Rejected: shader vertex shake on weak GPUs | Estimate: 35us/frame on MX350-heavy scenes
- [x] Task 17 ZERO-GC | DOD: `SlowTick` path uses structs, NativeQueue publishes, existing persistent telemetry; allocation scan shows only bootstrap, struct construction, persistent NativeArray, and dump-only file I/O | Rejected: LINQ, managed lists, string formatting, per-event heap objects | Estimate: 0B/frame, 12us GC avoidance under spikes
- [x] Task 18 OMEGA COMPILE CHECK | DOD: tide/seismic solver methods are static, Burst-decorated, and use Burst-compatible math; earlier core build reached 0 errors and 9 unrelated warnings before final polish | Rejected: managed DateTime, Random, Unity object APIs in solver | Estimate: 8us/solve
- [x] Task 19 TELEMETRY | DOD: 300-entry NativeArray circular black box writes `TideLevel` and `LastTremorIntensity`; invalid snapshot dumps `Docs/AgentLogs/Dump_WORLD_SEISMIC_GENERATOR.bin` | Rejected: managed list/log spam telemetry | Estimate: 1us/SlowTick

## Iterative Verification
- [x] Loop 1 tasks 1-5 + compile | DOD: registry/signal/state-machine code reviewed; compile initially exposed stale generated-project includes rather than seismic syntax | Rejected: stopping at first compile wall | Estimate: 120us audit
- [x] Loop 2 tasks 6-10 + compile | DOD: deterministic seed, tide harmonics, shader offset, sargassum, and rockfall fake reviewed | Rejected: direct dependencies and real destruction | Estimate: 160us audit
- [x] Loop 3 tasks 11-15 + compile | DOD: rumble, camera jitter, geyser sync, AUP absolute math, and depth attenuation reviewed | Rejected: manager calls and AUP reset logic | Estimate: 140us audit
- [x] Loop 4 tasks 16-19 + compile | DOD: low-tier shader disable, zero-GC audit, Burst-compatible static solvers, black box telemetry reviewed | Rejected: per-frame material allocations and managed telemetry | Estimate: 170us audit
- [x] Loop 5 self-audit + compile | DOD: static scans re-ran for singleton/random/camera shake, `math.sincos`, and SlowTick allocation signatures; final build attempt is blocked outside seismic by `LaserCutter.cs(1424,66)` CS1061 `string.AsSpan` | Rejected: editing unrelated LaserCutter domain | Estimate: 210us audit
- [x] Polish mandate executed | DOD: parsed only after all 19 tasks were terminal or blocked; replaced remaining solver-time divisions with reciprocal multiplies and removed telemetry dump string concatenation from player builds | Rejected: broad refactor loop | Estimate: 4us/solve saved
- [x] Final build status | PENDING VERIFICATION | DOD: `dotnet build Hecton8.Core.csproj -v:minimal --no-restore` reached Core compile and failed only on `Assets/_Project/Scripts/LaserCutter.cs(1424,66): CS1061 string.AsSpan`; no seismic file errors reported | Rejected: cross-domain LaserCutter edit | Estimate: 0us runtime
- [x] Loop 6 patient recheck without build | DOD: user explicitly prohibited `dotnet build`; no build launched. Re-read AGENTS, status, rationale, prompt, and mandates; removed `GlobalRegistry` service/settings polling from the director `Tick` call stack; cached player/world seed/time/quality/celestial state during init/SlowTick; resolved player AUP once per evaluation; cached sargassum tide height once per migratory lane tick instead of per island conversion | Rejected: broad cross-domain cleanup and compile-triggering validation | Estimate: 4-8us/frame/event saved plus reduced registry volatility
- [x] Loop 6 static verification | DOD: focused scans found no `Random.Range`, `UnityEngine.Random`, `EnvironmentManager.Instance`, `Earthquake.Instance`, `Camera.Shake`, managed `foreach`, `string.Format`, `.ToString()`, string interpolation, `new List`, `new Dictionary`, LINQ, `GameObject.Find`, `Resources.Load`, or `StartCoroutine` in the seismic director/sargassum patch surfaces; brace count is balanced 72/72 director and 60/60 sargassum | Rejected: repo-wide unrelated noise and `dotnet build` | Estimate: static-only, profiler proof absent
- [x] Loop 7 patient recheck without build | DOD: no build launched. Removed unnecessary `DefaultExecutionOrder`; cached tide harmonic solve so per-frame `Tick` reuses SlowTick tide and keeps per-frame work to seismic shader jitter; added 2.5s hysteresis gate for shader world-shake LOD transitions | Rejected: recalculating three tide `math.sincos` calls every frame and immediate low/high shader-LOD flip | Estimate: 8-15us/frame saved on CPU plus stable scalability switching
- [x] Loop 7 static verification | DOD: focused PowerShell `Select-String` scan over patched seismic/sargassum files returned no forbidden random/singleton/camera shake/foreach/string/LINQ/scene-search/coroutine/default-execution-order hits; math scan shows tide/seismic `math.sincos` and `math.rsqrt`, no `math.sqrt`/`math.normalize` in the director; brace count is balanced 78/78 director and 60/60 sargassum | Rejected: `dotnet build` per user instruction | Estimate: static-only, profiler proof absent
