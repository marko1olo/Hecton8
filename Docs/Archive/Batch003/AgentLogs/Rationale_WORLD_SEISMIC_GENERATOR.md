# Rationale_WORLD_SEISMIC_GENERATOR

Status: PENDING VERIFICATION

## Intake Decisions

Problem: The world tide/seismic work touches environment, shader state, voxel events, audio, player camera, and telemetry. Direct class dependencies would create cross-domain coupling during a multi-agent batch.
Solution: Use `GlobalRegistry` interfaces and `GlobalSignals` value payloads. Add `ISeismicDirector` as the single service surface and keep implementation details inside `HectonSeismicTideDirector`.
Rejected Alternatives: Direct references to camera, voxel, audio, or thermodynamics managers. Standard Unity singleton access is explicitly prohibited and unsafe under parallel agent edits.
Scalability potential: Low uses CPU camera/audio fake only; Middle adds deterministic shader uniform; High and Ultra use saved cycles for more visible shake/fog/rumble channels without adding gameplay physics truth.
Hardware Impact: Estimated i3/MX350 gain is avoiding object discovery and physics destruction, keeping seismic/tide evaluation under ~20us at SlowTick cadence.

Problem: Earthquakes and tides could be modeled as physical ocean/cave simulation, but gameplay requires deterministic perceptual output, not geophysical truth.
Solution: Deterministic triangle/sine harmonics and event fakes: camera jitter, shader offset, rumble, pooled rockfall signal.
Rejected Alternatives: Rigidbody cave collapse, dynamic water mesh deformation, per-chunk destruction. Too slow, non-deterministic, and not required for player belief.
Scalability potential: Low = harmonic tide scalar and camera jitter. Middle = kelp Y offset. High = shader vertex jitter. Ultra = richer rumble/debris cadence and stronger visual overkill.
Hardware Impact: Estimated low-end saving is >0.1ms/frame compared with any continuous terrain/ocean physics path; exact profiler proof absent.

## Architecture Decisions

Problem: Task 3 requests `Hecton8.Environment` asmdef isolation, but the project currently has no `Hecton8.Environment.asmdef`; existing Environment files compile under `Hecton8.Core` and existing weather code already depends on Core/Unity runtime surfaces.
Solution: Do not create a fake asmdef. Keep seismic runtime in the existing compile topology, add the contract to Core contracts, and avoid creating any new dependency edge from environment into camera, world, audio, or thermal concrete classes.
Rejected Alternatives: Creating a folder-level Environment asmdef would pull existing weather files into a new assembly and require broad references, violating the requested dependency shape. Creating an unused dummy asmdef would be false compliance.
Scalability potential: Low/Middle/High/Ultra remain controlled by `GlobalRegistry` quality and math precision flags, independent of assembly layout.
Hardware Impact: Runtime impact 0us. Build-risk reduction is material because the existing generated project remains coherent.

Problem: Tremor effects need to reach camera, audio, geysers, and debris without manager calls.
Solution: Add a fixed-size 32-byte `SeismicSignal` to `GlobalSignals`, with queue support for consumers that drain and latest-cache support for systems that only need the newest state.
Rejected Alternatives: MonoBehaviour events, C# delegates, or direct references. They allocate, couple domains, or break deterministic batch ordering.
Scalability potential: Low consumes camera/audio fields only. Middle adds thermal scalar. High/Ultra can drain queued events for richer presentation without changing producer code.
Hardware Impact: Estimated i3/MX350 gain 5-10us/event by avoiding Unity object lookup and delegate dispatch.

Problem: Tide height must affect global AUP water logic and sargassum without creating a second water authority.
Solution: Reuse `CelestialRuntimeSnapshot` as the global tide carrier and update `TideHeightMeters`, `TideHigh01`, and `TidePullVector` from a deterministic three-harmonic solver.
Rejected Alternatives: New tide singleton or direct water-system reference. Both create authority conflicts.
Scalability potential: Low uses only water-level scalar. Middle offsets sargassum. High/Ultra can use tide pull for foam/current visuals.
Hardware Impact: Solver estimate ~14us per evaluation on low-end CPU; avoids continuous water simulation.

Problem: Cave collapse must sell seismic force but real destruction is outside the frame budget and outside this domain.
Solution: Emit exactly three deterministic `DebrisSpawnSignal` rockfall fakes when intensity crosses high threshold for a new hour bucket.
Rejected Alternatives: Voxel carving, rigidbody rock generation, nearest chunk mutation. These are cross-domain and can exceed 0.1ms.
Scalability potential: Low spawns minimal fake debris. Middle adds audio rumble. High/Ultra can expand debris visuals in consumers without changing the deterministic trigger.
Hardware Impact: Estimated >100us/event saved on i3/MX350 by avoiding chunk rebuild/destruction.

Problem: Shader-side quake movement can be expensive on low-tier hardware but visually valuable on high-tier hardware.
Solution: Push `_HectonWorldShake` globally and disable it for Low, MX350, unknown tier, low-memory profile, or low math precision. Camera jitter remains active.
Rejected Alternatives: Per-material updates and always-on vertex displacement. Per-material edits risk allocations; always-on displacement wastes weak GPU time.
Scalability potential: Low = camera only. Middle = small global vertex offset. High = stronger displacement and fog response. Ultra = visual overkill through shader consumers.
Hardware Impact: Estimated 35us/frame saved on MX350-heavy scenes by zeroing the shader uniform.

Problem: Tremors should feel stronger in the abyss without making camera control worse.
Solution: Resolve player AUP through `GlobalRegistry.Player`; below -500m multiply rumble by 1.5 and camera jitter by 0.5.
Rejected Alternatives: Uniform intensity and local Y checks. Uniform intensity loses depth identity; local Y breaks AUP semantics.
Scalability potential: Low still gets audio identity; High/Ultra can add extra abyss-only post effects from the same signal fields.
Hardware Impact: Estimate 2us/solve for one AUP read and branch.

Problem: Post-mortem diagnosis requires the last 300 frames of high-level state with no hot-path allocation.
Solution: Allocate one persistent `NativeArray<SeismicTideTelemetryEntry>` ring at bootstrap; write `TideLevel`, `LastTremorIntensity`, direction, flags, and sequence; dump to `Docs/AgentLogs/Dump_WORLD_SEISMIC_GENERATOR.bin` only after invalid finite checks.
Rejected Alternatives: Managed list, text logs per frame, or no black box. Managed logs allocate and cannot explain NaN failure deterministically.
Scalability potential: All tiers share the same diagnostic ring. Ultra can add more consumers, not more producer heap churn.
Hardware Impact: Estimate 1us/SlowTick and 0B/frame; dump path is exceptional only.

Problem: Core compile was blocked by stale generated project includes for existing source files, not by seismic syntax.
Solution: Add the existing source files required by current dirty workspace to `Hecton8.Core.csproj`, including the new seismic director and already-present runtime files referenced by current code.
Rejected Alternatives: Reverting other agents, editing generated code around missing types, or stopping on first compile failure.
Scalability potential: Runtime unchanged. Build topology remains broad but coherent.
Hardware Impact: Runtime impact 0us.

## OMEGA POLISH CHANGES

Problem: Polish mandate required replacing avoidable honest math and floating divisions in the tide/seismic solver.
Solution: Kept the cinematic cheat model: triangle-wave tremors, three harmonic tide waves, deterministic debris/audio/camera signals. Replaced solver-time `/ 3600` and period divisions with `HourSecondsRcp` and prime-period reciprocals. Tide solver uses `math.sincos`; normalization uses `math.rsqrt`.
Rejected Alternatives: LUT asset, real ocean physics, real cave collapse, or refactoring the solver into jobs without profiler evidence. A LUT would add asset lifetime and sampling policy without improving the three-wave result.
Scalability potential: Low = camera/audio signal and zero shader displacement. Middle = sargassum tide offset. High = CoreLit vertex offset. Ultra = consumers can add visual overkill from the same deterministic signal.
Hardware Impact: Estimated i3/MX350 gain 4us/solve from reciprocal multiplies and no physical simulation.

Problem: Telemetry dump failure logging still built a concatenated string on an exceptional path.
Solution: Removed exception-message concatenation and wrapped the editor-only dump failure log in `#if UNITY_EDITOR`.
Rejected Alternatives: Runtime string formatting or swallowing all dump failures silently in editor.
Scalability potential: Same behavior across tiers; production player avoids dump-failure string churn.
Hardware Impact: 0B/frame; exceptional path only.

Problem: Cross-domain files were edited by necessity and needed ownership justification.
Solution: Core files received contracts, registry service slot, and signal queue; bootstrap initializes the service; camera/thermal/sargassum consume only `GlobalSignals` or `CelestialRuntimeSnapshot`; shader consumes a single global uniform. No concrete manager dependency was added.
Rejected Alternatives: Direct singleton calls, scene searches, or moving camera/thermal logic into the environment director.
Scalability potential: Consumers can scale independently per tier while producer remains deterministic.
Hardware Impact: Estimated 5-35us/frame/event saved depending on consumer path by avoiding lookup and per-material edits.

Problem: Final build verification is globally red after polish.
Solution: Ran `dotnet build Hecton8.Core.csproj -v:minimal --no-restore`. Build reached Core and failed on `Assets/_Project/Scripts/LaserCutter.cs(1424,66): CS1061 'string' does not contain a definition for 'AsSpan'`. No errors were reported from `HectonSeismicTideDirector`, `GlobalSignals`, registry contracts, camera jitter, thermal sync, sargassum tide, or CoreLit changes.
Rejected Alternatives: Editing `LaserCutter.cs` across domain or masking the error. The failure is not seismic/tide ownership.
Scalability potential: Runtime unchanged.
Hardware Impact: Runtime impact 0us.

Final Git Diff:
- Modified: `Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl`
- Modified: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- Modified: `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- Modified: `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
- Modified: `Assets/_Project/Scripts/Core/GlobalSignals.cs`
- Modified: `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`
- Modified: `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
- Modified: `Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs`
- Untracked new: `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs`
- Untracked new: `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs.meta`
- Untracked new: `Docs/Tasks/Status_WORLD_SEISMIC_GENERATOR.md`
- Untracked new: `Docs/AgentLogs/Rationale_WORLD_SEISMIC_GENERATOR.md`

## PATIENT RECHECK 2026-05-13

Problem: AGENTS Rule-09C forbids hot-path reads from `GlobalRegistry.*` convenience properties. The seismic director still resolved dispatcher, world seed, player runtime, quality tier, math precision, low-memory flag, and celestial snapshot from helpers called by `Tick()`.
Solution: Added cached runtime fields refreshed during explicit initialization, `OnEnable`, and `SlowTick`; `Tick()` now evaluates from cached dispatcher/world seed/player/quality/celestial state and only publishes the shader uniform. SlowTick still refreshes and broadcasts signals/celestial tide. Player AUP is resolved once per evaluation and reused for depth attenuation, rumble origin, and deterministic debris origin.
Rejected Alternatives: Polling registry from `Tick()` because property reads are cheap; adding direct references to camera/audio/thermal systems; launching a build against user instruction.
Scalability potential: Low/MX350 keeps cached shader-disable state and camera-only shake. Middle/High/Ultra keep smooth per-frame shader jitter without service polling. SlowTick controls live quality/state refresh, avoiding quality-flag flicker.
Hardware Impact: Estimated i3/MX350 gain 4-8us/frame/event from avoiding repeated registry/property reads and duplicate player transform/AUP conversions; measured profiler proof absent.

Problem: Migratory sargassum tide coupling read `GlobalRegistry.CelestialRuntimeSnapshot` inside every AUP-to-runtime island conversion, including scheduling, spatial publishing, kill zones, and nearest-island queries.
Solution: Added `_migratorySargassumTideHeightMeters` cached by `RefreshMigratorySargassumTideSnapshot()` once per migratory lane SlowTick using the celestial sequence. Runtime position conversion now adds the cached scalar only.
Rejected Alternatives: Keeping per-island registry reads; moving tide ownership into the scatter director; querying the seismic director directly.
Scalability potential: Low/Middle gets cheap scalar offset. High/Ultra can make consumers add richer canopy presentation from the same cached tide scalar without adding registry reads per island.
Hardware Impact: Max 24 islands currently means up to dozens of registry reads removed per migratory lane tick/query cycle; estimated 2-6us per active lane cycle saved on low-end CPU, static-only.

Problem: User requested recheck and improvement without build.
Solution: Ran static-only audits and did not launch `dotnet build`. Focused checks found no `Random.Range`, `UnityEngine.Random`, `EnvironmentManager.Instance`, `Earthquake.Instance`, `Camera.Shake`, managed `foreach`, `string.Format`, `.ToString()`, string interpolation, `new List`, `new Dictionary`, LINQ, `GameObject.Find`, `Resources.Load`, or `StartCoroutine` in the patched seismic/sargassum surfaces. Brace counts are balanced.
Rejected Alternatives: Full compile, Unity refresh, or cross-domain LaserCutter fix.
Scalability potential: Verification state remains PENDING until Unity/profiler/import data exist.
Hardware Impact: Runtime impact from audit 0us.

## SECOND PATIENT RECHECK 2026-05-13

Problem: Per-frame `Tick()` still evaluated the three-harmonic tide solve even though tide changes slowly and celestial tide publication is SlowTick-cadence.
Solution: Added `_cachedTide` and `_hasCachedTide`. `InitializeService()` and `SlowTick()` refresh the tide solve; per-frame `Tick()` reuses the cached tide and only evaluates the seismic state needed for shader jitter.
Rejected Alternatives: Keeping three `math.sincos` tide calls per frame or moving tide to real water simulation. The player cannot perceive sub-frame tide phase error at this cadence.
Scalability potential: Low/MX350 keeps tide as scalar and shader shake disabled. Middle/High/Ultra keep per-frame quake shader response while tide remains stable and cheap. Saved CPU can be spent by consumers on richer high-tier debris/fog/audio.
Hardware Impact: Estimated 8-15us/frame saved on i3/MX350 by removing three per-frame tide harmonic calls; profiler proof absent.

Problem: The seismic director carried a `DefaultExecutionOrder` despite being explicitly bootstrapped and tick-dispatched.
Solution: Removed the attribute. Runtime order remains owned by `GameBootstrapper` plus dispatcher registration, not Unity script execution order.
Rejected Alternatives: Keeping a magic execution number for a system with no Unity `Update`.
Scalability potential: No visual change; lower architecture risk across scene/bootstrap edits.
Hardware Impact: Runtime impact 0us.

Problem: Shader world-shake LOD state could flip immediately if quality/low-memory/math precision state changed.
Solution: Added `ShaderShakeLodHysteresisSeconds = 2.5` and pending-state fields. Runtime applies the first resolved state immediately, then requires the requested opposite state to remain stable before switching.
Rejected Alternatives: Immediate flip, which violates the state-hysteresis mandate and risks visible vertex-shake flicker.
Scalability potential: Low/MX350 remains cheap and stable. High/Ultra keeps visual overkill without oscillating if global quality state is unstable.
Hardware Impact: Estimate 0-2us refresh overhead at SlowTick cadence; prevents visual instability and shader uniform churn.

Problem: User requested continued improvement without build.
Solution: No `dotnet build` was launched. Focused PowerShell `Select-String` scans over patched seismic/sargassum files returned no forbidden random/singleton/camera-shake/foreach/string/LINQ/scene-search/coroutine/default-execution-order hits. Math scan shows `math.sincos` and `math.rsqrt` in the director, no `math.sqrt` or `math.normalize`. Brace counts: director 78/78, sargassum 60/60.
Rejected Alternatives: Full compile or Unity import validation against explicit user instruction.
Scalability potential: Status remains PENDING VERIFICATION until Unity/profiler/import proof exists.
Hardware Impact: Runtime impact from audit 0us.
