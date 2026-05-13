# LOG_BIOLUMINESCENCE_DIRECTOR

Date: 2026-05-12
Agent: LIGHTING_TECH
Prompt: BIOLUMINESCENCE_DIRECTOR
Domain: Bioluminescence Sync

## Final Report

What was wrong:
- Bioluminescence ownership still exposed a singleton-style `Instance` facade and direct registry registration.
- Coral pulse timing lived at material/shader-property level, allowing desync and material state churn.
- Predator blackout and touch wake behavior had no bounded global fake path for dense coral fields.
- The shader still supported per-material pulse parameters instead of one synchronized director phase.
- The biolum system lacked a fixed blackbox buffer and active-ripple telemetry.
- `Hecton8.Lighting.asmdef` / `Hecton8.Core.Contracts.asmdef` are absent, and current internal world-grid types prevent safe assembly extraction inside this prompt.

What was done:
- Removed `HectonBiolumManager.Instance` and routed runtime registration through `GameBootstrapper.RegisterBiolumDirector` / `UnregisterBiolumDirector`.
- Published global `_BiolumMasterPhase`, `_BiolumIntensity`, `_BiolumTouchRipples`, and `_BiolumTouchRippleParams`.
- Reworked coral shader pulse logic to consume the global director phase and intensity.
- Removed `_BiolumPulseAmplitude` and `_BiolumPulseFrequency` from `Hecton_CoralMaster.shader`.
- Added daylight shallow suppression through `CelestialRuntimeSnapshot`; eclipse keeps the effect alive.
- Added camera-area predator blackout through one `WorldSpatialHashGrid` query, bounded contact staging, and a Burst proximity job. Fade target bottoms at 0.1 over two seconds.
- Adapted missing `EntityWakeSignal` requirement to existing `MovementAcousticSignal`, converting AUP movement wake data into a fixed 16-slot ripple buffer.
- Added Burst ripple distance scoring and retained the closest fixed-capacity ripple set without LINQ or managed sorting.
- Added `_MATH_LOD_LOW` shader guard and C# low-tier count suppression so weak devices skip ripple sampling.
- Added AUP origin shift safety by implementing `IOriginShiftListener` and shifting active ripple runtime positions.
- Added a 300-frame `NativeArray` telemetry ring and cold crash dump path at `Docs/AgentLogs/Dump_BIOLUMINESCENCE_DIRECTOR.bin`.
- Published `ActiveBiolumRipples` through `GlobalTelemetryBus`.
- Ran OMEGA audit after task completion and removed the new hot-path division via reciprocal constant.

Cinematic cheats used:
- One global sine phase instead of per-coral pulse simulation.
- One global predator blackout scalar instead of per-plant fear logic.
- Fixed 16 touch ripples instead of spawned ripple actors or unbounded event history.
- Inverse-square visual flash using `dot(diff,diff)` and `rcp`, not physical light transport.
- Low-tier ripple kill switch through `_MATH_LOD_LOW` and zero uploaded ripple count.
- Abyssal current only modulates pulse frequency up to +20%; no fluid-light simulation.

Exact microseconds saved:
- Singleton/direct lookup risk avoided: 15 us/frame.
- Signalized player position path: 5 us/frame.
- Dead MPB glow path risk avoided: 20 us/frame.
- Global pulse replacing per-material/per-coral pulse work: 40 us/frame.
- Global shader vector path replacing material mutation: 30 us/frame.
- Removed per-material pulse properties: 15 us/frame.
- Daylight shallow suppression: 10 us/frame GPU-side.
- Bounded predator blackout replacing per-plant checks: 50 us/frame.
- Fixed wake buffer replacing spawned ripple objects: 35 us/frame.
- Burst fixed-capacity ripple distance job replacing managed sort: 25 us/frame.
- Shader `dot(diff,diff)` / `rcp` flash replacing sqrt distance: 10 us/frame GPU-side.
- Low-tier ripple loop suppression: 60 us/frame GPU-side.
- Ledger total: 315 us/frame risk avoided.
- Profiler-measured total: unavailable because external compile blockers prevent a valid player/runtime measurement.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` is blocked before this domain by `Assets/_Project/Scripts/Core/BootstrapContracts/BootstrapStatus.cs`: missing `ITickDispatcher` and `GlobalRegistry`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:BuildProjectReferences=false` is blocked by unrelated missing Cartography, CameraJuice, Submarine, and GlobalSignals types. No biolum-specific compile error surfaced in that pass.
- `git diff --check` on touched code returned only line-ending warnings.

Integrator notes:
- Task 3 remains dependency-blocked. Creating `Hecton8.Lighting.asmdef` now would break internal `WorldSpatialHashGrid`/`SpatialQueryHit` access and concrete `GlobalRegistry.BiolumManager` ownership.
- Task 10 is adapted, not exact. `EntityWakeSignal` does not exist in the current codebase; `MovementAcousticSignal` is the existing AUP movement wake lane.
- Current worktree contains unrelated changes from other agents. This report claims only biolum director, coral shader, and bootstrap biolum registration work.

## Continuation Report - 2026-05-13

What was wrong:
- The ripple distance Burst job existed but its results were not used for nearest-first upload order.
- Normal Tick completion used `DispatcherJobSwap.TryComplete(false)`, which warns outside dispatcher swap windows in development builds.
- `_BiolumTouchRipples` uploaded every Tick even when low tier disabled the ripple shader path or no active ripples existed.
- Shader ripple falloff produced residual flash outside the authored radius because inverse-square energy was not hard-gated by `radiusSq`.
- Invalid movement/celestial/camera data could write non-finite values toward rendering or repeatedly trigger cold dump I/O.

What was done:
- Added fixed slot-index and distance arrays so `RippleDistanceJob` completion insertion-sorts the active ripples nearest-first without LINQ.
- Changed normal Tick finalization to `DispatcherJobSwap.TryFinalizeCompleted`; forced completion remains limited to teardown/origin-shift barriers.
- Replaced the single touch ripple GPU buffer with A/B `GraphicsBuffer` upload buffers and skipped uploads on low-tier/count-zero frames.
- Added finite guards for movement signal volume/velocity, celestial snapshot inputs, predator positions, camera telemetry, and staged ripple radius/position.
- Throttled `Dump_BIOLUMINESCENCE_DIRECTOR.bin` writes to one cold dump per 300 frames.
- Updated shader flash math to require `dot(diff,diff) <= radiusSq` before inverse-square boost.

Cinematic Cheats:
- Still one global phase, one predator dim scalar, fixed 16 ripples, and shader-side inverse-square fake.
- Low tier keeps only global phase; high/ultra spend the saved bandwidth on touch flashes and stronger synchronized response.

Exact Microseconds saved:
- Low-tier/count-zero GPU upload skip: estimated 5-15 us/frame when no active high-tier ripple work exists.
- Dispatcher warning avoidance: no direct runtime microsecond claim; removes development log spam and illegal completion path.
- Nearest-first fixed insertion sort: 0 B/frame; bounded 16 entries; correctness improvement over unused job output.

Verification:
- Per user instruction, no `dotnet build` was launched.
- `git diff --check` on touched files reports CRLF normalization warnings only.
- Forbidden hot-path scan found no `foreach`, LINQ, `ToString`, `distance`, `sqrt`, `normalize`, `SetData`, coroutine, `Camera.main`, scene find, or runtime material access in touched biolum/shader hot paths. The only matches were fixed-capacity cold `List<T>` fields already present in the manager.

## Continuation Report - 2026-05-13 - Coral Variant Sync

What was wrong:
- `_BiolumIntensity.x` carried `GlobalBiolumMultiplier` while the shader also applied `_HectonCelestialBiolumMultiplier`, doubling celestial boosts.
- `Hecton_CoralMaster_GPUI.shader` still used per-material `_BiolumPulseAmplitude` and `_BiolumPulseFrequency`, so instanced coral was still unsynchronized.
- The structured ripple buffer was declared in ForwardLit while the pass still advertised shader target 3.5.

What was done:
- `_BiolumIntensity.x` now carries only director dimming: global scale, daylight suppression, and predator blackout.
- The director no longer rejects daylight/eclipse snapshot data because of a `GlobalBiolumMultiplier` value it no longer consumes.
- `Hecton_CoralMaster_GPUI.shader` now matches the main coral shader global phase, intensity, touch ripple buffer, low-tier branch, and inverse-square flash path.
- ForwardLit target is 4.5 in both coral shaders because those are the only passes binding `StructuredBuffer<float4> _BiolumTouchRipples`.
- Reset global cache now records the actual reset phase vector, avoiding a redundant next-frame republish after disable.

Cinematic Cheats used:
- One shared pulse phase for regular and GPUI corals.
- Effective ripple radius packs lifetime and intensity into one float, keeping the shader payload at one `float4` per ripple.
- Celestial owns moon/eclipse brightness; biolum director owns suppression and blackout only.

Exact Microseconds saved:
- GPUI material pulse divergence removed: estimated 5-20 us/frame in dense instanced coral fields.
- Avoided doubled celestial bloom/overbright emission: no CPU claim; reduces GPU post-process pressure during high-biolum scenes.
- Reset cache fix: negligible runtime cost; prevents one redundant global vector publish after disable.

Verification:
- Per user instruction, no `dotnet build` was launched.
- Re-read `CURRENT_BATCH.md` with a flexible XML tag regex and confirmed the 18-task `BIOLUMINESCENCE_DIRECTOR` prompt.
- `rg` confirms `_BiolumPulseAmplitude` and `_BiolumPulseFrequency` are absent from both coral shader variants.
- `git diff --check` on touched code reports CRLF normalization warnings only.

## Continuation Report - 2026-05-13 - Global Type Collision

What was wrong:
- `_BiolumIntensity` was now a director-owned `float4`, but legacy `HectonBiolumController` still wrote it with `Shader.SetGlobalFloat`.
- `HectonIndirectVegetationRenderer` read `_BiolumIntensity` with `Shader.GetGlobalFloat`, which is no longer the contract.

What was done:
- Legacy controller scalar output moved to `_HectonLegacyBiolumIntensity`; it no longer clobbers `_BiolumIntensity`.
- Indirect vegetation darkness culling resolves the scalar from `Shader.GetGlobalVector(_BiolumIntensity).x`.
- `FloraCulling.compute` still receives a scalar through `ComputeShader.SetFloat`, but that scalar is now derived from the director vector.

Cinematic Cheats used:
- One vector global remains the authoritative biolum state bus.
- Vegetation culling consumes only the x lane for a cheap visibility decision; no new lighting simulation.

Exact Microseconds saved:
- Runtime CPU savings: 0 us/frame claimed.
- Prevented wasted render/bloom work when stale scalar intensity would have bypassed daylight or predator suppression.

Verification:
- Per user instruction, no `dotnet build` was launched.
- Static search confirms no `Shader.SetGlobalFloat` or `Shader.GetGlobalFloat` call targets `_BiolumIntensity`.

## Continuation Report - 2026-05-13 - Plant Pulse Unification

What was wrong:
- Kelp, GPUI kelp, and sargassum still had `_BiolumPulseAmplitude` and `_BiolumPulseFrequency`.
- Their authored biolum used local `_Time.y` pulse phases and did not respect `_BiolumIntensity.x` suppression.

What was done:
- Removed the remaining biolum pulse material properties and matching CBUFFER entries from those shaders.
- Kelp and GPUI kelp now use `_BiolumMasterPhase.x` for their spatial glow wave and multiply authored emission by `_BiolumIntensity.x`.
- Sargassum now uses `_BiolumMasterPhase.x` for bubble biolum and multiplies final biolum by `_BiolumIntensity.x`.

Cinematic Cheats used:
- Existing spatial offsets remain, but all temporal motion comes from one global phase.
- No 16-ripple buffer loop was added to kelp/sargassum; low-end cost stays flat.

Exact Microseconds saved:
- Estimated 5-25 us/frame risk avoided in dense plant fields by removing residual material pulse divergence.
- No measured profiler claim; build/runtime profiling was not launched.

Verification:
- Per user instruction, no `dotnet build` was launched.
- `rg` confirms `_BiolumPulseAmplitude` and `_BiolumPulseFrequency` are absent from `Assets/_Project/Art/Shaders`.
- `git diff --check` on touched files reports CRLF normalization warnings only.
