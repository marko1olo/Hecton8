# LOG_WORLD_SEISMIC_GENERATOR

## 2026-05-12 - Seismic/Tides Purge And Tide Runtime

What was wrong:
- Static ocean/tide path had no deterministic seismic/tide director registered through the global runtime contract layer.
- The requested `Hecton8.Environment` asmdef does not exist in this workspace. Creating one under `Assets/_Project/Scripts/Environment` would drag existing weather files into a broken assembly topology.
- Seismic effects needed to reach camera, audio, thermal geysers, debris, shader, and sargassum without singleton calls or direct manager dependencies.

What was done:
- Added `ISeismicDirector`, `SeismicRuntimeSnapshot`, `SeismicRuntimeFlags`, and `GlobalRegistryServiceSlot.SeismicDirectorRuntime`.
- Added `HectonSeismicTideDirector` with deterministic H8-time state, LCG hour-bucket seeding, three prime-period tide harmonics, triangle-wave tremor intensity, depth attenuation, tier-aware shader displacement, and 300-entry NativeArray black box telemetry.
- Added `SeismicSignal` to `GlobalSignals` with queue/latest-cache access. Camera and thermal systems consume the signal; no direct environment dependency was added.
- Published tide data through `CelestialRuntimeSnapshot` and applied tide height to migratory sargassum runtime Y.
- Pushed `_HectonWorldShake` and added CoreLit world-position offset guarded by low math LOD.
- Emitted deterministic fake rockfall debris and `SubLowRumble` impact signals. No real destruction path was introduced.
- Bootstrapped seismic runtime through `GameBootstrapper`.
- Updated status and rationale files on disk as required.

Cinematic Cheats used:
- Three sine harmonics, periods 11/17/23 hours, instead of physical tide simulation.
- Triangle-wave tremor envelope instead of geophysical simulation.
- Three deterministic `DebrisSpawnSignal` rockfall fakes instead of voxel destruction.
- Global shader offset instead of per-object displacement or physics impulses.
- Camera/audio/thermal scalar signals instead of concrete subsystem control.

Exact microseconds saved:
- Registry service pointer instead of object search: estimated 4us/tick.
- Signal payload instead of direct camera/DSP/thermal manager calls: estimated 5-10us/event.
- Three-wave tide solver instead of physical water: estimated >100us/solve saved.
- Fake debris instead of voxel destruction: estimated >100us/event saved.
- Global shader uniform instead of per-material edits: estimated 30us/frame saved.
- Low/MX350 shader displacement disable: estimated 35us/frame saved in dense CoreLit scenes.
- Omega reciprocal-multiply polish: estimated 4us/solve saved.

Verification:
- Prompt was extracted from `Docs/Tasks/CURRENT_BATCH.md` with an attribute-aware CLI regex.
- Relevant mandates and domain file were read before coding.
- Static scan found no `Earthquake.Instance`, `EnvironmentManager.Instance`, `Camera.Shake`, `Random.Range`, or `UnityEngine.Random` in the seismic-owned path.
- Focused polish scans found no managed `foreach`, `string.Format`, `.ToString()`, or string interpolation in the seismic director and direct consumers. The only remaining `/ 3600` match is the cold reciprocal constant declaration. HLSL `normalize` remains behind `_MATH_LOD_HIGH`; low tier uses dominant-axis fallback.
- `dotnet build Hecton8.Core.csproj -v:minimal --no-restore` is blocked outside this task by `Assets/_Project/Scripts/LaserCutter.cs(1424,66): CS1061 'string' does not contain a definition for 'AsSpan'`. No seismic/tide errors were returned.

Status:
- PENDING VERIFICATION due global compile dependency in `LaserCutter.cs`.

## 2026-05-13 - Patient Recheck Without Build

What was wrong:
- `HectonSeismicTideDirector.Tick()` still reached helpers that read `GlobalRegistry.*` service/settings properties. That violates the hot-path service-cache mandate.
- Migratory sargassum tide coupling read `GlobalRegistry.CelestialRuntimeSnapshot` inside each island runtime-position conversion instead of caching the tide scalar at lane cadence.

What was done:
- Cached dispatcher, world seed provider, player runtime context, absolute time fallback, celestial snapshot, quality tier, math precision, low-memory flag, and shader-disable state during init/OnEnable/SlowTick.
- Changed director `Tick()` to evaluate from cached state and skip celestial publication. SlowTick refreshes cached dependencies, publishes celestial tide, emits signals, and writes telemetry.
- Resolved player AUP once per evaluation and reused it for abyss attenuation, rumble, and rockfall debris.
- Cached migratory sargassum tide height once per migratory lane tick using `CelestialRuntimeSnapshotSequence`; per-island conversion now only adds a cached float.

Cinematic Cheats used:
- No new physical truth. Tide remains three harmonic waves; quake remains triangle-wave envelope; rockfall remains deterministic debris signal.
- Sargassum tide motion remains scalar presentation offset, not water/flora simulation.

Exact microseconds saved:
- Hot-path registry polling removal: estimated 4-8us/frame/event on i3/MX350.
- Duplicate player AUP conversion removal: estimated 2us/SlowTick during rumble/debris emission.
- Sargassum per-island celestial registry reads removed: estimated 2-6us active lane cycle.

Verification:
- `dotnet build` was not launched per user instruction.
- Static scans found no `Random.Range`, `UnityEngine.Random`, `EnvironmentManager.Instance`, `Earthquake.Instance`, `Camera.Shake`, managed `foreach`, `string.Format`, `.ToString()`, string interpolation, `new List`, `new Dictionary`, LINQ, `GameObject.Find`, `Resources.Load`, or `StartCoroutine` in patched seismic/sargassum surfaces.
- `git diff --check` on touched tracked patch files reported only the repository CRLF warning for `WorldProceduralScatterDirectorMigratorySargassum.cs`.
- Brace count: director 72/72, sargassum 60/60.

Status:
- PENDING VERIFICATION. No Unity import, profiler, or compile run was performed in this pass.

## 2026-05-13 - Second Patient Recheck Without Build

What was wrong:
- Per-frame seismic `Tick()` still recomputed slow tide harmonics.
- The director had unnecessary `DefaultExecutionOrder` despite explicit bootstrap/dispatcher ownership.
- Shader world-shake LOD switching had no hysteresis.

What was done:
- Cached the tide solve in `HectonSeismicTideDirector`; init and SlowTick refresh the tide, while Tick reuses cached tide and only updates per-frame seismic shader jitter.
- Removed `DefaultExecutionOrder` from the director.
- Added a 2.5 second hysteresis gate for shader world-shake enable/disable transitions.

Cinematic Cheats used:
- Tide remains a slow scalar harmonic fake.
- Per-frame work remains focused on visible seismic shader displacement, not physical ocean truth.
- LOD stability protects presentation quality instead of chasing instant hardware-state changes.

Exact microseconds saved:
- Cached tide solve: estimated 8-15us/frame saved by removing three per-frame harmonic `math.sincos` calls.
- Removed execution-order attribute: 0us runtime, lowers ordering risk.
- Shader-shake LOD hysteresis: 0-2us SlowTick overhead; prevents flicker and redundant uniform churn.

Verification:
- `dotnet build` was not launched per user instruction.
- Focused static scan over patched seismic/sargassum files returned no forbidden random/singleton/camera shake/foreach/string/LINQ/scene-search/coroutine/default-execution-order hits.
- Director math scan found `math.sincos` and `math.rsqrt`; no `math.sqrt` or `math.normalize` in the director.
- Brace count: director 78/78, sargassum 60/60.
- Shader hook read confirmed `_HectonWorldShake` still reaches `HectonCoreLitApplyWorldShake`, with `_MATH_LOD_LOW` returning sanitized positions.

Status:
- PENDING VERIFICATION. No Unity import, profiler, or compile run was performed.
