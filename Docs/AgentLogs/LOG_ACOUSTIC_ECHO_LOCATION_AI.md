# LOG_ACOUSTIC_ECHO_LOCATION_AI

## 2026-05-16 - Acoustic Echo Location AI
What was wrong:
- Abyssal predator acoustic targeting could still derive target intent from exact player noise positions, so blind navigation behaved like omniscient sight.
- Acoustic portal propagation exposed `LastPortalAup`, but predator cognition had no bounded sensory bridge consuming portal echo taps.
- DSP/sonar tap intake needed hard count clamps and invalid-number rejection before any predator path target was written.

What was done:
- Confirmed prompt `ACOUSTIC_ECHO_LOCATION_AI`, task count 18, domain AI/PATHING.
- Read AGENTS.md, domain doc, and 8 mandates: acoustic sonar, DSP queue, AI cognition, pathing, AUP determinism, zero-GC, blackbox telemetry, signal lane segregation.
- Implemented/verified `AcousticEchoLocationRuntime` contract: `NativeQueue<EchoTap>`, fixed 32-tap frame slab, `EchoTrackingJob`, portal-AUP result, black-box telemetry, sonar tap hydration, MovementAcousticSignal and AcousticPingSignal fallbacks.
- Wired portal propagation into `SpatialAudioManager` so path-found sounds enqueue echo taps using `AcousticPathResult.LastPortalAup`.
- Wired Fauna predator cognition to use `AcousticEchoLocationRuntime.TryResolvePredatorEcho`; acoustic-only targets now pass the echo portal/investigation AUP, not raw player noise AUP.
- Added high-tier acoustic head sweep via existing Fauna head-look target handoff; visual player contact still overrides the fake.
- Added five-second silence expiry. When the trail expires, no acoustic target is returned and cognition falls back to search/wander behavior.
- Added loudest-tap priority for decoys/noisemakers via `Volume01 * Transmission01` selection.

Cinematic cheats used:
- Low tier: source node = sound node, no portal solve, direct swim to last heard node.
- High tier: lateral head-look sweep is a cheap sine offset, not a physical smell/wake simulation.
- Portal breadcrumbing: predator follows audible portal position, not a full acoustic wave field.

Exact microseconds saved/estimated:
- Removed direct acoustic distance branch for blind predator target acquisition: ~3 us/predator tick.
- Fixed 32-tap Burst scan instead of managed event/list scan: ~20-80 us stall risk avoided on predator-heavy frames.
- Low-tier direct-node fake avoids portal solve entirely on MX350 path: portal solve cost avoided per low-tier echo.
- Head sweep fake cost: ~2-4 us/frame, replacing any full cone/current simulation.
- AUP finite checks and black-box write: ~1-3 us/frame, paid only on bounded tap processing.

Validation:
- `git diff --check` passed on touched acoustic/Fauna/audio/status/rationale files; only line-ending warnings reported.
- `dotnet build Hecton8.Core.csproj --no-restore` is blocked by external dependency churn outside this prompt: deleted `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs` and tether contract state from parallel work. No further external repair was performed after dependency wall.

Integrator notes:
- Resolve the Bucketing/Tether compile wall, then rerun `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`.
- Recheck `NativeArray<SonarEchoTap>.ReadOnly.IsCreated` against the active Unity.Collections version if compile reaches `AcousticEchoLocationRuntime`; the guard is intentionally there to reject invalid DSP snapshots.

## 2026-05-16 - Phase 0-4 Inquisition Polish
What was wrong:
- Acoustic tap/result/state/black-box structs were sequential but not explicitly packed, leaving ARM64/Quest layout proof weaker than required.
- The sensory runtime still had private persistent `NativeArray` fields for frame taps, job result, and black-box telemetry.
- Fault dumps could rewrite the same binary file repeatedly during a NaN storm, which is hostile to Steam Deck MicroSD I/O.

What was done:
- Re-ran the mandated status/rationale read and re-extracted the exact `ACOUSTIC_ECHO_LOCATION_AI` XML prompt.
- Added `SystemID.AISensory` and three `GlobalDataVault` buffer IDs: `AcousticEchoFrameTaps`, `AcousticEchoTrailState`, and `AcousticEchoBlackBox`.
- Moved the acoustic frame slab, job result, and 300-frame black-box ring to generation-checked `VaultBufferHandle<T>` resolution.
- Added `[StructLayout(LayoutKind.Sequential, Pack = 1)]` to every acoustic payload owned by this domain.
- Added finite guards around high-tier head-sweep delta and distance-square math.
- Gated `Dump_ACOUSTIC_ECHO_LOCATION_AI.bin` to one fault dump per session after the ring entry is written.
- Re-scanned `Assets/_Project/Scripts/AI/Sensory/` for `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, managed delegates, and private `NativeArray` allocation.

Cinematic cheats used:
- Low/toaster: direct-node acoustic breadcrumbs and 32-tap cap remain the hard budget guard.
- High/Ultra: acoustic CPU remains cheap enough for richer consumers: IK sweep, visor salt/silt presentation, and hull dent VFX can spend the saved cycles without making acoustic truth more expensive.

Exact microseconds saved/estimated:
- DataVault handle resolution costs roughly +1-2 us on refresh but removes private allocator ownership and stale-array risk.
- One-shot dump gate avoids repeated binary file rewrites during fault storms; runtime hot path cost is one integer branch only on fault.
- Extra finite guards cost below ~0.5 us on high-tier sweep frames and prevent NaN propagation into AI/presentation consumers.

Validation:
- Static scan: no private `NativeArray` fields or `new NativeArray` remain in `AI/Sensory/AcousticEchoLocationRuntime.cs`.
- Static scan: no `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, managed `Action`/`Func`, or duplicate `EchoTap` signal type found in AI/Sensory.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` remains blocked before AI/Sensory by unrelated errors in `World/EcosystemDirector.cs`, `Core/Determinism/LockstepStateValidator.cs`, `Animation/Locomotion/ProceduralLadderClimbRuntime.cs`, and `SubmarineFluidDynamics.cs`.

Integrator notes:
- Do not treat this as Unity-verified. It is code-review/static-scan verified behind an external compile wall.
- After external compile walls are repaired, rerun the same build command and then Unity Console/Play Mode GC validation for 300 frames.

## 2026-05-16 - Heartbeat And Green Build Pass
What was wrong:
- Black-box telemetry was still biased toward successful hunts and faults, not a strict per-refresh heartbeat ring.
- Echo ingress had a 32-tap processing cap but no explicit 64-tap queue backlog cap on the main-thread bridge.
- `AcousticHuntsTriggered` could wrap after `uint.MaxValue`.
- The previous build status was stale after other agents cleared the external compile wall.

What was done:
- Added one black-box heartbeat write per acoustic refresh frame with same-frame de-duplication.
- Prewarmed the `NativeQueue<EchoTap>` bridge to 64 taps and rejected main-thread enqueue attempts beyond that cap.
- Kept the 32-tap frame slab as the deterministic processing cap and drained surplus queue entries.
- Rejected non-finite `currentTime` through the fault black-box path and dropped queued taps instead of scheduling poisoned work.
- Saturated `AcousticHuntsTriggered` at `uint.MaxValue`.
- Reduced repeated DataVault polling by using the cached vault after handles are created, with fallback refresh only on resolve failure.
- Attempted required `<POLISH_MANDATE>` extraction from `Docs/Tasks/CURRENT_BATCH.md`; the tag is absent.

Cinematic cheats used:
- Toaster mode remains a capped last-node acoustic fake: 64 queued taps, 32 scored taps, no portal wave simulation.
- High/Ultra keep the shark-like head sweep and can spend saved CPU on presentation systems without widening acoustic truth cost.

Exact microseconds saved/estimated:
- Queue backlog cap prevents unbounded NativeQueue growth; hot cost is one integer branch per tap enqueue.
- Heartbeat write costs ~1 us/acoustic refresh and buys the actual 300-frame crash trail.
- Cached DataVault use avoids repeated registry lookup on normal refreshes; fallback lookup only occurs on stale-handle resolve failure.

Validation:
- `rg` scan found no private persistent `NativeArray`, `new NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, or managed `Action`/`Func` in `Assets/_Project/Scripts/AI/Sensory/`.
- `rg` scan found no unpacked `[StructLayout(LayoutKind.Sequential)]` in `Assets/_Project/Scripts/AI/Sensory/`.
- `git diff --check` on owned acoustic/status/rationale/log files reports CRLF normalization warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded: 0 warnings, 0 errors, 2m11s.

Runtime caveat:
- Unity Play Mode, GCMonitor, and device profiling were not run in this terminal-only pass. Runtime/profiler proof remains pending outside the dotnet compile gate.

## 2026-05-16 - Fresh Build Recheck / External Compile Wall
What was wrong:
- A fresh `dotnet build` no longer matched the prior green state after parallel-domain edits landed.
- The first new blocker was `InputDispatcher.cs` defining `HECTON8_MMF_AVAILABLE` after `using` tokens.
- After that compile-only blocker was cleared, the build failed in non-acoustic systems: `ProceduralBiteIkJobs.cs`, `ToolDurabilitySystem.cs`, and `GameBootstrapper.cs`.

What was done:
- Re-ran the mandated status/rationale read and re-extracted the exact `ACOUSTIC_ECHO_LOCATION_AI` XML prompt.
- Re-scanned `Assets/_Project/Scripts/AI/Sensory/` for forbidden hot-path patterns and unpacked structs; scans remained clean.
- Corrected only the `InputDispatcher.cs` preprocessor placement so the build could proceed past CS1032.
- Marked Task 18 as `[BLOCKED BY DEPENDENCY]` again because the remaining compile errors are outside AI/Sensory ownership.

Cinematic cheats used:
- Acoustic behavior unchanged: low-tier remains direct-node fake, high-tier remains cheap sine head sweep, and echo truth remains bounded to 32 scored taps.

Exact microseconds saved/estimated:
- No acoustic runtime cost added.
- The `InputDispatcher.cs` compile-only correction has 0 us acoustic impact.
- The blocked external build consumed 2m08s wall-clock and stopped before Unity runtime/profiler proof.

Validation:
- `rg` scan found no private persistent `NativeArray`, `new NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, or managed `Action`/`Func` in `Assets/_Project/Scripts/AI/Sensory/`.
- `rg` scan found no unpacked `[StructLayout(LayoutKind.Sequential)]` in `Assets/_Project/Scripts/AI/Sensory/`.
- `git diff --check` on owned acoustic/status/rationale/log files reports CRLF normalization warnings only.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed with 96 errors outside AI/Sensory.

Integrator notes:
- First remaining error groups: `ProceduralBiteIkJobs.cs(306,24)` duplicate local `candidate`; `ToolDurabilitySystem.cs` missing `TryResolveNativeState`, `TryResolveItemStates`, `TryResolvePendingDecay`, and backing native fields; `GameBootstrapper.cs(2761,34)` calls an `Initialize` overload that no longer exists.
- Do not treat the acoustic prompt as Unity-runtime verified until these external domains compile and Play Mode/profiler evidence is collected.
